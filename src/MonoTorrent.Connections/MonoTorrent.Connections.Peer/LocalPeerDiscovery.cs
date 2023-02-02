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
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// When we send Announce we should embed the current port used to listen for incoming connections.
        /// </summary>
        string BaseSearchString { get; }

        /// <summary>
        /// A random identifier used to detect our own Announces so we can ignore them.
        /// </summary>
        string Cookie { get; }

        /// <summary>
        /// We glob together announces so that we don't iterate the network interfaces too frequently.
        /// </summary>
        Queue<(InfoHash, IPEndPoint)> PendingAnnounces { get; }

        /// <summary>
        /// Set to true when we're processing the pending announce queue.
        /// </summary>
        bool ProcessingAnnounces { get; set; }

        Task RateLimiterTask { get; set; }

        public LocalPeerDiscovery ()
            : base (new IPEndPoint (IPAddress.Any, MulticastAddressV4.Port))
        {
            lock (Random)
                Cookie = $"MT-{Random.Next (1, int.MaxValue)}";
            BaseSearchString = $"BT-SEARCH * HTTP/1.1\r\nHost: {MulticastAddressV4.Address}:{MulticastAddressV4.Port}\r\nPort: {{0}}\r\nInfohash: {{1}}\r\ncookie: {Cookie}\r\n\r\n\r\n";
            PendingAnnounces = new Queue<(InfoHash, IPEndPoint)> ();
            RateLimiterTask = Task.CompletedTask;
        }

        /// <summary>
        /// Send an announce request for this InfoHash.
        /// </summary>
        /// <param name="infoHash"></param>
        /// <param name="listeningPort"></param>
        /// <returns></returns>
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

        async void ProcessQueue ()
        {
            // Ensure this doesn't run on the UI thread as the networking calls can do some (partially) blocking operations.
            // Specifically 'NetworkInterface.GetAllNetworkInterfaces' is synchronous and can take hundreds of milliseconds.
            await SwitchToThreadpool ();

            await RateLimiterTask;

            using var sendingClient = new UdpClient ();
            var nics = NetworkInterface.GetAllNetworkInterfaces ();

            while (true) {
                InfoHash? infoHash = null;
                IPEndPoint? endPoint = null;
                lock (PendingAnnounces) {
                    if (PendingAnnounces.Count == 0) {
                        // Enforce a minimum delay before the next announce to avoid killing CPU by iterating network interfaces.
                        RateLimiterTask = Task.Delay (1000);
                        ProcessingAnnounces = false;
                        break;
                    }
                    (infoHash, endPoint) = PendingAnnounces.Dequeue ();
                }

                string message = string.Format (BaseSearchString, endPoint.Port, infoHash.ToHex ());
                byte[] data = Encoding.ASCII.GetBytes (message);

                foreach (var nic in nics) {
                    try {
                        sendingClient.Client.SetSocketOption (SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.HostToNetworkOrder (nic.GetIPProperties ().GetIPv4Properties ().Index));
                        await sendingClient.SendAsync (data, data.Length, MulticastAddressV4).ConfigureAwait (false);
                    } catch {
                        // If data can't be sent, just ignore the error
                    }
                }
            }
        }

        async void ReceiveAsync (UdpClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                try {
                    UdpReceiveResult result = await client.ReceiveAsync ().ConfigureAwait (false);
                    string[] receiveString = Encoding.ASCII.GetString (result.Buffer)
                        .Split (new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                    string? portString = receiveString.FirstOrDefault (t => t.StartsWith ("Port: ", StringComparison.Ordinal));
                    string? hashString = receiveString.FirstOrDefault (t => t.StartsWith ("Infohash: ", StringComparison.Ordinal));
                    string? cookieString = receiveString.FirstOrDefault (t => t.StartsWith ("cookie", StringComparison.Ordinal));

                    // An invalid response was received if these are missing.
                    if (portString == null || hashString == null)
                        continue;

                    // If we received our own cookie we can ignore the message.
                    if (cookieString != null && cookieString.Contains (Cookie))
                        continue;

                    // If the port is invalid, ignore it!
                    int portcheck = int.Parse (portString.Split (' ').Last ());
                    if (portcheck <= 0 || portcheck > 65535)
                        continue;

                    var infoHash = InfoHash.FromHex (hashString.Split (' ').Last ());
                    var uri = new Uri ($"ipv4://{result.RemoteEndPoint.Address}{':'}{portcheck}");

                    PeerFound?.Invoke (this, new LocalPeerFoundEventArgs (infoHash, uri));
                } catch (FileNotFoundException) {
                    throw;
                } catch {

                }
            }
        }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            var UdpClient = new UdpClient (PreferredLocalEndPoint);
            LocalEndPoint = (IPEndPoint?) UdpClient.Client.LocalEndPoint;

            token.Register (() => UdpClient.Dispose ());

            // enumerating all active IP interfaces and joining their multicast group, so we can reliably listen
            // on systems with multiple NIC
            var nics = NetworkInterface.GetAllNetworkInterfaces ();

            foreach (var nic in nics) {

                if ((!nic.Supports (NetworkInterfaceComponent.IPv4)) || (nic.OperationalStatus != OperationalStatus.Up))
                    continue;

                IPAddress? ip = null;
                foreach (var uni in nic.GetIPProperties ().UnicastAddresses) {
                    if (uni.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    ip = uni.Address;
                }

                if (ip is null)
                    continue;

                UdpClient.JoinMulticastGroup (MulticastAddressV4.Address, ip);
            }

            ReceiveAsync (UdpClient, token);
        }

        static ThreadSwitcher SwitchToThreadpool ()
            => new ThreadSwitcher ();
    }
}
