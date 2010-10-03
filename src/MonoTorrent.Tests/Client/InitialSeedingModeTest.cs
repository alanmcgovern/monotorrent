using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class InitialSeedingModeTest
    {
        InitialSeedingMode Mode {
            get { return Rig.Manager.Mode as InitialSeedingMode; }
        }

        TestRig Rig {
            get; set;
        }

        [SetUp]
        public void Setup()
        {
            Rig = TestRig.CreateSingleFile(Piece.BlockSize * 20, Piece.BlockSize * 2);
            Rig.Manager.Bitfield.Not ();
            Rig.Manager.Mode = new InitialSeedingMode(Rig.Manager);
        }

        [TearDown]
        public void Teardown()
        {
            Rig.Dispose();
        }

        [Test]
        public void SwitchingModesSendsHaves()
        {
            Rig.Manager.Peers.ConnectedPeers.Add(Rig.CreatePeer(true, true));
            Rig.Manager.Peers.ConnectedPeers.Add(Rig.CreatePeer(true, false));

            var peer = Rig.CreatePeer(true);
            peer.BitField.SetAll(true);
            Mode.HandlePeerConnected(peer, Direction.Incoming);
            Mode.Tick(0);

            Assert.IsTrue(Rig.Manager.Peers.ConnectedPeers[0].Dequeue() is HaveAllMessage, "#1");
            BitfieldMessage m = (BitfieldMessage) Rig.Manager.Peers.ConnectedPeers[1].Dequeue();
            Assert.IsTrue(m.BitField.AllTrue, "#2");
        }
    }
}
