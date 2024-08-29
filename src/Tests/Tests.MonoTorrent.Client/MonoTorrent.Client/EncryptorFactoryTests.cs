//
// EncryptorFactoryTests.cs
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
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages.Peer;

using NUnit.Framework;

namespace MonoTorrent.Client.Encryption
{
    [TestFixture]
    public class EncryptorFactoryTests
    {
        ConnectionPair pair;
        IPeerConnection Incoming => pair.Incoming;
        IPeerConnection Outgoing => pair.Outgoing;

        InfoHash InfoHash;
        InfoHash[] SKeys;

        BEncodedString IncomingId;
        BEncodedString OutgoingId;

        [SetUp]
        public void Setup ()
        {
            pair = new ConnectionPair ().DisposeAfterTimeout ();

            InfoHash = new InfoHash (Enumerable.Repeat ((byte) 255, 20).ToArray ());
            SKeys = new[] {
                new InfoHash (Enumerable.Repeat ((byte)254, 20).ToArray ()),
                new InfoHash (Enumerable.Repeat ((byte)253, 20).ToArray ()),
                InfoHash,
                new InfoHash (Enumerable.Repeat ((byte)252, 20).ToArray ())
            };

            IncomingId = new BEncodedString (Enumerable.Repeat ((byte) '0', 20).ToArray ());
            OutgoingId = new BEncodedString (Enumerable.Repeat ((byte) '1', 20).ToArray ());
        }

        [TearDown]
        public void Teardown ()
        {
            pair.Dispose ();
        }

        [Test]
        public async Task PlainText_PlainText ()
            => await Handshake (EncryptionType.PlainText, EncryptionType.PlainText, false);
        [Test]
        public async Task PlainText_PlainText_WithInitialData ()
            => await Handshake (EncryptionType.PlainText, EncryptionType.PlainText, true);

