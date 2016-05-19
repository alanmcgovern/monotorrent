using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using System;
using System.Collections.Generic;
using Xunit;

namespace MonoTorrent.Client
{
    public class PiecePickerTests : IDisposable
    {
        //static void Main(string[] args)
        //{
        //    PiecePickerTests t = new PiecePickerTests();
        //    t.Setup();
        //    t.RequestBlock();
        //    t.Setup();
        //    t.InvalidFastPiece();
        //    t.Setup();
        //    t.CancelRequests();
        //    t.Setup();
        //    t.RequestFastSeeder();
        //    t.Setup();
        //    t.RequestFastNotSeeder();
        //    t.Setup();
        //    t.RequestFastHaveEverything();
        //    t.Setup();
        //    t.RequestWhenSeeder();
        //    t.Setup();
        //    t.NoInterestingPieces();
        //    t.Setup();
        //    t.CancelRequests();
        //    t.Setup();
        //    t.RejectRequests();
        //    t.Setup();
        //    t.PeerChoked();
        //    t.Setup();
        //    t.FastPeerChoked();
        //    t.Setup();
        //    t.ChokeThenClose();
        //}
        protected PeerId peer;
        protected List<PeerId> peers;
        protected PiecePicker picker;
        protected TestRig rig;

        public PiecePickerTests()
        {
            // Yes, this is horrible. Deal with it.
            rig = TestRig.CreateMultiFile();
            peers = new List<PeerId>();
            picker = new IgnoringPicker(rig.Manager.Bitfield, new StandardPicker());
            picker.Initialise(rig.Manager.Bitfield, rig.Manager.Torrent.Files, new List<Piece>());
            peer = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            for (var i = 0; i < 20; i++)
            {
                var p = new PeerId(new Peer(new string(i.ToString()[0], 20), new Uri("tcp://" + i)), rig.Manager);
                p.SupportsFastPeer = true;
                peers.Add(p);
            }
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        [Fact]
        public void RequestFastSeeder()
        {
            var allowedFast = new int[] {1, 2, 3, 5, 8, 13, 21};
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange((int[]) allowedFast.Clone());

            peers[0].BitField.SetAll(true); // Lets pretend he has everything
            for (var i = 0; i < 7; i++)
            {
                for (var j = 0; j < 16; j++)
                {
                    var msg = picker.PickPiece(peers[0], peers);
                    Assert.NotNull(msg);
                    Assert.True(Array.IndexOf<int>(allowedFast, msg.PieceIndex) > -1, "#2." + j);
                }
            }
            Assert.Null(picker.PickPiece(peers[0], peers));
        }

        [Fact]
        public void RequestFastNotSeeder()
        {
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange(new int[] {1, 2, 3, 5, 8, 13, 21});

            peers[0].BitField.SetAll(true);
            peers[0].BitField[1] = false;
            peers[0].BitField[3] = false;
            peers[0].BitField[5] = false;

            for (var i = 0; i < 4; i++)
                for (var j = 0; j < 16; j++)
                {
                    var m = picker.PickPiece(peers[0], peers);
                    Assert.True(m.PieceIndex == 2 || m.PieceIndex == 8 || m.PieceIndex == 13 || m.PieceIndex == 21);
                }

            Assert.Null(picker.PickPiece(peers[0], peers));
        }

        [Fact]
        public void RequestFastHaveEverything()
        {
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange(new int[] {1, 2, 3, 5, 8, 13, 21});

            peers[0].BitField.SetAll(true);
            rig.Manager.Bitfield.SetAll(true);

            Assert.Null(picker.PickPiece(peers[0], peers));
        }

        [Fact]
        public void RequestChoked()
        {
            Assert.Null(picker.PickPiece(peers[0], peers));
        }

        [Fact]
        public void RequestWhenSeeder()
        {
            rig.Manager.Bitfield.SetAll(true);
            peers[0].BitField.SetAll(true);
            peers[0].IsChoking = false;

            Assert.Null(picker.PickPiece(peers[0], peers));
        }

        [Fact]
        public void NoInterestingPieces()
        {
            peer.IsChoking = false;
            for (var i = 0; i < rig.Manager.Bitfield.Length; i++)
                if (i%2 == 0)
                {
                    peer.BitField[i] = true;
                    rig.Manager.Bitfield[i] = true;
                }
            Assert.Null(picker.PickPiece(peer, peers));
        }

        [Fact]
        public virtual void CancelRequests()
        {
            var messages = new List<RequestMessage>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.PickPiece(peer, peers);
            Assert.Equal(rig.TotalBlocks, messages.Count);
            picker.CancelRequests(peer);

            var messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.Equal(messages.Count, messages2.Count);
            for (var i = 0; i < messages.Count; i++)
                Assert.True(messages2.Contains(messages[i]));
        }

        [Fact]
        public void RejectRequests()
        {
            var messages = new List<RequestMessage>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            foreach (var message in messages)
                picker.CancelRequest(peer, message.PieceIndex, message.StartOffset, message.RequestLength);

            var messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.Equal(messages.Count, messages2.Count);
            for (var i = 0; i < messages.Count; i++)
                Assert.True(messages2.Contains(messages[i]), "#2." + i);
        }

        [Fact]
        public void PeerChoked()
        {
            var messages = new List<RequestMessage>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.CancelRequests(peer);

            var messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.Equal(messages.Count, messages2.Count);
            for (var i = 0; i < messages.Count; i++)
                Assert.True(messages2.Contains(messages[i]), "#2." + i);
        }

        [Fact]
        //If a fast peer sends a choke message, CancelRequests will not be called"
        public void FastPeerChoked()
        {
            var messages = new List<RequestMessage>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);
            peer.SupportsFastPeer = true;

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.CancelRequests(peer);

            var messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.Equal(0, messages2.Count);
        }

