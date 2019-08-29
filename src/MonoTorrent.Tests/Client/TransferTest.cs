//
// TransferTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class TransferTest
    {
        static readonly TimeSpan Timeout = Debugger.IsAttached ? TimeSpan.FromHours (1) : TimeSpan.FromSeconds (5);

        CancellationTokenSource Cancellation { get ; set; }

        IEncryption decryptor = PlainTextEncryption.Instance;
        IEncryption encryptor = PlainTextEncryption.Instance;

        private ConnectionPair pair;
        private TestRig rig;

        [SetUp]
        public async Task Setup()
        {
            pair = new ConnectionPair(55432);

            Cancellation = new CancellationTokenSource (Timeout);
            Cancellation.Token.Register (pair.Dispose);

            rig = TestRig.CreateMultiFile();
            rig.Manager.HashChecked = true;
            await rig.Manager.StartAsync();
        }

        [TearDown]
        public async Task Teardown()
        {
            await rig.Manager.StopAsync();
            pair.Dispose();
            rig.Dispose();
        }

        [Test]
        public async Task MassiveMessage()
        {
            rig.AddConnection(pair.Incoming);
            await InitiateTransfer(pair.Outgoing, EncryptionTypes.All);
            _ = pair.Outgoing.SendAsync(new byte[] { 255 >> 1, 255, 255, 250 }, 0, 4);
            var receiveTask = pair.Outgoing.ReceiveAsync(new byte[1000], 0, 1000);
            if (!receiveTask.Wait (1000))
                Assert.Fail("Connection never closed");

            int r = receiveTask.Result;
            if (r != 0)
                Assert.Fail("Connection should've been closed");
        }

        [Test]
        public async Task NegativeData()
        {
            rig.AddConnection(pair.Incoming);
            await InitiateTransfer(pair.Outgoing, EncryptionTypes.All);
            await pair.Outgoing.SendAsync(new byte[] { 255, 255, 255, 250 }, 0, 4);
            var receiveTask = pair.Outgoing.ReceiveAsync(new byte[1000], 0, 1000);
            if (!receiveTask.Wait(1000))
                Assert.Fail("Connection never closed");

            int r = receiveTask.Result;
            if (r != 0)
                Assert.Fail("Connection should've been closed");
        }

        public async Task InitiateTransfer(CustomConnection connection, EncryptionTypes allowedEncryption)
        {
            EncryptorFactory.EncryptorResult result;
            if (connection.IsIncoming) {
                result = await EncryptorFactory.CheckIncomingConnectionAsync(connection, allowedEncryption, rig.Engine.Settings, new [] { rig.Manager.InfoHash });
            } else {
                result = await EncryptorFactory.CheckOutgoingConnectionAsync(connection, allowedEncryption, rig.Engine.Settings, rig.Manager.InfoHash);
            }
            decryptor = result.Decryptor;
            encryptor = result.Encryptor;
            await TestHandshake(result.Handshake, connection);
        }

        internal async Task TestHandshake(HandshakeMessage handshake, CustomConnection connection)
        {
            // 1) Send local handshake
            var sendHandshake = new HandshakeMessage(rig.Manager.Torrent.InfoHash, new string('g', 20), VersionInfo.ProtocolStringV100, true, false);
            await PeerIO.SendMessageAsync (connection, encryptor, sendHandshake);

            // 2) Receive remote handshake
            if (handshake == null)
                handshake = await PeerIO.ReceiveHandshakeAsync (connection, decryptor);

            Assert.AreEqual(rig.Engine.PeerId, handshake.PeerId, "#2");
            Assert.AreEqual(VersionInfo.ProtocolStringV100, handshake.ProtocolString, "#3");
            Assert.AreEqual(ClientEngine.SupportsFastPeer, handshake.SupportsFastPeer, "#4");
            Assert.AreEqual(ClientEngine.SupportsExtended, handshake.SupportsExtendedMessaging, "#5");

            // 2) Send local bitfield
            await PeerIO.SendMessageAsync (connection, encryptor, new BitfieldMessage(rig.Manager.Bitfield));

            // 3) Receive remote bitfield - have none
            PeerMessage message = await PeerIO.ReceiveMessageAsync(connection, decryptor);
			Assert.IsTrue (message is HaveNoneMessage || message is BitfieldMessage, "HaveNone");
			
            // 4) Send a few allowed fast
            await PeerIO.SendMessageAsync(connection, encryptor, new AllowedFastMessage(1));
            await PeerIO.SendMessageAsync(connection, encryptor, new AllowedFastMessage(2));
            await PeerIO.SendMessageAsync(connection, encryptor, new AllowedFastMessage(3));
            await PeerIO.SendMessageAsync(connection, encryptor, new AllowedFastMessage(0));

            // 5) Receive a few allowed fast
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
            await PeerIO.ReceiveMessageAsync(connection, decryptor);
        }
    }
}