        [Test]
        public void PlainText_RC4Full ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.PlainText, EncryptionType.RC4Full, false));
        [Test]
        public void PlainText_RC4Full_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.PlainText, EncryptionType.RC4Full, true));

        [Test]
        public void PlainText_RC4Header ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.PlainText, EncryptionType.RC4Header, false));
        [Test]
        public void PlainText_RC4Header_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.PlainText, EncryptionType.RC4Header, true));

        [Test]
        public void RC4Full_PlainText ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Full, EncryptionType.PlainText, false));
        [Test]
        public void RC4Full_PlainText_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Full, EncryptionType.PlainText, true));

        [Test]
        public async Task RC4Full_RC4Full ()
            => await Handshake (EncryptionType.RC4Full, EncryptionType.RC4Full, false);
        [Test]
        public async Task RC4Full_RC4Full_WithInitialData ()
            => await Handshake (EncryptionType.RC4Full, EncryptionType.RC4Full, true);

        [Test]
        public void RC4Full_RC4Header ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Full, EncryptionType.RC4Header, false));
        [Test]
        public void RC4Full_RC4Header_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Full, EncryptionType.RC4Header, true));

        [Test]
        public void RC4Header_PlainText ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Header, EncryptionType.PlainText, false));
        [Test]
        public void RC4Header_PlainText_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Header, EncryptionType.PlainText, true));

        [Test]
        public void RC4Header_RC4Full ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Header, EncryptionType.RC4Full, false));
        [Test]
        public void RC4Header_RC4Full_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionType.RC4Header, EncryptionType.RC4Full, true));

        [Test]
        public async Task RC4Header_RC4Header ()
            => await Handshake (EncryptionType.RC4Header, EncryptionType.RC4Header, false);
        [Test]
        public async Task RC4Header_RC4Header_WithInitialData ()
            => await Handshake (EncryptionType.RC4Header, EncryptionType.RC4Header, true);

        [Test]
        public async Task PreferHeader_AllowAny_1 ()
            => await Handshake (new[] { EncryptionType.RC4Full, EncryptionType.RC4Header }, new[] { EncryptionType.RC4Header }, true);
        [Test]
        public async Task PreferHeader_AllowAny_2 ()
            => await Handshake (new[] { EncryptionType.RC4Full, EncryptionType.RC4Header }, new[] { EncryptionType.RC4Header, EncryptionType.RC4Full }, true);
        [Test]
        public async Task PreferHeader_AllowAny_3 ()
            => await Handshake (new[] { EncryptionType.RC4Header, EncryptionType.RC4Full }, new[] { EncryptionType.RC4Header }, true);
        [Test]
        public async Task PreferHeader_AllowAny_4 ()
            => await Handshake (new[] { EncryptionType.RC4Header, EncryptionType.RC4Full }, new[] { EncryptionType.RC4Header, EncryptionType.RC4Full }, true);

        [Test]
        public async Task PreferPlainText_AllowAny ()
            => await Handshake (new[] { EncryptionType.PlainText, EncryptionType.RC4Header, EncryptionType.RC4Full }, EncryptionTypes.All, true);

        async Task Handshake (EncryptionType outgoingEncryption, EncryptionType incomingEncryption, bool appendInitialPayload)
            => await Handshake (new[] { outgoingEncryption }, new[] { incomingEncryption }, appendInitialPayload);

        async Task Handshake (IList<EncryptionType> outgoingEncryption, IList<EncryptionType> incomingEncryption, bool appendInitialPayload)
        {
            var handshakeIn = new HandshakeMessage (InfoHash, IncomingId, Constants.ProtocolStringV100);
            var handshakeOut = new HandshakeMessage (InfoHash, OutgoingId, Constants.ProtocolStringV100);

            var incomingTask = EncryptorFactory.CheckIncomingConnectionAsync (Incoming, incomingEncryption, SKeys, Factories.Default, TaskExtensions.Timeout);
            var outgoingTask = EncryptorFactory.CheckOutgoingConnectionAsync (Outgoing, outgoingEncryption, InfoHash, appendInitialPayload ? handshakeOut : null, Factories.Default, TaskExtensions.Timeout);

            // If the handshake was not part of the initial payload, send it now.
            var outgoingCrypto = await outgoingTask;
            if (!appendInitialPayload)
                await PeerIO.SendMessageAsync (Outgoing, outgoingCrypto.Encryptor, handshakeOut, null, null, null);

            // Receive the handshake and make sure it decrypted correctly.
            var incomingCrypto = await incomingTask;
            Assert.AreEqual (OutgoingId, incomingCrypto.Handshake.PeerId, "#1a");

            // Send the other handshake.
            await PeerIO.SendMessageAsync (Incoming, incomingCrypto.Encryptor, handshakeIn, null, null, null);

            // Receive the other handshake and make sure it decrypted ok on the other side.
            handshakeIn = await PeerIO.ReceiveHandshakeAsync (Outgoing, outgoingCrypto.Decryptor);
            Assert.AreEqual (IncomingId, handshakeIn.PeerId, "#1b");

            if (outgoingEncryption[0] == EncryptionType.PlainText) {
                // If the outgoing encryption is plain text, then the whole thing is plain text
                Assert.IsInstanceOf<PlainTextEncryption> (incomingCrypto.Decryptor, "#2a");
                Assert.IsInstanceOf<PlainTextEncryption> (outgoingCrypto.Decryptor, "#2b");
            } else {
                EncryptionType expected;

                if (outgoingEncryption.Contains (EncryptionType.RC4Full) && outgoingEncryption.Contains (EncryptionType.RC4Header)) {
                    // If the outgoing encryption supports both RC4Full and RC4Header, the final type is determined by
                    // the priority associated with the 'Incoming' connection as it will decide between Header or Full if
                    // the Outgoing connection asks for both.
                    expected = EncryptionTypes.PreferredRC4 (incomingEncryption).Value;
                } else {
                    // Otherwise the outgoing encryption will specify a single RC4 type, so the incoming connection
                    // will either accept it or close the connection.
                    expected = EncryptionTypes.PreferredRC4 (outgoingEncryption).Value;
                }

                if (expected == EncryptionType.RC4Full) {
                    Assert.IsInstanceOf<RC4> (incomingCrypto.Decryptor, "#3a");
                    Assert.IsInstanceOf<RC4> (outgoingCrypto.Decryptor, "#3b");
                } else if (expected == EncryptionType.RC4Header) {
                    Assert.IsInstanceOf<RC4Header> (incomingCrypto.Decryptor, "#4a");
                    Assert.IsInstanceOf<RC4Header> (outgoingCrypto.Decryptor, "#4b");
                } else {
                    throw new NotSupportedException ();
                }
            }
        }
    }
}
