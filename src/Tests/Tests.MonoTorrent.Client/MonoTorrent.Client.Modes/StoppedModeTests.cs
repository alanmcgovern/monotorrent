//
// StoppedModeTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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

using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Trackers;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{

    [TestFixture]
    public class StoppedModeTests
    {
        ConnectionPair conn;
        ConnectionManager ConnectionManager { get; set; }
        DiskManager DiskManager { get; set; }
        TorrentManager Manager { get; set; }
        PeerId Peer { get; set; }
        EngineSettings Settings { get; set; }
        ManualTrackerManager TrackerManager { get; set; }

        [SetUp]
        public void Setup ()
        {
            conn = new ConnectionPair ().DisposeAfterTimeout ();
            Settings = new EngineSettings ();
            DiskManager = new DiskManager (Settings, Factories.Default, new NullWriter ());
            ConnectionManager = new ConnectionManager ("LocalPeerId", Settings, Factories.Default, DiskManager);
            TrackerManager = new ManualTrackerManager ();

            long[] fileSizes = {
                Constants.BlockSize / 2,
                Constants.BlockSize * 32,
                Constants.BlockSize * 2,
                Constants.BlockSize * 13,
            };
            Manager = TestRig.CreateMultiFileManager (fileSizes, Constants.BlockSize * 2);
            Manager.SetTrackerManager (TrackerManager);
            Peer = new PeerId (new Peer (new PeerInfo (new Uri ("ipv4://123.123.123.123:12345"))), conn.Outgoing, new BitField (Manager.Torrent.PieceCount ()), Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic);
        }

        [TearDown]
        public void Teardown ()
        {
            conn.Dispose ();
            DiskManager.Dispose ();
        }

        [Test]
        public void AddConnection ()
        {
            Manager.Mode = new StoppedMode ();

            Assert.IsFalse (Peer.Connection.Disposed, "#1");
            Assert.Throws<NotSupportedException> (() => Manager.Mode.HandlePeerConnected (Peer));
        }
    }
}
