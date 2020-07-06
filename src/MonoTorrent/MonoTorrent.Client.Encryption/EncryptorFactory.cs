//
// EncryptorFactory.cs
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
using System.Diagnostics;
using System.Threading;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;

using ReusableTasks;

namespace MonoTorrent.Client.Encryption
{
    static class EncryptorFactory
    {
        internal struct EncryptorResult
        {
            public IEncryption Decryptor { get; }
            public IEncryption Encryptor { get; }
            public HandshakeMessage Handshake { get; }

            public EncryptorResult (IEncryption decryptor, IEncryption encryptor, HandshakeMessage handshake)
            {
                Decryptor = decryptor;
                Encryptor = encryptor;
                Handshake = handshake;
            }
        }

        static TimeSpan Timeout => Debugger.IsAttached ? TimeSpan.FromHours (1) : TimeSpan.FromSeconds (10);

        internal static async ReusableTask<EncryptorResult> CheckIncomingConnectionAsync (IConnection connection, EncryptionTypes encryption, EngineSettings settings, InfoHash[] sKeys)
        {
            if (!connection.IsIncoming)
                throw new Exception ("oops");

            using var cts = new CancellationTokenSource (Timeout);
            using CancellationTokenRegistration registration = cts.Token.Register (connection.Dispose);
            return await DoCheckIncomingConnectionAsync (connection, encryption, settings, sKeys).ConfigureAwait (false);
        }

        static async ReusableTask<EncryptorResult> DoCheckIncomingConnectionAsync (IConnection connection, EncryptionTypes encryption, EngineSettings settings, InfoHash[] sKeys)
        {
            EncryptionTypes allowedEncryption = (settings?.AllowedEncryption ?? EncryptionTypes.All) & encryption;
            bool supportsRC4Header = allowedEncryption.HasFlag (EncryptionTypes.RC4Header);
            bool supportsRC4Full = allowedEncryption.HasFlag (EncryptionTypes.RC4Full);
            bool supportsPlainText = allowedEncryption.HasFlag (EncryptionTypes.PlainText);

            // If the connection is incoming, receive the handshake before
            // trying to decide what encryption to use

            var message = new HandshakeMessage ();
            using (NetworkIO.BufferPool.Rent (HandshakeMessage.HandshakeLength, out ByteBuffer buffer)) {
                await NetworkIO.ReceiveAsync (connection, buffer, 0, HandshakeMessage.HandshakeLength, null, null, null).ConfigureAwait (false);
                message.Decode (buffer.Data, 0, HandshakeMessage.HandshakeLength);

                if (message.ProtocolString == VersionInfo.ProtocolStringV100) {
                    if (supportsPlainText)
                        return new EncryptorResult (PlainTextEncryption.Instance, PlainTextEncryption.Instance, message);
                } else if (supportsRC4Header || supportsRC4Full) {
                    // The data we just received was part of an encrypted handshake and was *not* the BitTorrent handshake
                    // First switch to the threadpool as creating encrypted sockets runs expensive computations in the ctor
                    await MainLoop.SwitchToThreadpool ();

                    var encSocket = new PeerBEncryption (sKeys, EncryptionTypes.All);
                    await encSocket.HandshakeAsync (connection, buffer.Data, 0, HandshakeMessage.HandshakeLength).ConfigureAwait (false);
                    if (encSocket.Decryptor is RC4Header && !supportsRC4Header)
                        throw new EncryptionException ("Decryptor was RC4Header but that is not allowed");
                    if (encSocket.Decryptor is RC4 && !supportsRC4Full)
                        throw new EncryptionException ("Decryptor was RC4Full but that is not allowed");

                    // As the connection was encrypted, the data we got from the initial Receive call will have
                    // been consumed during the crypto handshake process. Now that the encrypted handshake has
                    // been established, we should ensure we read the data again.
                    byte[] data = encSocket.InitialData?.Length > 0 ? encSocket.InitialData : null;
                    if (data == null) {
                        await NetworkIO.ReceiveAsync (connection, buffer, 0, HandshakeMessage.HandshakeLength, null, null, null).ConfigureAwait (false);
                        data = buffer.Data;
                        encSocket.Decryptor.Decrypt (data, 0, HandshakeMessage.HandshakeLength);
                    }
                    message.Decode (data, 0, HandshakeMessage.HandshakeLength);
                    if (message.ProtocolString == VersionInfo.ProtocolStringV100)
                        return new EncryptorResult (encSocket.Decryptor, encSocket.Encryptor, message);
                }
            }

            connection.Dispose ();
            throw new EncryptionException ("Invalid handshake received and no decryption works");
        }

        internal static async ReusableTask<EncryptorResult> CheckOutgoingConnectionAsync (IConnection connection, EncryptionTypes encryption, EngineSettings settings, InfoHash infoHash, HandshakeMessage handshake = null)
        {
            if (connection.IsIncoming)
                throw new Exception ("oops");

            using var cts = new CancellationTokenSource (Timeout);
            using CancellationTokenRegistration registration = cts.Token.Register (connection.Dispose);
            return await DoCheckOutgoingConnectionAsync (connection, encryption, settings, infoHash, handshake).ConfigureAwait (false);
        }

        static async ReusableTask<EncryptorResult> DoCheckOutgoingConnectionAsync (IConnection connection, EncryptionTypes encryption, EngineSettings settings, InfoHash infoHash, HandshakeMessage handshake)
        {
            EncryptionTypes allowedEncryption = settings.AllowedEncryption & encryption;
            bool supportsRC4Header = allowedEncryption.HasFlag (EncryptionTypes.RC4Header);
            bool supportsRC4Full = allowedEncryption.HasFlag (EncryptionTypes.RC4Full);
            bool supportsPlainText = allowedEncryption.HasFlag (EncryptionTypes.PlainText);

            // First switch to the threadpool as creating encrypted sockets runs expensive computations in the ctor
            await MainLoop.SwitchToThreadpool ();
            if ((settings.PreferEncryption || !supportsPlainText) && (supportsRC4Header || supportsRC4Full)) {
                var encSocket = new PeerAEncryption (infoHash, allowedEncryption, handshake?.Encode ());
                await encSocket.HandshakeAsync (connection).ConfigureAwait (false);
                if (encSocket.Decryptor is RC4Header && !supportsRC4Header)
                    throw new EncryptionException ("Decryptor was RC4Header but that is not allowed");
                if (encSocket.Decryptor is RC4 && !supportsRC4Full)
                    throw new EncryptionException ("Decryptor was RC4Full but that is not allowed");

                return new EncryptorResult (encSocket.Decryptor, encSocket.Encryptor, null);
            } else if (supportsPlainText) {
                if (handshake != null) {
                    int length = handshake.ByteLength;
                    using (NetworkIO.BufferPool.Rent (length, out ByteBuffer buffer)) {
                        handshake.Encode (buffer.Data, 0);
                        await NetworkIO.SendAsync (connection, buffer, 0, length, null, null, null).ConfigureAwait (false);
                    }
                }
                return new EncryptorResult (PlainTextEncryption.Instance, PlainTextEncryption.Instance, null);
            }

            connection.Dispose ();
            throw new EncryptionException ("Invalid handshake received and no decryption works");
        }
    }
}
