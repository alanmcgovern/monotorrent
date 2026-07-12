//
// UtpTransportSettings.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2026 Alan McGovern
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

namespace MonoTorrent.Connections.Peer.Utp
{
    public sealed record class UtpTransportSettings
    {
        public const int DefaultInitialPacketSize = 1400;
        public const int DefaultMaxReceiveBufferBytes = 1024 * 1024;
        public const int DefaultMaxSendQueuePackets = 4096;
        public const int DefaultSocketReceiveBufferBytes = 2 * 1024 * 1024;
        public const int DefaultSocketSendBufferBytes = 1024 * 1024;
        public const int MinimumRecoveryPacketSize = 150;

        public int MaxConnectedTimeouts { get; init; } = 4;

        public int MaxSynTimeouts { get; init; } = 2;

        public TimeSpan KeepAliveInterval { get; init; } = TimeSpan.FromSeconds (29);

        public TimeSpan ZeroWindowProbeInterval { get; init; } = TimeSpan.FromSeconds (15);

        public int MaxReorderDistance { get; init; } = 1024;

        public int MaxIncomingSynConnections { get; init; } = 1024;

        public int MaxReceiveBufferBytes { get; init; } = DefaultMaxReceiveBufferBytes;

        public int MaxSendQueuePackets { get; init; } = DefaultMaxSendQueuePackets;

        public int SocketReceiveBufferBytes { get; init; } = DefaultSocketReceiveBufferBytes;

        public int SocketSendBufferBytes { get; init; } = DefaultSocketSendBufferBytes;

        public TimeSpan DelayedAckDelay { get; init; } = TimeSpan.FromMilliseconds (10);

        public TimeSpan CongestionControlTarget { get; init; } = TimeSpan.FromMilliseconds (100);

        public bool EnableDelayedAcks { get; init; } = true;

        public bool EnablePathMtuDiscovery { get; init; } = true;

        public TimeSpan MtuProbeInterval { get; init; } = TimeSpan.FromMinutes (30);

        public int InitialPacketSize { get; init; } = DefaultInitialPacketSize;

        public static UtpTransportSettings Create (UtpTransportSettings? settings)
        {
            settings ??= new UtpTransportSettings ();

            if (settings.InitialPacketSize < MinimumRecoveryPacketSize)
                throw new ArgumentOutOfRangeException (nameof (settings), $"The initial uTP packet size must be at least {MinimumRecoveryPacketSize} bytes.");
            if (settings.MaxConnectedTimeouts < 1)
                throw new ArgumentOutOfRangeException (nameof (settings), "The maximum connected timeout count must be at least 1.");
            if (settings.MaxSynTimeouts < 1)
                throw new ArgumentOutOfRangeException (nameof (settings), "The maximum SYN timeout count must be at least 1.");
            if (settings.MaxReorderDistance < 32)
                throw new ArgumentOutOfRangeException (nameof (settings), "The maximum reorder distance must be at least 32 packets.");
            if (settings.MaxReorderDistance > 2015)
                throw new ArgumentOutOfRangeException (nameof (settings), "The maximum reorder distance must not exceed 2015 packets.");
            if (settings.MaxIncomingSynConnections < 1)
                throw new ArgumentOutOfRangeException (nameof (settings), "The maximum incoming SYN connection count must be at least 1.");
            if (settings.MaxReceiveBufferBytes < 1)
                throw new ArgumentOutOfRangeException (nameof (settings), "The maximum receive buffer size must be at least 1 byte.");
            if (settings.MaxSendQueuePackets < 1)
                throw new ArgumentOutOfRangeException (nameof (settings), "The maximum send queue packet count must be at least 1.");
            if (settings.SocketReceiveBufferBytes < 1)
                throw new ArgumentOutOfRangeException (nameof (settings), "The socket receive buffer size must be at least 1 byte.");
            if (settings.SocketSendBufferBytes < 1)
                throw new ArgumentOutOfRangeException (nameof (settings), "The socket send buffer size must be at least 1 byte.");
            if (settings.KeepAliveInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException (nameof (settings), "The keep-alive interval must be positive.");
            if (settings.ZeroWindowProbeInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException (nameof (settings), "The zero window probe interval must be positive.");
            if (settings.DelayedAckDelay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException (nameof (settings), "The delayed ACK delay must be positive.");
            if (settings.CongestionControlTarget <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException (nameof (settings), "The congestion control target delay must be positive.");
            if (settings.MtuProbeInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException (nameof (settings), "The MTU probe interval must be positive.");

            return settings;
        }
    }
}
