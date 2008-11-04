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
        private ConnectionPair pair;
        private TestRig rig;

        [SetUp]
        public void Setup()
        {
            pair = new ConnectionPair(55432);
            rig = new TestRig("");

            rig.AddConnection(pair.Outgoing);
        }

        [TearDown]
        public void Teardown()
        {
            pair.Dispose();
            rig.Dispose();
        }

        [Test]
        public void TestHandshake()
        {
            // 1) Send local handshake
            SendIncoming (new HandshakeMessage(rig.Manager.Torrent.infoHash, new string('g', 20), VersionInfo.ProtocolStringV100, true, false));

            // 2) Receive remote handshake
            byte[] buffer = new byte[68];
            pair.Incoming.EndReceive(pair.Incoming.BeginReceive(buffer, 0, 68, null, null));
            HandshakeMessage handshake = new HandshakeMessage();
            handshake.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(rig.Engine.PeerId, handshake.PeerId, "#2");
            Assert.AreEqual(VersionInfo.ProtocolStringV100, handshake.ProtocolString, "#3");
            Assert.AreEqual(ClientEngine.SupportsFastPeer, handshake.SupportsFastPeer, "#4");
            Assert.AreEqual(ClientEngine.SupportsExtended, handshake.SupportsExtendedMessaging, "#5");

            // 2) Send local bitfield
            SendIncoming (new BitfieldMessage(rig.Manager.Bitfield));

            // 3) Receive remote bitfield - have none
            PeerMessage message = (HaveNoneMessage)ReceiveIncoming();
			Assert.IsTrue (message is HaveNoneMessage, "HaveNone");
			
            // 4) Send a few allowed fast
            SendIncoming(new AllowedFastMessage(1));
            SendIncoming(new AllowedFastMessage(2));
            SendIncoming(new AllowedFastMessage(3));
            SendIncoming(new AllowedFastMessage(0));

            // 5) Receive a few allowed fast
            ReceiveIncoming();
            ReceiveIncoming();
            ReceiveIncoming();
            ReceiveIncoming();
            ReceiveIncoming();
            ReceiveIncoming();
            ReceiveIncoming();
        }

        private void SendIncoming(PeerMessage message)
        {
            byte[] b = message.Encode();
            IAsyncResult result = pair.Incoming.BeginSend(b, 0, b.Length, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                throw new Exception("Message didn't send correctly");
            pair.Incoming.EndSend(result);
        }

        private PeerMessage ReceiveIncoming()
        {
            byte[] buffer = new byte[4];
            IAsyncResult result = pair.Incoming.BeginReceive(buffer, 0, 4, null, null);
            if(!result.AsyncWaitHandle.WaitOne (5000, true))
                throw new Exception("Message length didn't receive correctly");
            pair.Incoming.EndReceive(result);

            int count = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0));
            byte[] message = new byte[count + 4];
            Buffer.BlockCopy(buffer, 0, message, 0, 4);

            result = pair.Incoming.BeginReceive(message, 4, count, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                throw new Exception("Message body didn't receive correctly");
            pair.Incoming.EndReceive(result);

            return PeerMessage.DecodeMessage(message, 0, message.Length, rig.Manager);
        }
    }
}
