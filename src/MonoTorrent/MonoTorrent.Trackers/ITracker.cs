//
// ITracker.cs
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


using System;
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Client.Tracker
{
    public interface ITracker
    {
        /// <summary>
        /// True if the tracker supports Scrape requests.
        /// </summary>
        bool CanScrape { get; }

        /// <summary>
        /// The minimum interval between announce requests.
        /// </summary>
        TimeSpan MinUpdateInterval { get; }

        /// <summary>
        /// The recommended interval between announce requests.
        /// </summary>
        TimeSpan UpdateInterval { get; }

        /// <summary>
        /// The time since the last announce request was sent.
        /// </summary>
        TimeSpan TimeSinceLastAnnounce { get; }

        /// <summary>
        /// The status of the tracker after the most recent announce request.
        /// </summary>
        TrackerState Status { get; }

        /// <summary>
        /// The uri for the tracker
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// The warning message sent with the most recent announce request.
        /// </summary>
        string WarningMessage { get; }

        /// <summary>
        /// The failure message sent with the most recent announce request.
        /// </summary>
        string FailureMessage { get; }

        /// <summary>
        /// Send an announce request to the tracker.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="token">The token used to cancel the request.</param>
        /// <returns></returns>
        ReusableTask<AnnounceResponse> AnnounceAsync (AnnounceParameters parameters, CancellationToken token);

        /// <summary>
        /// Send a scrape request to the tracker.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="token">The token used to cancel the request.</param>
        /// <returns></returns>
        ReusableTask<ScrapeResponse> ScrapeAsync (ScrapeParameters parameters, CancellationToken token);
    }
}
