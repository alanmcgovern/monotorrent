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
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using NUnit.Framework;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Encryption;


namespace MonoTorrent.Client
{
    [TestFixture]
    public class TransferTest
    {
        //static void Main(string[] args)
        //{
        //    TransferTest t = new TransferTest();
        //    t.Setup();
        //    t.TestHandshake();
        //    t.Teardown();
        //}

        IEncryption decryptor = new PlainTextEncryption();
        IEncryption encryptor = new PlainTextEncryption();

        private ConnectionPair pair;
        private TestRig rig;

        [SetUp]
        public void Setup()
        {
            pair = new ConnectionPair(55432);
            rig = new TestRig("");
            rig.Manager.Start();
        }

        [TearDown]
        public void Teardown()
        {
            rig.Manager.Stop();
            pair.Dispose();
            rig.Dispose();
        }

        [Test]
        public void IncomingEncrypted()
        {
            rig.Engine.Settings.PreferEncryption = true;
            rig.AddConnection(pair.Outgoing);
            InitiateTransfer(pair.Incoming);
        }

        [Test]
        public void IncomingUnencrypted()
        {
            rig.Engine.Settings.PreferEncryption = false;
            rig.AddConnection(pair.Outgoing);
            InitiateTransfer(pair.Incoming);
        }

        [Test]
        public void OutgoingEncrypted()
        {
            rig.Engine.Settings.PreferEncryption = true;
            rig.AddConnection(pair.Incoming);
            InitiateTransfer(pair.Outgoing);
        }

        [Test]
        public void OutgoingUnencrypted()
        {
            rig.Engine.Settings.PreferEncryption = false;
            rig.AddConnection(pair.Incoming);
            InitiateTransfer(pair.Outgoing);
        }

        [Test]
        public void MassiveMessage()
        {
            rig.AddConnection(pair.Incoming);
            InitiateTransfer(pair.Outgoing);
            pair.Outgoing.EndSend(pair.Outgoing.BeginSend(new byte[] { 255 >> 1, 255, 255, 250 }, 0, 4, null, null));
            IAsyncResult result = pair.Outgoing.BeginReceive(new byte[1000], 0, 1000, null, null);
            if (!result.AsyncWaitHandle.WaitOne(1000, true))
                Assert.Fail("Connection never closed");

            int r = pair.Outgoing.EndReceive(result);
            if (r != 0)
                Assert.Fail("Connection should've been closed");
        }

        [Test]
        public void NegativeData()
        {
            rig.AddConnection(pair.Incoming);
            InitiateTransfer(pair.Outgoing);
            pair.Outgoing.EndSend(pair.Outgoing.BeginSend(new byte[] { 255, 255, 255, 250 }, 0, 4, null, null));
            IAsyncResult result = pair.Outgoing.BeginReceive(new byte[1000], 0, 1000, null, null);
            if (!result.AsyncWaitHandle.WaitOne(1000, true))
                Assert.Fail("Connection never closed");

            int r = pair.Outgoing.EndReceive(result);
            if (r != 0)
                Assert.Fail("Connection should've been closed");
        }

        public void InitiateTransfer(CustomConnection connection)
        {
            PeerId id = new PeerId(new Peer("", connection.Uri), rig.Manager);
            id.Connection = connection;
            id.recieveBuffer = new ArraySegment<byte>(new byte[68]);
            byte[] data = id.recieveBuffer.Array;
            id.BytesToRecieve = 68;
            
            EncryptorFactory.EndCheckEncryption(EncryptorFactory.BeginCheckEncryption(id, null, null, new byte[][] {id.TorrentManager.Torrent.infoHash }), out data);
            decryptor = id.Decryptor;
            encryptor = id.Encryptor;
            TestHandshake(data, connection);
        }

        public void TestHandshake(byte[] buffer, CustomConnection connection)
        {
            // 1) Send local handshake
            SendMessage(new HandshakeMessage(rig.Manager.Torrent.infoHash, new string('g', 20), VersionInfo.ProtocolStringV100, true, false), connection);

            // 2) Receive remote handshake
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[68];
                connection.EndReceive(connection.BeginReceive(buffer, 0, 68, null, null));
                decryptor.Decrypt(buffer);
            }

            HandshakeMessage handshake = new HandshakeMessage();
            handshake.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(rig.Engine.PeerId, handshake.PeerId, "#2");
            Assert.AreEqual(VersionInfo.ProtocolStringV100, handshake.ProtocolString, "#3");
            Assert.AreEqual(ClientEngine.SupportsFastPeer, handshake.SupportsFastPeer, "#4");
            Assert.AreEqual(ClientEngine.SupportsExtended, handshake.SupportsExtendedMessaging, "#5");

            // 2) Send local bitfield
            SendMessage(new BitfieldMessage(rig.Manager.Bitfield), connection);

            // 3) Receive remote bitfield - have none
            PeerMessage message = ReceiveMessage(connection);
			Assert.IsTrue (message is HaveNoneMessage || message is BitfieldMessage, "HaveNone");
			
            // 4) Send a few allowed fast
            SendMessage(new AllowedFastMessage(1), connection);
            SendMessage(new AllowedFastMessage(2), connection);
            SendMessage(new AllowedFastMessage(3), connection);
            SendMessage(new AllowedFastMessage(0), connection);

            // 5) Receive a few allowed fast
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
            ReceiveMessage(connection);
        }

        private void SendMessage(PeerMessage message, CustomConnection connection)
        {
            byte[] b = message.Encode();
            encryptor.Encrypt(b);
            IAsyncResult result = connection.BeginSend(b, 0, b.Length, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                throw new Exception("Message didn't send correctly");
            connection.EndSend(result);
        }

        private PeerMessage ReceiveMessage(CustomConnection connection)
        {
            byte[] buffer = new byte[4];
            IAsyncResult result = connection.BeginReceive(buffer, 0, 4, null, null);
            if(!result.AsyncWaitHandle.WaitOne (5000, true))
                throw new Exception("Message length didn't receive correctly");
            connection.EndReceive(result);
            decryptor.Decrypt(buffer);

            int count = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0));
            byte[] message = new byte[count + 4];
            Buffer.BlockCopy(buffer, 0, message, 0, 4);

            result = connection.BeginReceive(message, 4, count, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                throw new Exception("Message body didn't receive correctly");
            connection.EndReceive(result);
            decryptor.Decrypt(message, 4, count);

            return PeerMessage.DecodeMessage(message, 0, message.Length, rig.Manager);
        }
    }
}
