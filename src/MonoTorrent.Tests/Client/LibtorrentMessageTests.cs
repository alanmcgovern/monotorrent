using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    
    public class LibtorrentMessageTests
    {
        TestRig rig;
        byte[] buffer;

        [OneTimeSetUp]
        public void GlobalSetup()
        {
            rig = TestRig.CreateMultiFile();
        }

        [OneTimeTearDown]
        public void GlobalTeardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            buffer = new byte[100000];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;
        }

        [Fact]
        public void HandshakeSupportsTest()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage();
            byte[] encoded = m.Encode();

            Assert.Equal(m.ByteLength, encoded.Length, "#1");
            Assert.True(m.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(PeerExchangeMessage.Support.Name); }), "#2");
            Assert.True(m.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(LTChat.Support.Name); }), "#3");
            Assert.True(m.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(LTMetadata.Support.Name); }), "#4");
        }

        [Fact]
        public void HandshakeDecodeTest()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage();
            byte[] data = m.Encode();
            ExtendedHandshakeMessage decoded = (ExtendedHandshakeMessage)PeerMessage.DecodeMessage(data, 0, data.Length, rig.Manager);

            Assert.Equal(m.ByteLength, data.Length);
            Assert.Equal(m.ByteLength, decoded.ByteLength, "#1");
            Assert.Equal(m.LocalPort, decoded.LocalPort, "#2");
            Assert.Equal(m.MaxRequests, decoded.MaxRequests, "#3");
            Assert.Equal(m.Version, decoded.Version, "#4");
            Assert.Equal(m.Supports.Count, decoded.Supports.Count, "#5");
            m.Supports.ForEach(delegate(ExtensionSupport s) { Assert.True(decoded.Supports.Contains(s), "#6:" + s.ToString()); });
        }

        [Fact]
        public void LTChatDecodeTest()
        {
            LTChat m = new LTChat(LTChat.Support.MessageId, "This Is My Message");

            byte[] data = m.Encode();
            LTChat decoded = (LTChat)PeerMessage.DecodeMessage(data, 0, data.Length, rig.Manager);
        
            Assert.Equal(m.Message, decoded.Message, "#1");
        }

        [Fact]
        public void PeerExchangeMessageTest()
        {
            // Decodes as: 192.168.0.1:100
            byte[] peer = new byte[] { 192, 168, 0, 1, 100, 0 };
            byte[] supports = new byte[] { (byte)(1 | 2) }; // 1 == encryption, 2 == seeder

            byte id = MonoTorrent.Client.Messages.Libtorrent.PeerExchangeMessage.Support.MessageId;
            PeerExchangeMessage message = new PeerExchangeMessage(id, peer, supports, null);

            byte[] buffer = message.Encode();
            PeerExchangeMessage m = (PeerExchangeMessage)PeerMessage.DecodeMessage(buffer, 0, buffer.Length, this.rig.Manager);
            Assert.True(Toolbox.ByteMatch(peer, m.Added), "#1");
            Assert.True(Toolbox.ByteMatch(supports, m.AddedDotF), "#1");
        }

        /*public static void Main(string[] args)
        {
            LibtorrentMessageTests t = new LibtorrentMessageTests();
            t.GlobalSetup();
            t.Setup();
            t.HandshakeDecodeTest();
            t.LTChatDecodeTest();
            t.GlobalTeardown();
        }*/
    }
}
