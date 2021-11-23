//
// ScrapeResponse.cs
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

namespace MonoTorrent.Trackers
{
    public abstract class TrackerResponse
    {
        /// <summary>
        /// The number of active peers which have completed downloading.
        /// </summary>
        public int Complete { get; }

        /// <summary>
        /// The number of peers that have ever completed downloading.
        /// </summary>
        public int Downloaded { get; }

        /// <summary>
        /// The number of active peers which have not completed downloading.
        /// </summary>
        public int Incomplete { get; }

        /// <summary>
        /// The failure message returned by the tracker.
        /// </summary>
        public string FailureMessage { get; }

        /// <summary>
        /// The status of the tracker after this request.
        /// </summary>
        public TrackerState State { get; }

        /// <summary>
        /// The warning message returned by the tracker.
        /// </summary>
        public string WarningMessage { get; }

        protected TrackerResponse (
            TrackerState state,
            int? complete = null,
            int? incomplete = null,
            int? downloaded = null,
            string warningMessage = null,
            string failureMessage = null
            )
        {
            State = state;
            Complete = complete ?? 0;
            Incomplete = incomplete ?? 0;
            Downloaded = downloaded ?? 0;
            WarningMessage = warningMessage ?? "";
            FailureMessage = failureMessage ?? "";
        }
    }
}
