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

using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;

using ReusableTasks;

namespace MonoTorrent.Client
{
    static class PeerIO
    {
        const int MaxMessageLength = Constants.BlockSize * 4;

        public static async ReusableTask<HandshakeMessage> ReceiveHandshakeAsync (IPeerConnection connection, IEncryption decryptor)
        {
            await MainLoop.SwitchToThreadpool ();

            using (NetworkIO.BufferPool.Rent (HandshakeMessage.HandshakeLength, out SocketMemory buffer)) {
                await NetworkIO.ReceiveAsync (connection, buffer, null, null, null).ConfigureAwait (false);

                decryptor.Decrypt (buffer.AsSpan ());

                var message = new HandshakeMessage ();
                message.Decode (buffer.AsSpan ());
                return message;
            }
        }

        public static ReusableTask<PeerMessage> ReceiveMessageAsync (IPeerConnection connection, IEncryption decryptor)
        {
            return ReceiveMessageAsync (connection, decryptor, null, null, null, null);
        }

        public static ReusableTask<PeerMessage> ReceiveMessageAsync (IPeerConnection connection, IEncryption decryptor, IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor, ITorrentData torrentData)
        {
            return ReceiveMessageAsync (connection, decryptor, rateLimiter, peerMonitor, managerMonitor, torrentData, default);
        }

        public static async ReusableTask<PeerMessage> ReceiveMessageAsync (IPeerConnection connection, IEncryption decryptor, IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor, ITorrentData torrentData, SocketMemory buffer)
        {
            await MainLoop.SwitchToThreadpool ();

            int messageHeaderLength = 4;
            int messageBodyLength;

            SocketMemory messageHeaderBuffer = buffer;
            SocketMemory messageBuffer = buffer;
            ByteBufferPool.Releaser messageBufferReleaser = default;

            using (var headerReleaser = messageHeaderBuffer.IsEmpty ? NetworkIO.BufferPool.Rent (messageHeaderLength, out messageHeaderBuffer) : default) {
                await NetworkIO.ReceiveAsync (connection, messageHeaderBuffer.Slice (0, messageHeaderLength), rateLimiter, peerMonitor?.ProtocolDown, managerMonitor?.ProtocolDown).ConfigureAwait (false);

                decryptor.Decrypt (messageHeaderBuffer.AsSpan (0, messageHeaderLength));

                messageBodyLength = Message.ReadInt (messageHeaderBuffer.AsSpan ());
                if (messageBodyLength < 0 || messageBodyLength > MaxMessageLength) {
                    connection.Dispose ();
                    throw new ProtocolException ($"Invalid message length received. Value was '{messageBodyLength}'");
                }

                if (messageBodyLength == 0)
                    return new KeepAliveMessage ();

                if (messageBuffer.IsEmpty || messageBuffer.Length < messageBodyLength + messageHeaderLength) {
                    messageBufferReleaser = NetworkIO.BufferPool.Rent (messageBodyLength + messageHeaderLength, out messageBuffer);
                    messageHeaderBuffer.Memory.Slice (0, messageHeaderLength).CopyTo (messageBuffer.Memory);
                }
            }

            using (messageBufferReleaser) {
                // Always assume protocol first, then convert to data when we what message it is!
                await NetworkIO.ReceiveAsync (connection, messageBuffer.Slice (messageHeaderLength, messageBodyLength), rateLimiter, peerMonitor?.ProtocolDown, managerMonitor?.ProtocolDown).ConfigureAwait (false);

                decryptor.Decrypt (messageBuffer.Span.Slice (messageHeaderLength, messageBodyLength));
                // FIXME: manager should never be null, except some of the unit tests do that.
                var data = PeerMessage.DecodeMessage (messageBuffer.Span.Slice (0, messageHeaderLength + messageBodyLength), torrentData);
                if (data is PieceMessage msg) {
                    peerMonitor?.ProtocolDown.AddDelta (-msg.RequestLength);
                    managerMonitor?.ProtocolDown.AddDelta (-msg.RequestLength);

                    peerMonitor?.DataDown.AddDelta (msg.RequestLength);
                    managerMonitor?.DataDown.AddDelta (msg.RequestLength);
                }
                return data;
            }
        }

        public static ReusableTask SendMessageAsync (IPeerConnection connection, IEncryption encryptor, PeerMessage message)
        {
            return SendMessageAsync (connection, encryptor, message, null, null, null);
        }

        public static async ReusableTask SendMessageAsync (IPeerConnection connection, IEncryption encryptor, PeerMessage message, IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor, SocketMemory buffer = default)
        {
            await MainLoop.SwitchToThreadpool ();

            int count = message.ByteLength;
            using (buffer.IsEmpty ? NetworkIO.BufferPool.Rent (count, out buffer) : default) {
                var pieceMessage = message as PieceMessage;
                message.Encode (buffer.AsSpan ());
                encryptor.Encrypt (buffer.AsSpan ());

                // Assume protocol first, then swap it to data once we successfully send the data bytes.
                await NetworkIO.SendAsync (connection, buffer, pieceMessage == null ? null : rateLimiter, peerMonitor?.ProtocolUp, managerMonitor?.ProtocolUp).ConfigureAwait (false);
                if (pieceMessage != null) {
                    peerMonitor?.ProtocolUp.AddDelta (-pieceMessage.RequestLength);
                    managerMonitor?.ProtocolUp.AddDelta (-pieceMessage.RequestLength);

                    peerMonitor?.DataUp.AddDelta (pieceMessage.RequestLength);
                    managerMonitor?.DataUp.AddDelta (pieceMessage.RequestLength);
                }
            }
        }
    }
}
