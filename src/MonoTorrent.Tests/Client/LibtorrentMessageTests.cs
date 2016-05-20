using System;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class LibtorrentMessageTests : IDisposable
    {
        public LibtorrentMessageTests()
        {
            rig = TestRig.CreateMultiFile();

            buffer = new byte[100000];
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        private readonly TestRig rig;
        private readonly byte[] buffer;

        [Fact]
        public void HandshakeDecodeTest()
        {
            var m = new ExtendedHandshakeMessage();
            var data = m.Encode();
            var decoded =
                (ExtendedHandshakeMessage) PeerMessage.DecodeMessage(data, 0, data.Length, rig.Manager);

            Assert.Equal(m.ByteLength, data.Length);
            Assert.Equal(m.ByteLength, decoded.ByteLength);
            Assert.Equal(m.LocalPort, decoded.LocalPort);
            Assert.Equal(m.MaxRequests, decoded.MaxRequests);
            Assert.Equal(m.Version, decoded.Version);
            Assert.Equal(m.Supports.Count, decoded.Supports.Count);
            m.Supports.ForEach(
                delegate(ExtensionSupport s) { Assert.True(decoded.Supports.Contains(s), "#6:" + s); });
        }

        [Fact]
        public void HandshakeSupportsTest()
        {
            var m = new ExtendedHandshakeMessage();
            var encoded = m.Encode();

            Assert.Equal(m.ByteLength, encoded.Length);
            Assert.True(
                m.Supports.Exists(
                    delegate(ExtensionSupport s) { return s.Name.Equals(PeerExchangeMessage.Support.Name); }));
            Assert.True(m.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(LTChat.Support.Name); }));
            Assert.True(
                m.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(LTMetadata.Support.Name); }));
        }

        [Fact]
        public void LTChatDecodeTest()
        {
            var m = new LTChat(LTChat.Support.MessageId, "This Is My Message");

            var data = m.Encode();
            var decoded = (LTChat) PeerMessage.DecodeMessage(data, 0, data.Length, rig.Manager);

            Assert.Equal(m.Message, decoded.Message);
        }

        [Fact]
        public void PeerExchangeMessageTest()
        {
            // Decodes as: 192.168.0.1:100
            var peer = new byte[] {192, 168, 0, 1, 100, 0};
            var supports = new[] {(byte) (1 | 2)}; // 1 == encryption, 2 == seeder

            var id = PeerExchangeMessage.Support.MessageId;
            var message = new PeerExchangeMessage(id, peer, supports, null);

            var buffer = message.Encode();
            var m =
                (PeerExchangeMessage) PeerMessage.DecodeMessage(buffer, 0, buffer.Length, rig.Manager);
            Assert.True(Toolbox.ByteMatch(peer, m.Added));
            Assert.True(Toolbox.ByteMatch(supports, m.AddedDotF));
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