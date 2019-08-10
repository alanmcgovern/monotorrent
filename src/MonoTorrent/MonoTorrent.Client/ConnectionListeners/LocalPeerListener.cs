//
// LocalPeerListener.cs
//
// Authors:
//   Jared Hendry hendry.jared@gmail.com
//
// Copyright (C) 2008 Jared Hendry
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
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MonoTorrent.Client
{
    class LocalPeerListener : Listener
    {
        const int MulticastPort = 6771;
        static readonly IPAddress MulticastIpAddress = IPAddress.Parse("239.192.152.143");

        public event EventHandler<LocalPeerFoundEventArgs> PeerFound;

        CancellationTokenSource Cancellation { get; set; }
        Regex RegexMatcher = new Regex("BT-SEARCH \\* HTTP/1.1\\r\\nHost: 239.192.152.143:6771\\r\\nPort: (?<port>[^@]+)\\r\\nInfohash: (?<hash>[^@]+)\\r\\n\\r\\n\\r\\n");

        public LocalPeerListener ()
            : base (new IPEndPoint (IPAddress.Any, 6771))
        {
        }

        public override void Start()
        {
            if (Status == ListenerStatus.Listening)
                return;

            Cancellation?.Cancel ();
            Cancellation = new CancellationTokenSource();

            try {
                var client = new UdpClient(MulticastPort);
                Cancellation.Token.Register (() => client.SafeDispose ());

                client.JoinMulticastGroup(MulticastIpAddress);
                ReceiveAsync (client, Cancellation.Token);
                RaiseStatusChanged(ListenerStatus.Listening);
            } catch {
                Cancellation?.Cancel ();
                Cancellation = null;
                RaiseStatusChanged(ListenerStatus.PortNotFree);
            }
        }

        public override void Stop()
        {
            if (Status == ListenerStatus.NotListening)
                return;

            Cancellation?.Cancel ();
            Cancellation = null;
            RaiseStatusChanged(ListenerStatus.NotListening);
        }

        async void ReceiveAsync (UdpClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                try {
                    var result = await client.ReceiveAsync ().ConfigureAwait (false);
                    var receiveString = Encoding.ASCII.GetString(result.Buffer);

                    var match = RegexMatcher.Match(receiveString);
                    if (!match.Success)
                        return;

                    int portcheck = Convert.ToInt32(match.Groups["port"].Value);
                    if (portcheck <= 0 || portcheck > 65535)
                        return;

                    var infoHash = InfoHash.FromHex(match.Groups["hash"].Value);
                    var uri = new Uri("ipv4://" + result.RemoteEndPoint.Address + ':' + portcheck);

                    PeerFound?.Invoke (this, new LocalPeerFoundEventArgs (infoHash, uri));
                } catch {

                }
            }
        }
    }
}
