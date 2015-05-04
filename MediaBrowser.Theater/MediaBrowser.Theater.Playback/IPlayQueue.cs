using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace MediaBrowser.Theater.Playback
{
    public interface IPlayQueue : INotifyCollectionChanged, IList<Media>
    {
        event Action<RepeatMode> RepeatModeChanged;
        event Action<SortMode> SortModeChanged;
        RepeatMode RepeatMode { get; set; }
        SortMode SortMode { get; set; }
        IPlaySequence<Media> GetPlayOrder();
        void AddRange(IEnumerable<Media> items);
    }
}