using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages;
using MonoTorrentTests;
using MonoTorrent.Common;

namespace MonoTorrent.Client.ExtendedMessageTests
{
    [TestFixture]
    public class LibtorrentMessageTests
    {
        TestRig rig;
        byte[] buffer;
        int offset = 2362;

        [TestFixtureSetUp]
        public void GlobalSetup()
        {
            rig = new TestRig("");
        }

        [TestFixtureTearDown]
        public void GlobalTeardown()
        {
            rig.Engine.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            buffer = new byte[100000];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;
        }

        [Test]
        public void HandshakeSupportsTest()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage();
            byte[] encoded = m.Encode();

            Assert.AreEqual(m.ByteLength, encoded.Length, "#1");
            Assert.IsTrue(m.Supports.Exists(delegate(ExtensionSupport s) { return s.Equals(PeerExchangeMessage.Support); }), "#2");
            Assert.IsTrue(m.Supports.Exists(delegate(ExtensionSupport s) { return s.Equals(LTChat.Support); }), "#3");
            Assert.IsTrue(m.Supports.Exists(delegate(ExtensionSupport s) { return s.Equals(LTMetadata.Support); }), "#4");
        }

        [Test]
        public void HandshakeDecodeTest()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage();
            byte[] data = m.Encode();
            ExtendedHandshakeMessage decoded = (ExtendedHandshakeMessage)PeerMessage.DecodeMessage(data, 0, data.Length, rig.Manager);

            Assert.AreEqual(m.ByteLength, data.Length);
            Assert.AreEqual(m.ByteLength, decoded.ByteLength, "#1");
            Assert.AreEqual(m.LocalPort, decoded.LocalPort, "#2");
            Assert.AreEqual(m.MaxRequests, decoded.MaxRequests, "#3");
            Assert.AreEqual(m.Version, decoded.Version, "#4");
            Assert.AreEqual(m.Supports.Count, decoded.Supports.Count, "#5");
            m.Supports.ForEach(delegate(ExtensionSupport s) { Assert.IsTrue(decoded.Supports.Contains(s), "#6:" + s.ToString()); });
        }

        [Test]
        public void LTChatDecodeTest()
        {
            LTChat m = new LTChat();
            m.Message = "This Is My Message";

            byte[] data = m.Encode();
            LTChat decoded = (LTChat)PeerMessage.DecodeMessage(data, 0, data.Length, rig.Manager);
        
            Assert.AreEqual(m.Message, decoded.Message, "#1");
        }

        [Test]
        public void PeerExchangeMessageTest()
        {
            // Decodes as: 192.168.0.1:100
            byte[] peer = new byte[] { 192, 168, 0, 1, 100, 0 };
            byte[] supports = new byte[] { (byte)(1 | 2) }; // 1 == encryption, 2 == seeder
            PeerExchangeMessage message = new PeerExchangeMessage(peer, supports, null);

            byte[] buffer = message.Encode();
            PeerExchangeMessage m = (PeerExchangeMessage)PeerMessage.DecodeMessage(buffer, 0, buffer.Length, this.rig.Manager);
            Assert.IsTrue(Toolbox.ByteMatch(peer, m.Added), "#1");
            Assert.IsTrue(Toolbox.ByteMatch(supports, m.AddedDotF), "#1");
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
