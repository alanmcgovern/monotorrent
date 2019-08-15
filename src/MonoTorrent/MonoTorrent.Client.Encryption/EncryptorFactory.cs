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
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Encryption
{
    static class EncryptorFactory
    {
        internal struct EncryptorResult
        {
            public IEncryption Decryptor { get; }
            public IEncryption Encryptor { get; }
            public byte [] InitialData { get; }

            public EncryptorResult (IEncryption decryptor, IEncryption encryptor, byte [] data)
            {
                Decryptor = decryptor;
                Encryptor = encryptor;
                InitialData = data;
            }
        }

        static TimeSpan Timeout => Debugger.IsAttached ? TimeSpan.FromHours (1) : TimeSpan.FromSeconds (10);

        internal static async Task<EncryptorResult> CheckIncomingConnectionAsync (IConnection connection, EncryptionTypes encryption, EngineSettings settings, int bytesToReceive, InfoHash[] sKeys)
        {
            if (!connection.IsIncoming)
                throw new Exception ("oops");

            using (var cts = new CancellationTokenSource (Timeout))
            using (var registration = cts.Token.Register (connection.Dispose))
                return await DoCheckIncomingConnectionAsync (connection, encryption, settings, bytesToReceive, sKeys);
        }

        static async Task<EncryptorResult> DoCheckIncomingConnectionAsync(IConnection connection, EncryptionTypes encryption, EngineSettings settings, int bytesToReceive, InfoHash[] sKeys)
        {
            var allowedEncryption = (settings?.AllowedEncryption ?? EncryptionTypes.All) & encryption;
            var supportsRC4Header = allowedEncryption.HasFlag (EncryptionTypes.RC4Header);
            var supportsRC4Full = allowedEncryption.HasFlag (EncryptionTypes.RC4Full);
            var supportsPlainText = allowedEncryption.HasFlag (EncryptionTypes.PlainText);

            // If the connection is incoming, receive the handshake before
            // trying to decide what encryption to use

            var buffer = new byte[bytesToReceive];
            await NetworkIO.ReceiveAsync(connection, buffer, 0, bytesToReceive, null, null, null).ConfigureAwait (false);

            HandshakeMessage message = new HandshakeMessage();
            message.Decode(buffer, 0, buffer.Length);

            if (message.ProtocolString == VersionInfo.ProtocolStringV100) {
                if (supportsPlainText) {
                    return new EncryptorResult (PlainTextEncryption.Instance, PlainTextEncryption.Instance, buffer);
                }
            }
            else if (supportsRC4Header || supportsRC4Full)
            {
                // The data we just received was part of an encrypted handshake and was *not* the BitTorrent handshake
                var encSocket = new PeerBEncryption(sKeys, EncryptionTypes.All);
                await encSocket.HandshakeAsync(connection, buffer, 0, buffer.Length);
                if (encSocket.Decryptor is RC4Header && !supportsRC4Header)
                    throw new EncryptionException("Decryptor was RC4Header but that is not allowed");
                if (encSocket.Decryptor is RC4 && !supportsRC4Full)
                    throw new EncryptionException("Decryptor was RC4Full but that is not allowed");

                var data = encSocket.InitialData?.Length > 0 ? encSocket.InitialData : null;
                return new EncryptorResult (encSocket.Decryptor, encSocket.Encryptor, data);
            }

            throw new EncryptionException("Invalid handshake received and no decryption works");
        }

        internal static async Task<EncryptorResult> CheckOutgoingConnectionAsync(IConnection connection, EncryptionTypes encryption, EngineSettings settings, InfoHash infoHash)
        {
            if (connection.IsIncoming)
                throw new Exception ("oops");

            using (var cts = new CancellationTokenSource (Timeout))
            using (var registration = cts.Token.Register (connection.Dispose))
                return await DoCheckOutgoingConnectionAsync (connection, encryption, settings, infoHash);
        }

        static async Task<EncryptorResult> DoCheckOutgoingConnectionAsync(IConnection connection, EncryptionTypes encryption, EngineSettings settings, InfoHash infoHash)
        {
            var allowedEncryption = settings.AllowedEncryption & encryption;
            var supportsRC4Header = allowedEncryption.HasFlag (EncryptionTypes.RC4Header);
            var supportsRC4Full = allowedEncryption.HasFlag (EncryptionTypes.RC4Full);
            var supportsPlainText = allowedEncryption.HasFlag (EncryptionTypes.PlainText);

            if ((settings.PreferEncryption || !supportsPlainText) && (supportsRC4Header || supportsRC4Full)) {
                var encSocket = new PeerAEncryption(infoHash, allowedEncryption);
                await encSocket.HandshakeAsync(connection);
                if (encSocket.Decryptor is RC4Header && !supportsRC4Header)
                    throw new EncryptionException("Decryptor was RC4Header but that is not allowed");
                if (encSocket.Decryptor is RC4 && !supportsRC4Full)
                    throw new EncryptionException("Decryptor was RC4Full but that is not allowed");

                var data = encSocket.InitialData?.Length > 0 ? encSocket.InitialData : null;
                return new EncryptorResult (encSocket.Decryptor, encSocket.Encryptor, data);
            }
            else if (supportsPlainText)
            {
                return new EncryptorResult (PlainTextEncryption.Instance, PlainTextEncryption.Instance, null);
            }
            throw new EncryptionException("Invalid handshake received and no decryption works");
        }
    }
}
