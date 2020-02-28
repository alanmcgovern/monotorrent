//
// TrackerTier.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;

namespace MonoTorrent.Client.Tracker
{
    public class TrackerTier
    {
        public ITracker ActiveTracker => Trackers[ActiveTrackerIndex];

        internal int ActiveTrackerIndex { get; set; }

        public IList<ITracker> Trackers { get; }

        internal bool SentStartedEvent { get; set; }

        internal ValueStopwatch LastAnnounce { get; set; }
        internal ValueStopwatch LastScrape { get; set; }

        public bool LastAnnounceSucceeded { get; internal set; }
        public bool LastScrapSucceeded { get; internal set; }

        public DateTime LastUpdated { get; internal set; }

        internal TimeSpan TimeSinceLastAnnounce => LastAnnounce.IsRunning ? LastAnnounce.Elapsed : TimeSpan.MaxValue;
        internal TimeSpan TimeSinceLastScrape => LastScrape.IsRunning ? LastScrape.Elapsed : TimeSpan.MaxValue;

        internal TrackerTier (IEnumerable<string> trackerUrls)
        {
            var trackerList = new List<ITracker> ();
            foreach (string trackerUrl in trackerUrls) {
                if (!Uri.TryCreate (trackerUrl, UriKind.Absolute, out Uri result)) {
                    Logger.Log (null, "TrackerTier - Invalid tracker Url specified: {0}", trackerUrl);
                    continue;
                }

                ITracker tracker = TrackerFactory.Create (result);
                if (tracker != null) {
                    trackerList.Add (tracker);
                } else {
                    Logger.Log (null, "Unsupported protocol {0}", result);
                }
            }

            Trackers = trackerList.AsReadOnly ();
        }
    }
}
