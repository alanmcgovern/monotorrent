//
// InitialSeedingModeTests.cs
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


using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{
    [TestFixture]
    public class InitialSeedingModeTests
    {
        InitialSeedingMode Mode {
            get { return Rig.Manager.Mode as InitialSeedingMode; }
        }

        TestRig Rig {
            get; set;
        }

        [SetUp]
        public void Setup ()
        {
            Rig = TestRig.CreateSingleFile (Piece.BlockSize * 20, Piece.BlockSize * 2);
            Rig.Manager.Bitfield.Not ();
            Rig.Manager.UnhashedPieces.SetAll (false);
            Rig.Manager.Mode = new InitialSeedingMode (Rig.Manager, Rig.Engine.DiskManager, Rig.Engine.ConnectionManager, Rig.Engine.Settings);
        }

        [TearDown]
        public void Teardown ()
        {
            Rig.Dispose ();
        }

        [Test]
        public void SwitchingModesSendsHaves ()
        {
            Rig.Manager.Peers.ConnectedPeers.Add (Rig.CreatePeer (true, true));
            Rig.Manager.Peers.ConnectedPeers.Add (Rig.CreatePeer (true, false));

            var peer = Rig.CreatePeer (true);
            peer.BitField.SetAll (true);
            Mode.HandlePeerConnected (peer);
            Mode.Tick (0);

            Assert.IsTrue (Rig.Manager.Peers.ConnectedPeers[0].MessageQueue.TryDequeue () is HaveAllMessage, "#1");
            BitfieldMessage m = (BitfieldMessage) Rig.Manager.Peers.ConnectedPeers[1].MessageQueue.TryDequeue ();
            Assert.IsTrue (m.BitField.AllTrue, "#2");
        }
    }
}
