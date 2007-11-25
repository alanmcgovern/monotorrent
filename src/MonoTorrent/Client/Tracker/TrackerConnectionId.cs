//
// TrackerConnectionId.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
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



using MonoTorrent.Common;
using System.Net;


namespace MonoTorrent.Client
{
    /// <summary>
    /// Provides a method of keeping a TrackerConnection linked with its TorrentManager during an AsyncRequest
    /// </summary>
    public class TrackerConnectionID
    {
        internal TorrentEvent TorrentEvent;

        /// <summary>
        /// Object containing information about the async event
        /// </summary>
        public object Request;


        /// <summary>
        /// Either Announcing or Scraping depending on which event is about to happen
        /// </summary>
        public Tracker Tracker;


        public TrackerTier TrackerTier;


        internal bool TrySubsequent;


        #region Constructors
        /// <summary>
        /// Creates a new TrackerConnectionID
        /// </summary>
        /// <param name="request">Object containing information about the Async Request</param>
        /// <param name="manager">The TorrentManager associated with the TrackerConnection</param>
        internal TrackerConnectionID(TrackerTier tier, Tracker tracker, bool trySubsequent, TorrentEvent torrentEvent, object request)
        {
            this.TrackerTier = tier;
            this.Tracker = tracker;
            this.TrySubsequent = trySubsequent;
            this.TorrentEvent = torrentEvent;
            this.Request = request;
        }
        #endregion
    }
}
