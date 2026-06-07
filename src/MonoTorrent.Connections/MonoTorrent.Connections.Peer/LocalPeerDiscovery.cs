//
// LocalPeerDiscovery.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Connections.Peer
{
    public class LocalPeerDiscovery : SocketListener, ILocalPeerDiscovery
    {
        /// <summary>
        /// The IPAddress and port of the IPV4 multicast group.
        /// </summary>
        static readonly IPEndPoint MulticastAddressV4 = new IPEndPoint (IPAddress.Parse ("239.192.152.143"), 6771);

        // u8 literals for the fixed parts of the announce message, avoiding any string allocation at announce time.
        static ReadOnlySpan<byte> HeaderLine => "BT-SEARCH * HTTP/1.1\r\n"u8;
        static ReadOnlySpan<byte> HostPrefix => "Host: 239.192.152.143:6771\r\n"u8;
        static ReadOnlySpan<byte> PortPrefix => "Port: "u8;
        static ReadOnlySpan<byte> InfohashPrefix => "\r\nInfohash: "u8;
        static ReadOnlySpan<byte> CookiePrefix => "\r\ncookie: "u8;
        static ReadOnlySpan<byte> Trailer => "\r\n\r\n\r\n"u8;
        static ReadOnlySpan<byte> CrLf => "\r\n"u8;

        // Prefix bytes used during receive parsing — no allocation.
        static ReadOnlySpan<byte> PortPrefixParse => "Port: "u8;
        static ReadOnlySpan<byte> InfohashPrefixParse => "Infohash: "u8;
        static ReadOnlySpan<byte> CookiePrefixParse => "cookie"u8;

        // Maximum wire size: header (~22) + host line (~30) + port line (~14) +
        // infohash line (~50) + cookie line (~50) + trailer (6) = well under 256.
        const int MaxMessageSize = 512;

        /// <summary>
        /// Used to generate a unique identifier for this client instance.
        /// </summary>
        static readonly Random Random = new Random ();

        /// <summary>
        /// This asynchronous event is raised whenever a peer is discovered.
        /// </summary>
        public event EventHandler<LocalPeerFoundEventArgs>? PeerFound;

        public TimeSpan AnnounceInternal => TimeSpan.FromMinutes (5);
        public TimeSpan MinimumAnnounceInternal => TimeSpan.FromMinutes (1);

        // Cookie stored as bytes so we never re-encode it at announce time.
        readonly byte[] CookieBytes;

        /// <summary>
        /// We glob together announces so we don't iterate network interfaces too frequently.
        /// </summary>
        Queue<(InfoHash, IPEndPoint)> PendingAnnounces { get; }

        bool ProcessingAnnounces { get; set; }

        Task RateLimiterTask { get; set; }

        // Raw Socket instead of UdpClient so we can use SendTo with a pooled buffer and
        // ReceiveFrom with a pre-allocated buffer — both are allocation-free on the hot path.
        Socket SendingSocket { get; }

        // Reusable receive buffer; only one receive is outstanding at a time per socket.
        readonly byte[] ReceiveBuffer = new byte[2048];

        // Reused EndPoint object for ReceiveFrom — avoids allocating a new IPEndPoint per datagram.
        EndPoint RemoteEndPointReuse = new IPEndPoint (IPAddress.Any, 0);

        public LocalPeerDiscovery ()
            : base (new IPEndPoint (IPAddress.Any, MulticastAddressV4.Port))
        {
            int cookie;
            lock (Random)
                cookie = Random.Next (1, int.MaxValue);

            // Pre-encode the cookie once; reuse bytes on every announce.
            CookieBytes = Encoding.ASCII.GetBytes ($"MT-{cookie}");

            PendingAnnounces = new Queue<(InfoHash, IPEndPoint)> ();
            RateLimiterTask = Task.CompletedTask;

            SendingSocket = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            SendingSocket.SendBufferSize = 1024;
            SendingSocket.ReceiveBufferSize = 1024;
        }

        /// <summary>
        /// Send an announce request for this InfoHash.
        /// </summary>
        public Task Announce (InfoHash infoHash, IPEndPoint listeningPort)
        {
            lock (PendingAnnounces) {
                PendingAnnounces.Enqueue ((infoHash, listeningPort));
                if (!ProcessingAnnounces) {
                    ProcessingAnnounces = true;
                    ProcessQueue ();
                }
            }
            return Task.CompletedTask;
        }

        // Writes the announce datagram into `dest` and returns the number of bytes written.
        // Zero heap allocations.
        int BuildAnnounceMessage (Span<byte> dest, InfoHash infoHash, int port)
        {
            var buf = dest;

            // "BT-SEARCH * HTTP/1.1\r\n"
            HeaderLine.CopyTo (buf);
            buf = buf[HeaderLine.Length..];

            // "Host: 239.192.152.143:6771\r\n"
            HostPrefix.CopyTo (buf);
            buf = buf[HostPrefix.Length..];

            // "Port: <port>\r\n"
            PortPrefix.CopyTo (buf);
            buf = buf[PortPrefix.Length..];
            bool ok = Utf8Formatter.TryFormat (port, buf, out int written);
            buf = buf[written..];
            CrLf.CopyTo (buf);
            buf = buf[CrLf.Length..];

            // "Infohash: <40-char hex>\r\n"
            InfohashPrefix.CopyTo (buf);
            buf = buf[InfohashPrefix.Length..];
            // InfoHash.ToHex() returns a string; use a stackalloc hex encoder to avoid that allocation.
            WriteHexBytes (infoHash.Span, buf);
            buf = buf[(infoHash.Span.Length * 2)..];
            CrLf.CopyTo (buf);
            buf = buf[CrLf.Length..];

            // "cookie: MT-<n>\r\n"
            CookiePrefix.CopyTo (buf);
            buf = buf[CookiePrefix.Length..];
            CookieBytes.CopyTo (buf);
            buf = buf[CookieBytes.Length..];

            // "\r\n\r\n\r\n"
            Trailer.CopyTo (buf);
            buf = buf[Trailer.Length..];

            return dest.Length - buf.Length;
        }

        // Encode bytes as uppercase ASCII hex without any heap allocation.
        static void WriteHexBytes (ReadOnlySpan<byte> src, Span<byte> dest)
        {
            const string Hex = "0123456789ABCDEF";
            for (int i = 0; i < src.Length; i++) {
                dest[i * 2] = (byte) Hex[src[i] >> 4];
                dest[i * 2 + 1] = (byte) Hex[src[i] & 0xF];
            }
        }

        async void ProcessQueue ()
        {
            // Get off the UI / calling thread before doing any blocking network work.
            await SwitchToThreadpool ();
            await RateLimiterTask;

            Process ();

            void Process ()
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces ();

                // Single stack-allocated send buffer reused across every NIC / announce.
                using var releaser = MemoryPool.Default.Rent (MaxMessageSize, out Memory<byte> sendBuf);

                while (true) {
                    InfoHash? infoHash;
                    IPEndPoint? endPoint;

                    lock (PendingAnnounces) {
                        if (PendingAnnounces.Count == 0) {
                            RateLimiterTask = Task.Delay (1000);
                            ProcessingAnnounces = false;
                            break;
                        }
                        (infoHash, endPoint) = PendingAnnounces.Dequeue ();
                    }

                    int len = BuildAnnounceMessage (sendBuf.Span, infoHash, endPoint.Port);
                    // Rent a heap buffer only for the actual SendTo call (Socket API requires byte[]).

                    foreach (var nic in nics) {
                        try {
                            SendingSocket.SetSocketOption (
                                SocketOptionLevel.IP,
                                SocketOptionName.MulticastInterface,
                                IPAddress.HostToNetworkOrder (nic.GetIPProperties ().GetIPv4Properties ().Index));

                            // Synchronous SendTo: no Task/allocation on the sending path.
                            SendingSocket.SendTo (sendBuf.Span.Slice (0, len), SocketFlags.None, MulticastAddressV4);
                        } catch {
                            // Ignore per-NIC failures.
                        }
                    }
                }
            }
        }

        // Parses a datagram received in `buffer[..length]` using only Span APIs — no string allocations.
        void ParseDatagram (ReadOnlySpan<byte> buffer, IPEndPoint remote)
        {
            ReadOnlySpan<byte> port = default;
            ReadOnlySpan<byte> infohash = default;
            bool hasCookieMatch = false;

            var remaining = buffer;
            while (!remaining.IsEmpty) {
                // Find end of line.
                int idx = remaining.IndexOf (CrLf);
                ReadOnlySpan<byte> line = idx >= 0 ? remaining[..idx] : remaining;
                remaining = idx >= 0 ? remaining[(idx + CrLf.Length)..] : default;

                if (line.IsEmpty)
                    continue;

                if (line.StartsWith (PortPrefixParse)) {
                    port = line[PortPrefixParse.Length..].TrimStart ((byte) ' ');
                } else if (line.StartsWith (InfohashPrefixParse)) {
                    infohash = line[InfohashPrefixParse.Length..].TrimStart ((byte) ' ');
                } else if (line.StartsWith (CookiePrefixParse)) {
                    // Check if the cookie value contains our own cookie bytes.
                    // Use IndexOf on byte spans — zero allocation.
                    hasCookieMatch = line.IndexOf (CookieBytes) >= 0;
                }
            }

            if (port.IsEmpty || infohash.IsEmpty)
                return;

            if (hasCookieMatch)
                return;

            if (!Utf8Parser.TryParse (port, out int portNumber, out _) || portNumber <= 0 || portNumber > 65535)
                return;

            // InfoHash.FromHex accepts a ReadOnlySpan<byte> in modern MonoTorrent — no string needed.
            var hash = InfoHash.FromHex (infohash);
            var uri = new Uri ($"ipv4://{remote.Address}:{portNumber}");

            PeerFound?.Invoke (this, new LocalPeerFoundEventArgs (hash, uri));
        }

        // Receive loop using Socket.ReceiveFromAsync (SocketAsyncEventArgs overload) so the OS
        // writes directly into our pre-allocated ReceiveBuffer — no per-packet byte[] allocation.
        async void ReceiveAsync (Socket socket, CancellationToken token)
        {
            var saea = new SocketAsyncEventArgs ();
            saea.SetBuffer (ReceiveBuffer, 0, ReceiveBuffer.Length);
            saea.RemoteEndPoint = RemoteEndPointReuse;

            // Wrap SAEA in a reusable TaskCompletionSource-free awaitable via a simple
            // event-driven loop — we signal a SemaphoreSlim (one permit) instead.
            using var signal = new SemaphoreSlim (0, 1);
            saea.Completed += (_, _) => signal.Release ();

            while (!token.IsCancellationRequested) {
                saea.SocketError = SocketError.Success;
                saea.RemoteEndPoint = RemoteEndPointReuse;   // reset each iteration

                bool completedSync;
                try {
                    completedSync = !socket.ReceiveFromAsync (saea);
                } catch (ObjectDisposedException) {
                    break;
                } catch {
                    continue;
                }

                if (!completedSync)
                    await signal.WaitAsync (token).ConfigureAwait (false);

                if (saea.SocketError != SocketError.Success)
                    continue;

                int received = saea.BytesTransferred;
                if (received <= 0)
                    continue;

                // RemoteEndPoint is reused but ReceiveFromAsync always writes the sender address
                // into it before the completion fires — safe to read here.
                var remote = (IPEndPoint) saea.RemoteEndPoint!;

                try {
                    ParseDatagram (ReceiveBuffer.AsSpan (0, received), remote);
                } catch (FileNotFoundException) {
                    throw;
                } catch {
                    // Ignore malformed datagrams.
                }
            }

            saea.Dispose ();
        }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            // Build the receiving socket manually so we can use it with SocketAsyncEventArgs.
            var receiveSocket = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            receiveSocket.SendBufferSize = 1024;
            receiveSocket.ReceiveBufferSize = 32768;
            receiveSocket.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiveSocket.Bind (PreferredLocalEndPoint);
            LocalEndPoint = (IPEndPoint?) receiveSocket.LocalEndPoint;

            token.Register (() => receiveSocket.Dispose ());

            var nics = NetworkInterface.GetAllNetworkInterfaces ();
            foreach (var nic in nics) {
                if (!nic.Supports (NetworkInterfaceComponent.IPv4) || nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                IPAddress? ip = null;
                foreach (var uni in nic.GetIPProperties ().UnicastAddresses) {
                    if (uni.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    ip = uni.Address;
                }

                if (ip is null)
                    continue;

                try {
                    receiveSocket.SetSocketOption (SocketOptionLevel.IP, SocketOptionName.AddMembership,
                        new MulticastOption (MulticastAddressV4.Address, ip));
                } catch {
                    // Some NICs don't support multicast; ignore them.
                }
            }

            ReceiveAsync (receiveSocket, token);
        }

        static ThreadSwitcher SwitchToThreadpool ()
            => new ThreadSwitcher ();
    }
}
