﻿using MediaBrowser.Common;
using MediaBrowser.Common.Events;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.UserInput;
using MediaBrowser.Theater.Presentation.Controls;
using MediaBrowser.UI.Controls;
using MediaBrowser.UI.Implementations;
using Microsoft.Expression.Media.Effects;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MediaBrowser.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseWindow
    {
        internal event EventHandler<NavigationEventArgs> Navigated;

        internal RotatingBackdrops RotatingBackdrops { get; private set; }

        private readonly ILogger _logger;
        private readonly IApplicationHost _appHost;
        private readonly IPresentationManager _appWindow;
        private readonly ITheaterConfigurationManager _config;
        private readonly ISessionManager _session;
        private readonly IPlaybackManager _playbackManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow" /> class.
        /// </summary>
        public MainWindow(ILogger logger, IPlaybackManager playbackManager, IApiClient apiClient, IImageManager imageManager, IApplicationHost appHost, IPresentationManager appWindow, IUserInputManager userInput, ITheaterConfigurationManager config, ISessionManager session, INavigationService nav)
            : base(userInput, nav)
        {
            _logger = logger;
            _appHost = appHost;
            _appWindow = appWindow;
            _config = config;
            _session = session;
            _playbackManager = playbackManager;

            Loaded += MainWindow_Loaded;

            InitializeComponent();

            RotatingBackdrops = new RotatingBackdrops(Dispatcher, BackdropContainer, imageManager, apiClient, playbackManager, _config);

            WindowCommands.ApplicationHost = _appHost;
            _config.UserConfigurationUpdated += _config_UserConfigurationUpdated;
            _config.ConfigurationUpdated += _config_ConfigurationUpdated;
            _session.UserLoggedIn += _session_UserLoggedIn;
            _session.UserLoggedOut += _session_UserLoggedOut;
            _playbackManager.PlaybackStarted += _playbackManager_PlaybackStarted;
            _playbackManager.PlaybackCompleted += _playbackManager_PlaybackCompleted;
        }

        void _playbackManager_PlaybackStarted(object sender, PlaybackStartEventArgs e)
        {
            if (e.Player is IInternalMediaPlayer)
            {
                Dispatcher.InvokeAsync(() => OnPropertyChanged("IsMouseIdle"), DispatcherPriority.Background);
            }

            UpdateBackdropContainerVisibility();
        }

        void _playbackManager_PlaybackCompleted(object sender, PlaybackStopEventArgs e)
        {
            Dispatcher.InvokeAsync(() => BackdropContainer.Visibility = Visibility.Visible);
        }

        private void UpdateBackdropContainerVisibility()
        {
            var visibility = _playbackManager.MediaPlayers.Any(i =>
            {
                var media = i.CurrentMedia;

                return media != null && !media.IsAudio;

            })
                                               ? Visibility.Collapsed
                                               : Visibility.Visible;

            Dispatcher.InvokeAsync(() => BackdropContainer.Visibility = visibility);
        }

        void _session_UserLoggedOut(object sender, EventArgs e)
        {
            UpdatePageTransition();
        }

        void _session_UserLoggedIn(object sender, EventArgs e)
        {
            UpdatePageTransition();
        }

        void _config_ConfigurationUpdated(object sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() => RenderOptions.SetBitmapScalingMode(this, _config.Configuration.EnableHighQualityImageScaling ? BitmapScalingMode.Fant : BitmapScalingMode.LowQuality));
        }

        void _config_UserConfigurationUpdated(object sender, UserConfigurationUpdatedEventArgs e)
        {
            Dispatcher.InvokeAsync(() => PageFrame.TransitionType = GetTransitionEffect(e.Configuration.NavigationTransition));
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = App.Instance;

            DragBar.MouseDown += DragableGridMouseDown;

            ((TheaterApplicationWindow)_appWindow).OnWindowLoaded();

            RenderOptions.SetBitmapScalingMode(this, _config.Configuration.EnableHighQualityImageScaling
                                                   ? BitmapScalingMode.Fant
                                                   : BitmapScalingMode.LowQuality);

            UpdatePageTransition();
        }

        private async void UpdatePageTransition()
        {
            if (_session.CurrentUser != null)
            {
                var userConfig = await _config.GetUserTheaterConfiguration(_session.CurrentUser.Id).ConfigureAwait(false);
                Dispatcher.InvokeAsync(() => PageFrame.TransitionType = GetTransitionEffect(userConfig.NavigationTransition));
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (RotatingBackdrops != null)
            {
                RotatingBackdrops.Dispose();
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// Dragables the grid mouse down.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs" /> instance containing the event data.</param>
        private void DragableGridMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Gets the page frame.
        /// </summary>
        /// <value>The page frame.</value>
        internal TransitionFrame PageFrame
        {
            get
            {
                // Finding the grid that is generated by the ControlTemplate of the Button
                var container = TreeHelper.FindChild<PageContainer>(PageContent, "PageContainer");

                return container.Frame;
            }
        }

        internal Page CurrentPage
        {
            get { return PageFrame.Content as Page; }
        }

        private TransitionEffect GetTransitionEffect(string name)
        {
            switch (name.ToLower())
            {
                case "blur":
                    return new RadialBlurTransitionEffect();
                case "circle reveal":
                    return new CircleRevealTransitionEffect();
                case "cloud reveal":
                    return new CloudRevealTransitionEffect();
                case "fade":
                    return new FadeTransitionEffect();
                case "horizontal blinds":
                    return new BlindsTransitionEffect { Orientation = BlindOrientation.Horizontal };
                case "horizontal slide":
                    return new SlideInTransitionEffect { SlideDirection = SlideDirection.RightToLeft };
                case "horizontal wipe":
                    return new WipeTransitionEffect { WipeDirection = WipeDirection.RightToLeft };
                case "ripple":
                    return new RippleTransitionEffect();
                case "smooth swirl":
                    return new SmoothSwirlGridTransitionEffect();
                case "vertical blinds":
                    return new BlindsTransitionEffect { Orientation = BlindOrientation.Vertical };
                case "vertical slide":
                    return new SlideInTransitionEffect { SlideDirection = SlideDirection.TopToBottom };
                case "vertical wipe":
                    return new WipeTransitionEffect { WipeDirection = WipeDirection.TopToBottom };
                case "wave":
                    return new WaveTransitionEffect();
                default:
                    return null;
            }
        }

        /// <summary>
        /// Navigates the specified page.
        /// </summary>
        /// <param name="page">The page.</param>
        internal Task Navigate(Page page)
        {
            _logger.Info("Navigating to " + page.GetType().Name);
            var task = new TaskCompletionSource<bool>();

            Dispatcher.InvokeAsync(async () =>
            {
                var current = CurrentPage;

                await PageFrame.NavigateWithTransition(page);

                task.TrySetResult(true);

                EventHelper.FireEventIfNotNull(Navigated, this, new NavigationEventArgs
                {
                    NewPage = page,
                    OldPage = current

                }, _logger);
            });

            return task.Task;
        }

        /// <summary>
        /// Navigates the back.
        /// </summary>
        internal Task NavigateBack()
        {
            var task = new TaskCompletionSource<bool>();

            Dispatcher.InvokeAsync(async () =>
            {
                if (PageFrame.NavigationService.CanGoBack)
                {
                    var current = CurrentPage;

                    await PageFrame.GoBackWithTransition();

                    EventHelper.FireEventIfNotNull(Navigated, this, new NavigationEventArgs
                    {
                        NewPage = CurrentPage,
                        OldPage = current

                    }, _logger);
                }
                task.TrySetResult(true);
            });

            return task.Task;
        }

        /// <summary>
        /// Navigates the forward.
        /// </summary>
        internal Task NavigateForward()
        {
            var task = new TaskCompletionSource<bool>();

            Dispatcher.InvokeAsync(async () =>
            {
                if (PageFrame.NavigationService.CanGoForward)
                {
                    var current = CurrentPage;

                    await PageFrame.GoForwardWithTransition();

                    EventHelper.FireEventIfNotNull(Navigated, this, new NavigationEventArgs
                    {
                        NewPage = CurrentPage,
                        OldPage = current

                    }, _logger);
                }
                task.TrySetResult(true);
            });

            return task.Task;
        }

        internal void ClearNavigationHistory()
        {
            var frame = PageFrame;

            if (frame.CanGoBack)
            {
                while (frame.RemoveBackEntry() != null)
                {

                }
            }
        }

        /// <summary>
        /// Removes the pages from history.
        /// </summary>
        /// <param name="count">The count.</param>
        internal void RemovePagesFromHistory(int count)
        {
            var frame = PageFrame;

            var current = 0;

            if (frame.CanGoBack)
            {
                while (current < count && frame.RemoveBackEntry() != null)
                {
                    current++;
                }
            }
        }

        /// <summary>
        /// Called when [browser back].
        /// </summary>
        protected override void OnBrowserBack()
        {
            base.OnBrowserBack();

            NavigateBack();
        }

        /// <summary>
        /// Called when [browser forward].
        /// </summary>
        protected override void OnBrowserForward()
        {
            base.OnBrowserForward();

            NavigateForward();
        }
    }
}
