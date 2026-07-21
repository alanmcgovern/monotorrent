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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;

using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages;

using ReusableTasks;

namespace MonoTorrent.Client
{
    static class PeerIO
    {
        const int MaxMessageLength = Constants.BlockSize * 4;

        public static async ReusableTask<(Memory<byte> handshake, ByteBufferPool.Releaser releaser)> ReceiveHandshakeAsync (IPeerConnection connection, IEncryption decryptor)
        {
            await MainLoop.SwitchToThreadpool ();

            var releaser = NetworkIO.BufferPool.Rent (HandshakeMessage.HandshakeLength, out Memory<byte> buffer);
            try {
                await NetworkIO.ReceiveAsync (connection, buffer, null, null, null).ConfigureAwait (false);

                decryptor.Decrypt (buffer.Span);

                return (buffer, releaser);
            } catch {
                releaser.Dispose ();
                throw;
            }
        }

        public static async ReusableTask ReceiveHandshakeAsync (IPeerConnection connection, IEncryption decryptor, Memory<byte> buffer)
        {
            await MainLoop.SwitchToThreadpool ();

            await NetworkIO.ReceiveAsync (connection, buffer, null, null, null).ConfigureAwait (false);
            decryptor.Decrypt (buffer.Span);
        }

        public static ReusableTask<ReadOnlyMemory<byte>> ReceiveMessageAsync (IPeerConnection connection, IEncryption decryptor)
        {
            return ReceiveMessageAsync (connection, decryptor, null, null, null, null);
        }

        public static async ReusableTask<ReadOnlyMemory<byte>> ReceiveMessageAsync (IPeerConnection connection, IEncryption decryptor, IRateLimiter? rateLimiter, ConnectionMonitor? peerMonitor, ConnectionMonitor? managerMonitor, ITorrentManagerInfo? torrentData)
        {
            return (await ReceiveMessageAsync (connection, decryptor, rateLimiter, peerMonitor, managerMonitor, torrentData, new byte[4]).ConfigureAwait (false)).message;
        }

        static ReadOnlyMemory<byte> InternedKeepAlive = new byte[4];

        public static async ReusableTask<(ReadOnlyMemory<byte> message, ByteBufferPool.Releaser releaser)> ReceiveMessageAsync (IPeerConnection connection, IEncryption decryptor, IRateLimiter? rateLimiter, ConnectionMonitor? peerMonitor, ConnectionMonitor? managerMonitor, ITorrentManagerInfo? torrentData, Memory<byte> headerBuffer)
        {
            if (headerBuffer.Length != 4)
                throw new ArgumentException ("The header buffer should always be exactly 4 bytes - sufficient to receive the message length");

            await MainLoop.SwitchToThreadpool ();

            await NetworkIO.ReceiveAsync (connection, headerBuffer, rateLimiter, peerMonitor?.ProtocolDown, managerMonitor?.ProtocolDown).ConfigureAwait (false);

            decryptor.Decrypt (headerBuffer.Span);

            var payloadLength = BinaryPrimitives.ReadInt32BigEndian (headerBuffer.Span);
            if (payloadLength < 0) {
                connection.Dispose ();
                throw new ProtocolException ($"Invalid message length received. Value was negative: '{payloadLength}'");
            }

            if (payloadLength > MaxMessageLength) {
                // Some messages are proportional to the size of the bitfield. If any message
                // exceeds the regular threshold, calculate whether or not it could plausibly
                // be a bitfield. Maybe this should be restricted to just bitfield messages?
                if (payloadLength > ((torrentData?.TorrentInfo?.PieceCount () ?? 0) / 8 + 64))
                    throw new ProtocolException ($"Invalid message length received. Value was too large: '{payloadLength}'");
            }

            if (payloadLength == 0) {
                // If we've received a keepalive we don't need to allocate a new buffer to
                // return it. We can return a ReadOnlyMemory with the 4 zero bytes in it.
                // Saves a small bit of churn on renting/releasing buffers.
                return (InternedKeepAlive, default);
            }

            var messageBufferReleaser = NetworkIO.BufferPool.Rent (payloadLength + headerBuffer.Length, out var messageBuffer);
            headerBuffer.CopyTo (messageBuffer);

            // Always assume protocol first, then convert to data when we what message it is!
            await NetworkIO.ReceiveAsync (connection, messageBuffer.Slice (headerBuffer.Length), rateLimiter, peerMonitor?.ProtocolDown, managerMonitor?.ProtocolDown).ConfigureAwait (false);

            // Decrypt in place
            decryptor.Decrypt (messageBuffer.Span.Slice (headerBuffer.Length));

            if (MessageDispatcher.GetType (messageBuffer) == MessageType.Piece) {
                var requestLength = new PieceMessage (messageBuffer).RequestLength;
                peerMonitor?.ProtocolDown.AddDelta (-requestLength);
                managerMonitor?.ProtocolDown.AddDelta (-requestLength);

                peerMonitor?.DataDown.AddDelta (requestLength);
                managerMonitor?.DataDown.AddDelta (requestLength);
            }
            return (messageBuffer, messageBufferReleaser);
        }

        public static ReusableTask SendMessageAsync (IPeerConnection connection, IEncryption encryptor, Memory<byte> message)
        {
            return SendMessageAsync (connection, encryptor, message, null, null, null);
        }

        public static async ReusableTask SendMessageAsync (IPeerConnection connection, IEncryption encryptor, Memory<byte> msg, IRateLimiter? rateLimiter, ConnectionMonitor? peerMonitor, ConnectionMonitor? managerMonitor)
        {
            await MainLoop.SwitchToThreadpool ();

            // Check if it's a piece message before encrypting it, otherwise we can't tell.
            var isPieceMessage = MessageDispatcher.GetType (msg) == MessageType.Piece;
            encryptor.Encrypt (msg.Span);

            // Assume protocol first, then swap it to data once we successfully send the data bytes.
            await NetworkIO.SendAsync (connection, msg, isPieceMessage ? rateLimiter : null, peerMonitor?.ProtocolUp, managerMonitor?.ProtocolUp).ConfigureAwait (false);
            if (isPieceMessage) {
                var requestLength = new PieceMessage (msg).RequestLength;
                peerMonitor?.ProtocolUp.AddDelta (-requestLength);
                managerMonitor?.ProtocolUp.AddDelta (-requestLength);

                peerMonitor?.DataUp.AddDelta (requestLength);
                managerMonitor?.DataUp.AddDelta (requestLength);
            }
        }
    }
}