        [Fact]
        public void ChokeThenClose()
        {
            var messages = new List<RequestMessage>();
            peer.IsChoking = false;
            peer.BitField.SetAll(true);
            peer.SupportsFastPeer = true;

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.CancelRequests(peer);

            var messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.Equal(messages.Count, messages2.Count);
            for (var i = 0; i < messages.Count; i++)
                Assert.True(messages2.Contains(messages[i]), "#2." + i);
        }

        [Fact]
        public void RequestBlock()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll(true);
            for (var i = 0; i < 1000; i++)
            {
                var b = picker.PickPiece(peer, peers, i);
                Assert.Equal(Math.Min(i, rig.TotalBlocks), b.Messages.Count);
                picker.CancelRequests(peer);
            }
        }

        [Fact]
        public void InvalidFastPiece()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange(new int[] {1, 2, 5, 55, 62, 235, 42624});
            peer.BitField.SetAll(true);
            for (var i = 0; i < rig.BlocksPerPiece*3; i++)
            {
                var m = picker.PickPiece(peer, peers);
                Assert.NotNull(m);
                Assert.True(m.PieceIndex == 1 || m.PieceIndex == 2 || m.PieceIndex == 5);
            }

            for (var i = 0; i < 10; i++)
                Assert.Null(picker.PickPiece(peer, peers));
        }

        [Fact]
        public void CompletePartialTest()
        {
            Piece piece;
            peer.IsChoking = false;
            peer.AmInterested = true;
            peer.BitField.SetAll(true);
            var message = picker.PickPiece(peer, peers);
            Assert.True(picker.ValidatePiece(peer, message.PieceIndex, message.StartOffset, message.RequestLength,
                out piece));
            picker.CancelRequests(peer);
            for (var i = 0; i < piece.BlockCount; i++)
            {
                message = picker.PickPiece(peer, peers);
                Piece p;
                Assert.True(
                    picker.ValidatePiece(peer, message.PieceIndex, message.StartOffset, message.RequestLength, out p),
                    "#2." + i);
            }
            Assert.True(piece.AllBlocksRequested);
            Assert.True(piece.AllBlocksReceived);
        }

        [Fact]
        public void DoesntHaveFastPiece()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange(new int[] {1, 2, 3, 4});
            peer.BitField.SetAll(true);
            picker = new StandardPicker();
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            var bundle = picker.PickPiece(peer, new Common.BitField(peer.BitField.Length), peers,
                1, 0, peer.BitField.Length);
            Assert.Null(bundle);
        }


        [Fact]
        public void DoesntHaveSuggestedPiece()
        {
            peer.IsChoking = false;
            peer.SupportsFastPeer = true;
            peer.SuggestedPieces.AddRange(new int[] {1, 2, 3, 4});
            peer.BitField.SetAll(true);
            picker = new StandardPicker();
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            var bundle = picker.PickPiece(peer, new Common.BitField(peer.BitField.Length), peers,
                1, 0, peer.BitField.Length);
            Assert.Null(bundle);
        }

        [Fact]
        public void InvalidSuggestPiece()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.SuggestedPieces.AddRange(new int[] {1, 2, 5, 55, 62, 235, 42624});
            peer.BitField.SetAll(true);
            picker.PickPiece(peer, peers);
        }

        [Fact]
        public void PickBundle()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll(true);

            MessageBundle bundle;
            var messages = new List<PeerMessage>();

            while ((bundle = picker.PickPiece(peer, peers, rig.BlocksPerPiece*5)) != null)
            {
                Assert.True(bundle.Messages.Count == rig.BlocksPerPiece*5
                            || bundle.Messages.Count + messages.Count == rig.TotalBlocks);
                messages.AddRange(bundle.Messages);
            }
            Assert.Equal(rig.TotalBlocks, messages.Count);
        }

        [Fact]
        public void PickBundle_2()
        {
            peer.IsChoking = false;

            for (var i = 0; i < 7; i++)
                peer.BitField[i] = true;

            MessageBundle bundle;
            var messages = new List<PeerMessage>();

            while ((bundle = picker.PickPiece(peer, peers, rig.BlocksPerPiece*5)) != null)
            {
                Assert.True(bundle.Messages.Count == rig.BlocksPerPiece*5
                            || bundle.Messages.Count + messages.Count == rig.BlocksPerPiece*7);
                messages.AddRange(bundle.Messages);
            }
            Assert.Equal(rig.BlocksPerPiece*7, messages.Count);
        }

        [Fact]
        public void PickBundle_3()
        {
            var messages = new List<PeerMessage>();
            peers[2].IsChoking = false;
            peers[2].BitField.SetAll(true);
            messages.Add(picker.PickPiece(peers[2], peers));

            peer.IsChoking = false;

            for (var i = 0; i < 7; i++)
                peer.BitField[i] = true;

            MessageBundle bundle;

            while ((bundle = picker.PickPiece(peer, peers, rig.BlocksPerPiece*5)) != null)
                messages.AddRange(bundle.Messages);

            Assert.Equal(rig.BlocksPerPiece*7, messages.Count);
        }

        [Fact]
        public void PickBundle4()
        {
            peers[0].IsChoking = false;
            peers[0].BitField.SetAll(true);

            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 4, 4);
            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 6, 6);

            var b = picker.PickPiece(peers[0], new List<PeerId>(), 20*rig.BlocksPerPiece);

            foreach (RequestMessage m in b.Messages)
                Assert.True(m.PieceIndex > 6);
        }

        [Fact]
        public void PickBundle5()
        {
            rig.Manager.Bitfield.SetAll(true);

            for (var i = 0; i < 20; i++)
            {
                rig.Manager.Bitfield[i%2] = false;
                rig.Manager.Bitfield[10 + i] = false;
            }

            peers[0].IsChoking = false;
            peers[0].BitField.SetAll(true);

            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 3, 3);
            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 6, 6);

            var b = picker.PickPiece(peers[0], new List<PeerId>(), 20*rig.BlocksPerPiece);
            Assert.Equal(20*rig.BlocksPerPiece, b.Messages.Count);
            foreach (RequestMessage m in b.Messages)
                Assert.True(m.PieceIndex >= 10 && m.PieceIndex < 30);
        }

        [Fact]
        public void PickBundle6()
        {
            rig.Manager.Bitfield.SetAll(false);

            peers[0].IsChoking = false;
            peers[0].BitField.SetAll(true);

            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 0, 0);
            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 1, 1);
            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 3, 3);
            for (var i = 0; i < rig.BlocksPerPiece; i++)
                picker.PickPiece(peers[0], peers[0].BitField, new List<PeerId>(), 1, 6, 6);

            var b = picker.PickPiece(peers[0], new List<PeerId>(), 2*rig.BlocksPerPiece);
            Assert.Equal(2*rig.BlocksPerPiece, b.Messages.Count);
            foreach (RequestMessage m in b.Messages)
                Assert.True(m.PieceIndex >= 4 && m.PieceIndex < 6);
        }

        [Fact]
        public void FastPieceTest()
        {
            for (var i = 0; i < 2; i++)
            {
                peers[i].BitField.SetAll(true);
                peers[i].SupportsFastPeer = true;
                peers[i].IsAllowedFastPieces.Add(5);
                peers[i].IsAllowedFastPieces.Add(6);
            }
            var m1 = picker.PickPiece(peers[0], new List<PeerId>());
            var m2 = picker.PickPiece(peers[1], new List<PeerId>());
            Assert.NotEqual(m1.PieceIndex, m2.PieceIndex);
        }
    }
}