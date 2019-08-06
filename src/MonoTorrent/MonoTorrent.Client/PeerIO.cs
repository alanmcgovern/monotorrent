//
// PeerIO.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2010 Alan McGovern
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
using System.Net;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    static class PeerIO
    {
        const int MaxMessageLength = Piece.BlockSize * 4;

        public static async Task<HandshakeMessage> ReceiveHandshakeAsync (IConnection connection, IEncryption decryptor)
        {
            var buffer = ClientEngine.BufferManager.GetBuffer (HandshakeMessage.HandshakeLength);
            try {
                await NetworkIO.ReceiveAsync (connection, buffer, 0, HandshakeMessage.HandshakeLength, null, null, null).ConfigureAwait (false);

                decryptor.Decrypt (buffer, 0, HandshakeMessage.HandshakeLength);

                var message = new HandshakeMessage ();
                message.Decode (buffer, 0, HandshakeMessage.HandshakeLength);
                return message;
            } finally {
                ClientEngine.BufferManager.FreeBuffer (ref buffer);
            }
        }

        public static async Task<PeerMessage> ReceiveMessageAsync (IConnection connection, IEncryption decryptor, IRateLimiter rateLimiter, ConnectionMonitor monitor, TorrentManager manager)
        {
            var messageLengthBuffer = BufferManager.EmptyBuffer;
            var messageBuffer = BufferManager.EmptyBuffer;

            try {
                int messageLength = 4;
                ClientEngine.BufferManager.GetBuffer (ref messageLengthBuffer, messageLength);
                await NetworkIO.ReceiveAsync (connection, messageLengthBuffer, 0, messageLength, rateLimiter, monitor?.ProtocolDown, manager?.Monitor.ProtocolDown).ConfigureAwait (false);

                decryptor.Decrypt (messageLengthBuffer, 0, messageLength);

                int messageBody = IPAddress.HostToNetworkOrder (BitConverter.ToInt32 (messageLengthBuffer, 0)); ;
                if (messageBody < 0 || messageBody > MaxMessageLength)
                    throw new Exception ($"Invalid message length received. Value was '{messageBody}'");

                if (messageBody == 0)
                    return new KeepAliveMessage ();

                ClientEngine.BufferManager.GetBuffer (ref messageBuffer, messageBody + messageLength);
                Buffer.BlockCopy (messageLengthBuffer, 0, messageBuffer, 0, messageLength);
                ClientEngine.BufferManager.FreeBuffer (ref messageLengthBuffer);

                // Always assume protocol first, then convert to data when we what message it is!
                await NetworkIO.ReceiveAsync (connection, messageBuffer, messageLength, messageBody, rateLimiter, monitor?.ProtocolDown, manager?.Monitor.ProtocolDown).ConfigureAwait (false);

                decryptor.Decrypt (messageBuffer, messageLength, messageBody);
                var data = PeerMessage.DecodeMessage (messageBuffer, 0, messageLength + messageBody, manager);
                if (data is PieceMessage msg)
                {
                    monitor?.ProtocolDown.AddDelta(-msg.RequestLength);
                    manager?.Monitor.ProtocolDown.AddDelta(-msg.RequestLength);

                    monitor?.DataDown.AddDelta(msg.RequestLength);
                    manager?.Monitor.DataDown.AddDelta(msg.RequestLength);
                }
                return data;
            } finally {
                ClientEngine.BufferManager.FreeBuffer (ref messageLengthBuffer);
                ClientEngine.BufferManager.FreeBuffer (ref messageBuffer);
            }
        }

        public static async Task SendMessageAsync (IConnection connection, IEncryption encryptor, PeerMessage message, IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor)
        {
            int count = message.ByteLength;
            var buffer = ClientEngine.BufferManager.GetBuffer (count);

            try {
                var pieceMessage = message as PieceMessage;
                message.Encode (buffer, 0);
                encryptor.Encrypt (buffer, 0, count);

                // Assume protocol first, then swap it to data once we successfully send the data bytes.
                await NetworkIO.SendAsync (connection, buffer, 0, count, pieceMessage == null ? null : rateLimiter, peerMonitor?.DataUp, managerMonitor?.DataUp).ConfigureAwait (false);
                if (pieceMessage != null)
                {
                    peerMonitor?.ProtocolUp.AddDelta(-pieceMessage.RequestLength);
                    managerMonitor?.ProtocolUp.AddDelta(-pieceMessage.RequestLength);

                    peerMonitor?.DataUp.AddDelta(pieceMessage.RequestLength);
                    managerMonitor?.DataUp.AddDelta(pieceMessage.RequestLength);
                }
            } finally {
                ClientEngine.BufferManager.FreeBuffer (ref buffer);
            }
        }
    }
}
