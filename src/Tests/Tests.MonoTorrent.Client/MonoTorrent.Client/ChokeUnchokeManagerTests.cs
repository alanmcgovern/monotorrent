//
// ChokeUnchokeManagerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using System.Linq;

using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;

using NUnit.Framework;

namespace MonoTorrent.Client.Unchoking
{
    [TestFixture]
    public class ChokeUnchokeManagerTests
    {
        InfoHash ExpectedInfoHash = new InfoHash (new byte[20]);
        class Unchokeable : IUnchokeable
        {
            public event EventHandler<TorrentStateChangedEventArgs> StateChanged;

            public bool Seeding { get; set; }

            public long DownloadSpeed { get; set; }

            public long UploadSpeed { get; set; }

            public long MaximumDownloadSpeed { get; set; }

            public long MaximumUploadSpeed { get; set; }

            public int UploadSlots { get; set; }

            public int UploadingTo { get; set; }

            public List<PeerId> Peers { get; } = new List<PeerId> ();

            public Unchokeable (params PeerId[] peers)
            {
                Peers.AddRange (peers);
            }

            public void RaiseStateChanged (TorrentStateChangedEventArgs e)
            {
                StateChanged?.Invoke (this, e);
            }
        }

        [Test]
        public void ChokeTwoPeers ()
        {
            var unchokeable = new Unchokeable (
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };
            unchokeable.Peers.ForEach (p => { p.AmChoking = false; unchokeable.UploadingTo++; });
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);
            foreach (var peer in unchokeable.Peers.Where (t => t.AmChoking)) {
                Assert.IsInstanceOf<ChokeMessage> (peer.MessageQueue.TryDequeue ());
                Assert.AreEqual (0, peer.MessageQueue.QueueLength);
            }
        }

