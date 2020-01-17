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


using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client.Encryption
{
    [TestFixture]
    public class EncryptorFactoryTests
    {
        ConnectionPair pair;
        IConnection2 Incoming => pair.Incoming;
        IConnection2 Outgoing => pair.Outgoing;

        InfoHash InfoHash;
        InfoHash[] SKeys;

        BEncodedString IncomingId;
        BEncodedString OutgoingId;

        [SetUp]
        public void Setup ()
        {
            pair = new ConnectionPair ().WithTimeout ();

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
            => await Handshake (EncryptionTypes.PlainText, EncryptionTypes.PlainText, false);
        [Test]
        public async Task PlainText_PlainText_WithInitialData ()
            => await Handshake (EncryptionTypes.PlainText, EncryptionTypes.PlainText, true);

        [Test]
        public void PlainText_RC4Full ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.PlainText, EncryptionTypes.RC4Full, false));
        [Test]
        public void PlainText_RC4Full_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.PlainText, EncryptionTypes.RC4Full, true));

        [Test]
        public void PlainText_RC4Header ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.PlainText, EncryptionTypes.RC4Header, false));
        [Test]
        public void PlainText_RC4Header_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.PlainText, EncryptionTypes.RC4Header, true));

        [Test]
        public void RC4Full_PlainText ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Full, EncryptionTypes.PlainText, false));
        [Test]
        public void RC4Full_PlainText_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Full, EncryptionTypes.PlainText, true));

        [Test]
        public async Task RC4Full_RC4Full ()
            => await Handshake (EncryptionTypes.RC4Full, EncryptionTypes.RC4Full, false);
        [Test]
        public async Task RC4Full_RC4Full_WithInitialData ()
            => await Handshake (EncryptionTypes.RC4Full, EncryptionTypes.RC4Full, true);

        [Test]
        public void RC4Full_RC4Header ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, false));
        [Test]
        public void RC4Full_RC4Header_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, true));

        [Test]
        public void RC4Header_PlainText ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Header, EncryptionTypes.PlainText, false));
        [Test]
        public void RC4Header_PlainText_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Header, EncryptionTypes.PlainText, true));

        [Test]
        public void RC4Header_RC4Full ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Header, EncryptionTypes.RC4Full, false));
        [Test]
        public void RC4Header_RC4Full_WithInitialData ()
            => Assert.ThrowsAsync<EncryptionException> (() => Handshake (EncryptionTypes.RC4Header, EncryptionTypes.RC4Full, true));

        [Test]
        public async Task RC4Header_RC4Header ()
            => await Handshake (EncryptionTypes.RC4Header, EncryptionTypes.RC4Header, false);
        [Test]
        public async Task RC4Header_RC4Header_WithInitialData ()
            => await Handshake (EncryptionTypes.RC4Header, EncryptionTypes.RC4Header, true);

        async Task Handshake (EncryptionTypes outgoingEncryption, EncryptionTypes incomingEncryption, bool appendInitialPayload)
        {
            var handshakeIn = new HandshakeMessage (InfoHash, IncomingId, VersionInfo.ProtocolStringV100);
            var handshakeOut = new HandshakeMessage (InfoHash, OutgoingId, VersionInfo.ProtocolStringV100);

            var incomingTask = EncryptorFactory.CheckIncomingConnectionAsync (Incoming, incomingEncryption, new EngineSettings (), SKeys);
            var outgoingTask = EncryptorFactory.CheckOutgoingConnectionAsync (Outgoing, outgoingEncryption, new EngineSettings (), InfoHash, appendInitialPayload ? handshakeOut : null);

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

            // Make sure we got the crypto we asked for.
            if (incomingEncryption == EncryptionTypes.PlainText)
                Assert.IsInstanceOf<PlainTextEncryption> (incomingCrypto.Decryptor, "#2");
            else if (incomingEncryption == EncryptionTypes.RC4Full)
                Assert.IsInstanceOf<RC4> (incomingCrypto.Decryptor, "#3");
            else if (incomingEncryption == EncryptionTypes.RC4Header)
                Assert.IsInstanceOf<RC4Header> (incomingCrypto.Decryptor, "#4");

            if (outgoingEncryption == EncryptionTypes.PlainText)
                Assert.IsInstanceOf<PlainTextEncryption> (outgoingCrypto.Decryptor, "#5");
            else if (outgoingEncryption == EncryptionTypes.RC4Full)
                Assert.IsInstanceOf<RC4> (outgoingCrypto.Decryptor, "#6");
            else if (outgoingEncryption == EncryptionTypes.RC4Header)
                Assert.IsInstanceOf<RC4Header> (outgoingCrypto.Decryptor, "#7");
        }
    }
}
