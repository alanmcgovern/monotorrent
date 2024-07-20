//
// TrackerTests.cs
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
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.Trackers;

using NUnit.Framework;

namespace MonoTorrent.TrackerServer
{
    [TestFixture]
    public class TrackerTests
    {
        Uri uri = null;
        HttpTrackerListener listener;
        TrackerServer server;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            for (int i = 23456; i < 23556; i++) {
                uri = new Uri ($"http://127.0.0.1:{i}/announce/");
                try {
                    listener = new HttpTrackerListener (uri.OriginalString);
                    listener.Start ();
                    break;
                } catch {
                    continue;
                }
            }

            server = new TrackerServer ();
            server.RegisterListener (listener);
        }

        [OneTimeTearDown]
        public void FixtureTeardown ()
        {
            listener.Stop ();
            server.Dispose ();
        }

        [SetUp]
        public void Setup ()
        {
            //tracker = new MonoTorrent.Client.Tracker.HTTPTracker(uri);
        }

        [Test]
        public async Task MultipleAnnounce ()
        {
            Random r = new Random ();

            for (int i = 0; i < 20; i++) {
                var buffer = new byte[20];
                r.NextBytes (buffer);
                var infoHash = InfoHashes.FromV1 (new InfoHash (buffer));
                TrackerTier tier = new TrackerTier (Factories.Default, new[] { uri.ToString () });
                var parameters = new MonoTorrent.Trackers.AnnounceRequest (0, 0, 0, TorrentEvent.Started,
                                                                       infoHash, false, new BEncodedString (new string ('1', 20)).Span.ToArray (), t => ("", 1411), false);
                Assert.IsTrue (tier.ActiveTracker.CanScrape);
                await tier.Trackers[0].AnnounceAsync (parameters, CancellationToken.None);
            }
        }

        [Test]
        public async Task MultipleScrape ()
        {
            Random r = new Random ();

            for (int i = 0; i < 20; i++) {
                var buffer = new byte[20];
                r.NextBytes (buffer);
                var infoHash = InfoHashes.FromV1 (new InfoHash (buffer));
                TrackerTier tier = new TrackerTier (Factories.Default, new[] { uri.ToString () });
                var parameters = new MonoTorrent.Trackers.ScrapeRequest (infoHash);
                await tier.Trackers[0].ScrapeAsync (parameters, CancellationToken.None);
            }
        }
    }
}