        [Test]
        public void ChokePeer_FastExtensions_RequestingFastPiece ()
        {
            var unchokeable = new Unchokeable (
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };

            unchokeable.Peers.ForEach (p => {
                p.AmChoking = false;
                unchokeable.UploadingTo++;
                p.AmAllowedFastPieces = new[] { 1 };
                p.SupportsFastPeer = true;
                p.MessageQueue.Enqueue (new PieceMessage (1, 0, Constants.BlockSize));
            });
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);
            foreach (var peer in unchokeable.Peers) {
                if (peer.AmChoking)
                    Assert.IsInstanceOf<ChokeMessage> (peer.MessageQueue.TryDequeue ());
                Assert.IsInstanceOf<PieceMessage> (peer.MessageQueue.TryDequeue ());
                Assert.AreEqual (0, peer.MessageQueue.QueueLength);
            }
        }

        [Test]
        public void ChokePeer_FastExtensions_RequestingPiece ()
        {
            var unchokeable = new Unchokeable (
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };

            unchokeable.Peers.ForEach (p => {
                p.AmChoking = false;
                unchokeable.UploadingTo++;
                p.SupportsFastPeer = true;
                p.MessageQueue.Enqueue (new PieceMessage (1, 0, Constants.BlockSize));
            });
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);
            foreach (var peer in unchokeable.Peers) {
                if (peer.AmChoking) {
                    Assert.IsInstanceOf<ChokeMessage> (peer.MessageQueue.TryDequeue ());
                    Assert.IsInstanceOf<RejectRequestMessage> (peer.MessageQueue.TryDequeue ());
                } else {
                    Assert.IsInstanceOf<PieceMessage> (peer.MessageQueue.TryDequeue ());
                }
                Assert.AreEqual (0, peer.MessageQueue.QueueLength);
            }
        }

        [Test]
        public void ChokePeer_NotInterested ()
        {
            var unchokeable = new Unchokeable (
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };

            // This is a peer who we unchoked because they wanted data
            // but now they do not want any more data from us.
            unchokeable.Peers.ForEach (p => {
                p.IsInterested = false;
                p.AmChoking = false;
                unchokeable.UploadingTo++;
            });
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.AreEqual (0, unchokeable.UploadingTo);
            foreach (var peer in unchokeable.Peers) {
                Assert.IsTrue (peer.AmChoking);
                Assert.IsInstanceOf<ChokeMessage> (peer.MessageQueue.TryDequeue ());
                Assert.AreEqual (0, peer.MessageQueue.QueueLength);
            }
        }

        [Test]
        public void ChokePeer_NotInterested_ThenInterested ()
        {
            var unchokeable = new Unchokeable (
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };
            unchokeable.Peers.ForEach (p => {
                p.IsInterested = false;
                p.AmChoking = true;
            });
            var unchoker = new ChokeUnchokeManager (unchokeable);
            unchoker.UnchokeReview ();
            Assert.AreEqual (0, unchokeable.UploadingTo);

            unchokeable.Peers.ForEach (p => {
                p.IsInterested = true;
            });
            unchoker.UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);
            // We preferred the one who has been choked the longest
            Assert.IsFalse (unchokeable.Peers[0].AmChoking);
        }

        [Test]
        public void ChokePeer_NotInterested_ThenInterestedAndDisposed ()
        {
            var unchokeable = new Unchokeable (
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash),
                PeerId.Create (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };
            unchokeable.Peers.ForEach (p => {
                p.IsInterested = true;
                p.AmChoking = true;
            });
            var unchoker = new ChokeUnchokeManager (unchokeable);
            unchoker.UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);

            unchokeable.Peers[1].Dispose ();
            unchokeable.Peers.RemoveAt (1);
            unchokeable.UploadSlots = 4;

            for (int i = 0; i < 10; i++) {
                unchoker.UnchokeReview ();
                unchoker.UnchokeReview ();
                unchoker.UnchokeReview ();
            }
        }

        [Test]
        public void ChokePeer_RequestingPiece ()
        {
            var unchokeable = new Unchokeable (
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };

            unchokeable.Peers.ForEach (p => {
                p.AmChoking = false;
                unchokeable.UploadingTo++;
                // SupportsFastPeer is set to false so this should be ignored.
                // This will always be empty during normal downloading.
                p.AmAllowedFastPieces = new[] { 1 };
                p.SupportsFastPeer = false;
                p.MessageQueue.Enqueue (new PieceMessage (1, 0, Constants.BlockSize));
            });
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);
            foreach (var peer in unchokeable.Peers) {
                if (peer.AmChoking) {
                    Assert.IsInstanceOf<ChokeMessage> (peer.MessageQueue.TryDequeue ());
                } else {
                    Assert.IsInstanceOf<PieceMessage> (peer.MessageQueue.TryDequeue ());
                }
                Assert.AreEqual (0, peer.MessageQueue.QueueLength);
            }
        }

        [Test]
        public void UnchokeOneWithOneSlot ()
        {
            var unchokeable = new Unchokeable (PeerId.CreateInterested (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };
            Assert.IsTrue (unchokeable.Peers[0].AmChoking);
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.IsFalse (unchokeable.Peers[0].AmChoking);
            Assert.AreEqual (1, unchokeable.UploadingTo);
        }

        [Test]
        public void UnchokeThreeWithOneSlot ()
        {
            var unchokeable = new Unchokeable (
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash),
                PeerId.CreateInterested (10, ExpectedInfoHash)) {
                UploadSlots = 1
            };
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);
        }

        [Test]
        public void UnchokeOneWithUnlimitedSlots ()
        {
            var unchokeable = new Unchokeable (PeerId.CreateInterested (10, ExpectedInfoHash)) {
                UploadSlots = 0
            };
            Assert.IsTrue (unchokeable.Peers[0].AmChoking);
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.IsFalse (unchokeable.Peers[0].AmChoking);
            Assert.AreEqual (1, unchokeable.UploadingTo);
        }
    }
}
