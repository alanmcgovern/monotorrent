//
// LibtorrentMessageTests.cs
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


using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages.Libtorrent;

using NUnit.Framework;

namespace MonoTorrent.Client.Messages
{
    [TestFixture]
    public class LibtorrentMessageTests
    {
        [Test]
        public void HandshakeSupportsTest ()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage (false, 1234, 5555);
            byte[] encoded = m.Encode ();

            Assert.AreEqual (m.ByteLength, encoded.Length, "#1");
            Assert.IsTrue (m.Supports.Exists (s => s.Name.Equals (PeerExchangeMessage.Support.Name)), "#2");
            Assert.IsTrue (m.Supports.Exists (s => s.Name.Equals (LTChat.Support.Name)), "#3");
            Assert.IsTrue (m.Supports.Exists (s => s.Name.Equals (LTMetadata.Support.Name)), "#4");
            Assert.AreEqual (ClientEngine.DefaultMaxPendingRequests, m.MaxRequests, "#5");
        }

        [Test]
        public void HandshakeSupportsTest_Private ()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage (true, 123, 5555);
            byte[] encoded = m.Encode ();

            Assert.AreEqual (m.ByteLength, encoded.Length, "#1");
            Assert.IsFalse (m.Supports.Exists (s => s.Name.Equals (PeerExchangeMessage.Support.Name)), "#2");
            Assert.IsTrue (m.Supports.Exists (s => s.Name.Equals (LTChat.Support.Name)), "#3");
            Assert.IsTrue (m.Supports.Exists (s => s.Name.Equals (LTMetadata.Support.Name)), "#4");
        }

        [Test]
        public void HandshakeDecodeTest ()
        {
            ExtendedHandshakeMessage m = new ExtendedHandshakeMessage (false, 123, 5555);
            byte[] data = m.Encode ();
            ExtendedHandshakeMessage decoded = (ExtendedHandshakeMessage) PeerMessage.DecodeMessage (data, 0, data.Length, null);

            Assert.AreEqual (m.ByteLength, data.Length);
            Assert.AreEqual (m.ByteLength, decoded.ByteLength, "#1");
            Assert.AreEqual (m.LocalPort, decoded.LocalPort, "#2");
            Assert.AreEqual (m.MaxRequests, decoded.MaxRequests, "#3");
            Assert.AreEqual (m.Version, decoded.Version, "#4");
            Assert.AreEqual (m.Supports.Count, decoded.Supports.Count, "#5");
            m.Supports.ForEach (delegate (ExtensionSupport s) { Assert.IsTrue (decoded.Supports.Contains (s), "#6:" + s); });
        }

        [Test]
        public void LTChatDecodeTest ()
        {
            LTChat m = new LTChat (LTChat.Support.MessageId, "This Is My Message");

            byte[] data = m.Encode ();
            LTChat decoded = (LTChat) PeerMessage.DecodeMessage (data, 0, data.Length, null);

            Assert.AreEqual (m.Message, decoded.Message, "#1");
        }

        [Test]
        public void PeerExchangeMessageDecode ()
        {
            // Decodes as: 192.168.0.1:100
            byte[] peer = { 192, 168, 0, 1, 100, 0 };
            byte[] supports = { 1 | 2 }; // 1 == encryption, 2 == seeder

            byte id = PeerExchangeMessage.Support.MessageId;
            PeerExchangeMessage message = new PeerExchangeMessage (id, peer, supports, null);

            byte[] buffer = message.Encode ();
            PeerExchangeMessage m = (PeerExchangeMessage) PeerMessage.DecodeMessage (buffer, 0, buffer.Length, null);
            Assert.IsTrue (Toolbox.ByteMatch (peer, m.Added), "#1");
            Assert.IsTrue (Toolbox.ByteMatch (supports, m.AddedDotF), "#1");
        }

        [Test]
        public void PeerExchangeMessageDecode_Empty ()
        {
            var data = new BEncodedDictionary ().Encode ();
            var message = new PeerExchangeMessage ();
            message.Decode (data, 0, data.Length);
            Assert.IsEmpty (message.Added, "#1");
            Assert.IsEmpty (message.AddedDotF, "#2");
            Assert.IsEmpty (message.Dropped, "#3");
        }
    }
}
