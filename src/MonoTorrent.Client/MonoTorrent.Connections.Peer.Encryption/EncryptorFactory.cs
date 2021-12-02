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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using MonoTorrent.Client;
using MonoTorrent.Messages.Peer;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer.Encryption
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

        internal static async ReusableTask<EncryptorResult> CheckIncomingConnectionAsync (IPeerConnection connection, IList<EncryptionType> allowedEncryption, InfoHash[] sKeys, Factories factories)
        {
            if (!connection.IsIncoming)
                throw new Exception ("oops");

            using var cts = new CancellationTokenSource (Timeout);
            using CancellationTokenRegistration registration = cts.Token.Register (connection.Dispose);
            return await DoCheckIncomingConnectionAsync (connection, allowedEncryption, sKeys, factories).ConfigureAwait (false);
        }

        static async ReusableTask<EncryptorResult> DoCheckIncomingConnectionAsync (IPeerConnection connection, IList<EncryptionType> preferredEncryption, InfoHash[] sKeys, Factories factories)
        {
            bool supportsRC4Header = preferredEncryption.Contains (EncryptionType.RC4Header);
            bool supportsRC4Full = preferredEncryption.Contains (EncryptionType.RC4Full);
            bool supportsPlainText = preferredEncryption.Contains (EncryptionType.PlainText);

            // If the connection is incoming, receive the handshake before
            // trying to decide what encryption to use

            var message = new HandshakeMessage ();
            using (NetworkIO.BufferPool.Rent (HandshakeMessage.HandshakeLength, out SocketMemory buffer)) {
                await NetworkIO.ReceiveAsync (connection, buffer, null, null, null).ConfigureAwait (false);
                message.Decode (buffer.AsSpan ());

                if (message.ProtocolString == Constants.ProtocolStringV100) {
                    if (supportsPlainText)
                        return new EncryptorResult (PlainTextEncryption.Instance, PlainTextEncryption.Instance, message);
                } else if (supportsRC4Header || supportsRC4Full) {
                    // The data we just received was part of an encrypted handshake and was *not* the BitTorrent handshake
                    // First switch to the threadpool as creating encrypted sockets runs expensive computations in the ctor
                    await MainLoop.SwitchToThreadpool ();

                    using var encSocket = new PeerBEncryption (factories, sKeys, preferredEncryption);
                    await encSocket.HandshakeAsync (connection, buffer.Memory).ConfigureAwait (false);
                    if (encSocket.Decryptor is RC4Header && !supportsRC4Header)
                        throw new EncryptionException ("Decryptor was RC4Header but that is not allowed");
                    if (encSocket.Decryptor is RC4 && !supportsRC4Full)
                        throw new EncryptionException ("Decryptor was RC4Full but that is not allowed");

                    // As the connection was encrypted, the data we got from the initial Receive call will have
                    // been consumed during the crypto handshake process. Now that the encrypted handshake has
                    // been established, we should ensure we read the data again.
                    Memory<byte> data = encSocket.InitialData?.Length > 0 ? encSocket.InitialData : default;
                    if (data.IsEmpty) {
                        await NetworkIO.ReceiveAsync (connection, buffer, null, null, null).ConfigureAwait (false);
                        encSocket.Decryptor.Decrypt (buffer.AsSpan ());
                        data = buffer.Memory;
                    }
                    message.Decode (data.Span);
                    if (message.ProtocolString == Constants.ProtocolStringV100)
                        return new EncryptorResult (encSocket.Decryptor, encSocket.Encryptor, message);
                }
            }

            connection.Dispose ();
            throw new EncryptionException ("Invalid handshake received and no decryption works");
        }

        internal static async ReusableTask<EncryptorResult> CheckOutgoingConnectionAsync (IPeerConnection connection, IList<EncryptionType> allowedEncryption, InfoHash infoHash, HandshakeMessage handshake, Factories factories)
        {
            if (connection.IsIncoming)
                throw new Exception ("oops");

            using var cts = new CancellationTokenSource (Timeout);
            using CancellationTokenRegistration registration = cts.Token.Register (connection.Dispose);
            return await DoCheckOutgoingConnectionAsync (connection, allowedEncryption, infoHash, handshake, factories).ConfigureAwait (false);
        }

        static async ReusableTask<EncryptorResult> DoCheckOutgoingConnectionAsync (IPeerConnection connection, IList<EncryptionType> preferredEncryption, InfoHash infoHash, HandshakeMessage handshake, Factories factories)
        {
            bool supportsRC4Header = preferredEncryption.Contains (EncryptionType.RC4Header);
            bool supportsRC4Full = preferredEncryption.Contains (EncryptionType.RC4Full);
            bool supportsPlainText = preferredEncryption.Contains (EncryptionType.PlainText);

            // First switch to the threadpool as creating encrypted sockets runs expensive computations in the ctor
            await MainLoop.SwitchToThreadpool ();
            if (preferredEncryption[0] != EncryptionType.PlainText) {
                using var encSocket = new PeerAEncryption (factories, infoHash, preferredEncryption, handshake?.Encode ());
                await encSocket.HandshakeAsync (connection).ConfigureAwait (false);
                if (encSocket.Decryptor is RC4Header && !supportsRC4Header)
                    throw new EncryptionException ("Decryptor was RC4Header but that is not allowed");
                if (encSocket.Decryptor is RC4 && !supportsRC4Full)
                    throw new EncryptionException ("Decryptor was RC4Full but that is not allowed");

                return new EncryptorResult (encSocket.Decryptor, encSocket.Encryptor, null);
            } else if (supportsPlainText) {
                if (handshake != null) {
                    int length = handshake.ByteLength;
                    using (NetworkIO.BufferPool.Rent (length, out SocketMemory buffer)) {
                        handshake.Encode (buffer.Span);
                        await NetworkIO.SendAsync (connection, buffer, null, null, null).ConfigureAwait (false);
                    }
                }
                return new EncryptorResult (PlainTextEncryption.Instance, PlainTextEncryption.Instance, null);
            }

            connection.Dispose ();
            throw new EncryptionException ("Invalid handshake received and no decryption works");
        }
    }
}
