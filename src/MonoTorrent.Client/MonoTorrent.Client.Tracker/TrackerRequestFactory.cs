//
// TrackerRequestFactory.cs
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
using System.Linq;
using System.Net.Sockets;

using MonoTorrent.Client;
using MonoTorrent.Client.Modes;
using MonoTorrent.Connections;

namespace MonoTorrent.Trackers
{
    class TrackerRequestFactory : ITrackerRequestFactory
    {
        TorrentManager Manager { get; }

        public TrackerRequestFactory (TorrentManager manager)
        {
            Manager = manager;
        }

        public AnnounceRequest CreateAnnounce (TorrentEvent clientEvent)
        {
            ClientEngine engine = Manager.Engine!;

            bool requireEncryption = !engine.Settings.AllowedEncryption.Contains (EncryptionType.PlainText);
            bool supportsEncryption = EncryptionTypes.SupportsRC4 (engine.Settings.AllowedEncryption);

            requireEncryption = requireEncryption && ClientEngine.SupportsEncryption;
            supportsEncryption = supportsEncryption && ClientEngine.SupportsEncryption;

            Func<ConnectionMode, (string?, int)> reportedAddressFunc = mode => {
                string? ip = null;
                var addresssFamily = mode switch {
                    ConnectionMode.IPv4 => AddressFamily.InterNetwork,
                    ConnectionMode.IPv6 => AddressFamily.InterNetworkV6,
                    _ => throw new NotSupportedException ($"AddressFamily.{mode} not supported by {nameof (TrackerRequestFactory)}.{nameof (CreateAnnounce)}")
                };

                int port = engine.GetOverrideOrActualListenPort (ConnectionType.Tcp, mode).GetValueOrDefault (-1);
                if (engine.Settings.ReportedListenEndPoints.TryGetValue (ConnectionType.Tcp, out var reportedAddresses))
                    ip = reportedAddresses.Where (t => t.AddressFamily == addresssFamily).FirstOrDefault ()?.Address.ToString ();
                return (ip, port);
            };

            // FIXME: In metadata mode we need to pretend we need to download data otherwise
            // tracker optimisations might result in no peers being sent back.
            long bytesLeft = 1000;
            if (Manager.HasMetadata)
                bytesLeft = (long) ((1 - Manager.Bitfield.PercentComplete / 100.0) * Manager.Torrent!.Size);

            return new AnnounceRequest (Manager.Monitor.DataBytesReceived,
                                          Manager.Monitor.DataBytesSent,
                                          bytesLeft,
                                          clientEvent, Manager.InfoHashes, requireEncryption, Manager.Engine!.PeerId.AsMemory (),
                                          reportedAddressFunc, supportsEncryption);
        }

        public ScrapeRequest CreateScrape ()
        {
            return new ScrapeRequest (Manager.InfoHashes);
        }
    }
}
