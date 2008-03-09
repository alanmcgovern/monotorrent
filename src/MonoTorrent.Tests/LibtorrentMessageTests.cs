using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SampleClient;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.ExtendedMessageTests
{
    [TestFixture]
    public class LibtorrentMessageTests
    {
        private EngineTestRig rig;
        byte[] buffer;
        int offset = 2362;

        [TestFixtureSetUp]
        public void GlobalSetup()
        {
            rig = new EngineTestRig("");
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
            Assert.AreEqual(2, m.Supports.Count, "#2");
            Assert.IsTrue(m.Supports.Exists(delegate(LTSupport s) { return s.Equals(LTChat.Support); }), "#3");
            Assert.IsTrue(m.Supports.Exists(delegate(LTSupport s) { return s.Equals(LTMetadata.Support); }), "#4");
        }

        [Test]
        public void HandshakeDecodeTest()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage();
            byte[] data = m.Encode();
            ExtendedHandshakeMessage decoded = (ExtendedHandshakeMessage)PeerMessage.DecodeMessage(data, 4, data.Length - 4, rig.Manager);

            Assert.AreEqual(m.ByteLength, data.Length);
            Assert.AreEqual(m.ByteLength, decoded.ByteLength, "#1");
            Assert.AreEqual(m.LocalPort, decoded.LocalPort, "#2");
            Assert.AreEqual(m.MaxRequests, decoded.MaxRequests, "#3");
            Assert.AreEqual(m.Version, decoded.Version, "#4");
            Assert.AreEqual(m.Supports.Count, decoded.Supports.Count, "#5");
            m.Supports.ForEach(delegate(LTSupport s) { Assert.IsTrue(decoded.Supports.Contains(s), "#6:" + s.ToString()); });
        }

        [Test]
        public void LTChatDecodeTest()
        {
            LTChat m = new LTChat();
            m.Message = "This Is My Message";

            byte[] data = m.Encode();
            LTChat decoded = (LTChat)PeerMessage.DecodeMessage(data, 4, data.Length - 4, rig.Manager);
        
            Assert.AreEqual(m.Message, decoded.Message, "#1");
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
