using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using System;
using Xunit;

namespace MonoTorrent.Client
{
    public class InitialSeedingModeTest : IDisposable
    {
        private InitialSeedingMode Mode
        {
            get { return Rig.Manager.Mode as InitialSeedingMode; }
        }

        private TestRig Rig { get; set; }

        public InitialSeedingModeTest()
        {
            Rig = TestRig.CreateSingleFile(Piece.BlockSize*20, Piece.BlockSize*2);
            Rig.Manager.Bitfield.Not();
            Rig.Manager.Mode = new InitialSeedingMode(Rig.Manager);
        }

        public void Dispose()
        {
            Rig.Dispose();
        }

        [Fact]
        public void SwitchingModesSendsHaves()
        {
            Rig.Manager.Peers.ConnectedPeers.Add(Rig.CreatePeer(true, true));
            Rig.Manager.Peers.ConnectedPeers.Add(Rig.CreatePeer(true, false));

            var peer = Rig.CreatePeer(true);
            peer.BitField.SetAll(true);
            Mode.HandlePeerConnected(peer, Direction.Incoming);
            Mode.Tick(0);

            Assert.True(Rig.Manager.Peers.ConnectedPeers[0].Dequeue() is HaveAllMessage);
            var m = (BitfieldMessage) Rig.Manager.Peers.ConnectedPeers[1].Dequeue();
            Assert.True(m.BitField.AllTrue);
        }
    }
}