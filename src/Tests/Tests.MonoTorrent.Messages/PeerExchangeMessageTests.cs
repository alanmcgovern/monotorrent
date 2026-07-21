using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Messages;

using NUnit.Framework;

namespace MonoTorrent.Messages.Peer.Libtorrent
{
    [TestFixture]
    public class PeerExchangeMessageTests
    {
        [Test]
        public void PeerExchangeMessageDecode ()
        {
            // Decodes as: 192.168.0.1:100
            byte[] peer = { 192, 168, 0, 1, 100, 0 };
            byte[] peerDotF = { 1 | 2 }; // 1 == encryption, 2 == seeder

            byte[] peer6 = IPAddress.Parse ("::1234:5678").GetAddressBytes ();
            byte[] peer6DotF = { 1 | 2 }; // 1 == encryption, 2 == seeder

            (var message, var releaser) = MessageEncoder.Extended.WritePeerExchange(MessageEncoder.Extended.SupportedMessages, peer, peerDotF, default, peer6, peer6DotF, default);

            var m = new Extended.PeerExchangeMessage(message);
            Assert.IsTrue (peer.AsSpan ().SequenceEqual (m.Added), "#1");
            Assert.IsTrue (peerDotF.AsSpan ().SequenceEqual (m.AddedDotF), "#2");

            Assert.IsTrue (peer6.AsSpan ().SequenceEqual (m.Added6), "#3");
            Assert.IsTrue (peer6DotF.AsSpan ().SequenceEqual (m.Added6DotF), "#4");
        }

        [Test]
        public void PeerExchangeMessageDecode_Empty ()
        {
            (var data, var releaser) = MessageEncoder.Extended.WritePeerExchange (MessageEncoder.Extended.SupportedMessages, default, default, default, default, default, default);
            var message = new Extended.PeerExchangeMessage (data);
            Assert.IsTrue (message.Added.IsEmpty, "#1");
            Assert.IsTrue (message.AddedDotF.IsEmpty, "#2");
            Assert.IsTrue (message.Dropped.IsEmpty, "#3");

            Assert.IsTrue (message.Added6.IsEmpty, "#1");
            Assert.IsTrue (message.Added6DotF.IsEmpty, "#2");
            Assert.IsTrue (message.Dropped6.IsEmpty, "#3");
        }
    }
}
