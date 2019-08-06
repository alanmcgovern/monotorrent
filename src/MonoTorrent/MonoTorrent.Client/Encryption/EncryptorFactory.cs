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
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.Threading;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;
using System.Threading.Tasks;

namespace MonoTorrent.Client.Encryption
{
    static class EncryptorFactory
    {
        internal static async Task<byte[]> CheckEncryptionAsync(PeerId id, int bytesToReceive, InfoHash[] sKeys)
        {
            using (var cts = new CancellationTokenSource (TimeSpan.FromSeconds (1000)))
            using (var registration = cts.Token.Register (id.Connection.Dispose))
                return await CheckEncryptionAsync (id, bytesToReceive, sKeys, cts.Token);
        }

        static async Task<byte[]> CheckEncryptionAsync(PeerId id, int bytesToReceive, InfoHash[] sKeys, CancellationToken token)
        {
            IConnection connection = id.Connection;
            var allowedEncryption = (id.Engine?.Settings.AllowedEncryption ?? EncryptionTypes.All) & id.Peer.Encryption;
            var supportsRC4Header = allowedEncryption.HasFlag (EncryptionTypes.RC4Header);
            var supportsRC4Full = allowedEncryption.HasFlag (EncryptionTypes.RC4Full);
            var supportsPlainText = allowedEncryption.HasFlag (EncryptionTypes.PlainText);

            // If the connection is incoming, receive the handshake before
            // trying to decide what encryption to use
            if (connection.IsIncoming)
            {
                var buffer = new byte[bytesToReceive];
                await NetworkIO.ReceiveAsync(connection, buffer, 0, bytesToReceive, null, null, null).ConfigureAwait (false);

                HandshakeMessage message = new HandshakeMessage();
                message.Decode(buffer, 0, buffer.Length);

                if (message.ProtocolString == VersionInfo.ProtocolStringV100) {
                    if (supportsPlainText) {
                        id.Encryptor = id.Decryptor = PlainTextEncryption.Instance;
                        return buffer;
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

                    id.Decryptor = encSocket.Decryptor;
                    id.Encryptor = encSocket.Encryptor;
                    return encSocket.InitialData?.Length > 0 ? encSocket.InitialData : null;
                }
            }
            else
            {
                if ((id.Engine.Settings.PreferEncryption || !supportsPlainText) && (supportsRC4Header || supportsRC4Full)) {
                    var encSocket = new PeerAEncryption(id.TorrentManager.InfoHash, allowedEncryption);
                    await encSocket.HandshakeAsync(connection);
                    if (encSocket.Decryptor is RC4Header && !supportsRC4Header)
                        throw new EncryptionException("Decryptor was RC4Header but that is not allowed");
                    if (encSocket.Decryptor is RC4 && !supportsRC4Full)
                        throw new EncryptionException("Decryptor was RC4Full but that is not allowed");

                    id.Decryptor = encSocket.Decryptor;
                    id.Encryptor = encSocket.Encryptor;
                    return encSocket.InitialData?.Length > 0 ? encSocket.InitialData : null;
                }
                else if (supportsPlainText)
                {
                    id.Encryptor = id.Decryptor = PlainTextEncryption.Instance;
                    return null;
                }
            }

            throw new EncryptionException("Invalid handshake received and no decryption works");
        }
    }
}
