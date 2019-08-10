using System;
using System.Collections.Generic;
using System.Linq;

using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;
using MonoTorrent.Common;
using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PiecePickerTests
    {
        PeerId peer;
        List<PeerId> peers;
        PieceManager manager;
        PiecePicker picker;
        StandardPicker standardPicker;
        TestRig rig;


        [SetUp]
        public virtual void Setup()
        {
            // Yes, this is horrible. Deal with it.
            rig = TestRig.CreateMultiFile();
            peers = new List<PeerId>();

            manager = new PieceManager ();
            manager.ChangePicker ((standardPicker = new StandardPicker()), rig.Manager.Bitfield, rig.Manager.Torrent.Files);
            this.picker = manager.Picker;

            peer = new PeerId(new Peer(new string('a', 20), new Uri("ipv4://BLAH")), rig.Manager);
            for (int i = 0; i < 20; i++)
            {
                PeerId p = new PeerId(new Peer(new string(i.ToString()[0], 20), new Uri("ipv4://" + i)), rig.Manager);
                p.SupportsFastPeer = true;
                peers.Add(p);
            }
        }

        [TearDown]
        public void GlobalTeardown()
        {
            rig.Dispose();
        }

        [Test]
        public void RequestFastSeeder()
        {
            int[] allowedFast = new int[] { 1, 2, 3, 5, 8, 13, 21 };
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange((int[])allowedFast.Clone());

            peers[0].BitField.SetAll(true); // Lets pretend he has everything
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    var msg = picker.PickPiece(peers[0], peers);
                    Assert.IsNotNull(msg, "#1." + j);
                    Assert.IsTrue(Array.IndexOf(allowedFast, msg.PieceIndex) > -1, "#2." + j);
                }
            }
            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }
        [Test]
        public void RequestFastNotSeeder()
        {
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange(new int[] { 1, 2, 3, 5, 8, 13, 21 });

            peers[0].BitField.SetAll(true);
            peers[0].BitField[1] = false;
            peers[0].BitField[3] = false;
            peers[0].BitField[5] = false;

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 16; j++)
                {
                    var m = picker.PickPiece(peers[0], peers);
                    Assert.IsTrue(m.PieceIndex == 2 || m.PieceIndex == 8 || m.PieceIndex == 13 || m.PieceIndex == 21);
                } 

            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }
        [Test]
        public void ReceiveAllPieces_PieceUnhashed()
        {
            peers[0].BitField.SetAll(true);
            peers[0].IsChoking = false;
            rig.Manager.Bitfield.SetAll (true).SetFalse (1);

            PieceRequest p;
            var requests = new List<PieceRequest> ();
            Piece piece = null;
            while ((p = picker.PickPiece(peers[0], peers)) != null) {
                piece = manager.PieceDataReceived (peers[0], new PieceMessage (p.PieceIndex, p.StartOffset, p.RequestLength)) ?? piece;
                if (requests.Any (t => t.PieceIndex == p.PieceIndex && t.RequestLength == p.RequestLength && t.StartOffset == p.StartOffset))
                    Assert.Fail ("We should not pick the same piece twice");
                requests.Add (p);
            }
            Assert.IsNull (picker.PickPiece(peers[0], peers), "#1");
            Assert.IsTrue (manager.UnhashedPieces[1], "#2");
            Assert.IsTrue (piece.AllBlocksReceived, "#3");
        }

        [Test]
        public void RequestFastHaveEverything()
        {
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange(new int[] { 1, 2, 3, 5, 8, 13, 21 });

            peers[0].BitField.SetAll(true);
            rig.Manager.Bitfield.SetAll(true);

            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }

        [Test]
        public void RequestChoked()
        {
            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }

        [Test]
        public void RequestWhenSeeder()
        {
            rig.Manager.Bitfield.SetAll(true);
            peers[0].BitField.SetAll(true);
            peers[0].IsChoking = false;

            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }

        [Test]
        public void StandardPicker_PickStandardPiece ()
        {
            peers [0].IsChoking = false;
            peers [0].BitField.SetAll (true);

            rig.Manager.Bitfield [1] = true;
            var message = standardPicker.PickPiece (peers [0], rig.Manager.Bitfield.Clone ().Not (), peers, 1, 0, 10);
            Assert.AreEqual (0, message[0].PieceIndex);

            peers [1].IsChoking = false;
            peers [1].BitField.SetAll (true);
            peers [1].Peer.HashedPiece (false);
            message = standardPicker.PickPiece (peers [1], rig.Manager.Bitfield.Clone ().Not (), peers, 1, 0, 10);
            Assert.AreEqual (2, message[0].PieceIndex);
        }

        [Test]
        public void NoInterestingPieces()
        {
            peer.IsChoking = false;
            for (int i = 0; i < rig.Manager.Bitfield.Length; i++)
                if (i % 2 == 0)
                {
                    peer.BitField[i] = true;
                    rig.Manager.Bitfield[i] = true;
                }
            Assert.IsNull(picker.PickPiece(peer, peers));
        }

        [Test]
        public virtual void CancelRequests()
        {
            var messages = new List<PieceRequest>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            PieceRequest m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.PickPiece(peer, peers);
            Assert.AreEqual(rig.TotalBlocks, messages.Count, "#0");
            picker.CancelRequests(peer);

            var messages2 = new List<PieceRequest>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]));
        }

        [Test]
        public void RejectRequests()
        {
            var messages = new List<PieceRequest>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            PieceRequest m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            foreach (PieceRequest message in messages)
                picker.CancelRequest(peer, message.PieceIndex, message.StartOffset, message.RequestLength);

            var messages2 = new List<PieceRequest>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]), "#2." + i);
        }

        [Test]
        public void PeerChoked()
        {
            var messages = new List<PieceRequest>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            PieceRequest m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.CancelRequests(peer);

            var messages2 = new List<PieceRequest>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]), "#2." + i);
        }

        [Test]
        public void ChokeThenClose()
        {
            var messages = new List<PieceRequest>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);
            peer.SupportsFastPeer = true;

            PieceRequest m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.CancelRequests(peer);

            var messages2 = new List<PieceRequest>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]), "#2." + i);
        }

        [Test]
        public void RequestBlock()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll(true);
            for (int i = 0; i < 1000; i++)
            {
                var b = picker.PickPiece(peer, peers, i);
                Assert.AreEqual(Math.Min(i, rig.TotalBlocks), b.Count);
                picker.CancelRequests(peer);
            }
        }

        [Test]
        public void InvalidFastPiece()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange(new int[] { 1, 2, 5, 55, 62, 235, 42624 });
            peer.BitField.SetAll(true);
            for (int i = 0; i < rig.BlocksPerPiece * 3; i++)
            {
                var m = picker.PickPiece(peer, peers);
                Assert.IsNotNull(m, "#1." + i.ToString());
                Assert.IsTrue(m.PieceIndex == 1 || m.PieceIndex == 2 || m.PieceIndex == 5, "#2");
            }

            for (int i = 0; i < 10; i++)
                Assert.IsNull(picker.PickPiece(peer, peers), "#3");
        }

        [Test]
        public void CompletePartialTest()
        {
            Piece piece;
            peer.IsChoking = false;
            peer.AmInterested = true;
            peer.BitField.SetAll(true);
            var message = picker.PickPiece(peer, peers);
            Assert.IsTrue(picker.ValidatePiece(peer, message.PieceIndex, message.StartOffset, message.RequestLength, out piece), "#1");
            picker.CancelRequests(peer);
            for (int i = 0; i < piece.BlockCount; i++)
            {
                message = picker.PickPiece(peer, peers);
                Piece p;
                Assert.IsTrue(picker.ValidatePiece(peer, message.PieceIndex, message.StartOffset, message.RequestLength, out p), "#2." + i);
            }
            Assert.IsTrue(piece.AllBlocksRequested, "#3");
            Assert.IsTrue(piece.AllBlocksReceived, "#4");
        }

        [Test]
        public void DoesntHaveFastPiece()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange(new int[] { 1, 2, 3, 4 });
            peer.BitField.SetAll(true);
            picker = new StandardPicker();
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            var bundle = picker.PickPiece(peer, new BitField(peer.BitField.Length), peers, 1, 0, peer.BitField.Length);
            Assert.IsNull(bundle);
        }


        [Test]
        public void DoesntHaveSuggestedPiece()
        {
            peer.IsChoking = false;
            peer.SupportsFastPeer = true;
            peer.SuggestedPieces.AddRange(new int[] { 1, 2, 3, 4 });
            peer.BitField.SetAll(true);
            picker = new StandardPicker();
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            var bundle = picker.PickPiece(peer, new BitField(peer.BitField.Length), peers, 1, 0, peer.BitField.Length);
            Assert.IsNull(bundle);
        }

        [Test]
        public void InvalidSuggestPiece()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.SuggestedPieces.AddRange(new int[] { 1, 2, 5, 55, 62, 235, 42624 });
            peer.BitField.SetAll(true);
            picker.PickPiece(peer, peers);
        }

        [Test]
        public void PickBundle()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            IList<PieceRequest> bundle;
            var messages = new List<PieceRequest>();
            
            while ((bundle = picker.PickPiece(peer, peers, rig.BlocksPerPiece * 5)) != null)
            {
                Assert.IsTrue(bundle.Count == rig.BlocksPerPiece * 5
                              || (bundle.Count + messages.Count) == rig.TotalBlocks, "#1");
                messages.AddRange(bundle);
            }
            Assert.AreEqual(rig.TotalBlocks, messages.Count, "#2");
        }

        [Test]
        public void PickBundle_2()
        {
            peer.IsChoking = false;

            for (int i = 0; i < 7; i++)
                peer.BitField[i] = true;
            
            IList<PieceRequest> bundle;
            var messages = new List<PieceRequest>();

            while ((bundle = picker.PickPiece(peer, peers, rig.BlocksPerPiece * 5)) != null)
            {
                Assert.IsTrue(bundle.Count == rig.BlocksPerPiece * 5
                              || (bundle.Count + messages.Count) == rig.BlocksPerPiece * 7, "#1");
                messages.AddRange(bundle);
            }
            Assert.AreEqual(rig.BlocksPerPiece * 7, messages.Count, "#2");
        }

        [Test]
        public void PickBundle_3()
        {
            var messages = new List<PieceRequest>();
            peers[2].IsChoking = false;
            peers[2].BitField.SetAll(true);
            messages.Add(picker.PickPiece(peers[2], peers));

            peer.IsChoking = false;

            for (int i = 0; i < 7; i++)
                peer.BitField[i] = true;

            IList<PieceRequest> bundle;

            while ((bundle = picker.PickPiece(peer, peers, rig.BlocksPerPiece * 5)) != null)
                messages.AddRange(bundle);

            Assert.AreEqual(rig.BlocksPerPiece * 7, messages.Count, "#2");
        }

        [Test]
        public void PickBundle4()
        {
            peers[0].IsChoking = false;
            peers[0].BitField.SetAll(true);

            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 4, 4);
            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 6, 6);

            var b = picker.PickPiece(peers[0], new List<PeerId>(), 20 * rig.BlocksPerPiece);
            foreach (PieceRequest m in b)
                Assert.IsTrue(m.PieceIndex > 6);
        }

        [Test]
        public void PickBundle5()
        {
            rig.Manager.Bitfield.SetAll(true);

            for (int i = 0; i < 20; i++)
            {
                rig.Manager.Bitfield[i % 2] = false;
                rig.Manager.Bitfield[10 + i] = false;
            }
            
            peers[0].IsChoking = false;
            peers[0].BitField.SetAll(true);

            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 3, 3);
            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 6, 6);

            var b = picker.PickPiece(peers[0], new List<PeerId>(), 20 * rig.BlocksPerPiece);
            Assert.AreEqual(20 * rig.BlocksPerPiece, b.Count);
            foreach (PieceRequest m in b)
                Assert.IsTrue(m.PieceIndex >=10 && m.PieceIndex < 30);
        }

        [Test]
        public void PickBundle6()
        {
            rig.Manager.Bitfield.SetAll(false);

            peers[0].IsChoking = false;
            peers[0].BitField.SetAll(true);

            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 0, 0);
            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 1, 1);
            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 3, 3);
            for (int i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 6, 6);

            var b = picker.PickPiece(peers[0], new List<PeerId>(), 2 * rig.BlocksPerPiece);
            Assert.AreEqual(2 * rig.BlocksPerPiece, b.Count);
            foreach (PieceRequest m in b)
                Assert.IsTrue(m.PieceIndex >= 4 && m.PieceIndex < 6);
        }

        [Test]
        public void FastPieceTest()
        {
            for (int i = 0; i < 2; i++)
            {
                peers[i].BitField.SetAll(true);
                peers[i].SupportsFastPeer = true;
                peers[i].IsAllowedFastPieces.Add(5);
                peers[i].IsAllowedFastPieces.Add(6);
            }
            var m1 = picker.PickPiece(peers[0], new List<PeerId>());
            var m2 = picker.PickPiece(peers[1], new List<PeerId>());
            Assert.AreNotEqual(m1.PieceIndex, m2.PieceIndex, "#1");
        }
    }
}
