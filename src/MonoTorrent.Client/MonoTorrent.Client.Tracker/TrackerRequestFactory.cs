﻿//
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


using MonoTorrent.Client;
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
            ClientEngine engine = Manager.Engine;

            bool requireEncryption = !engine.Settings.AllowedEncryption.Contains (EncryptionType.PlainText);
            bool supportsEncryption = EncryptionTypes.SupportsRC4 (engine.Settings.AllowedEncryption);

            requireEncryption = requireEncryption && ClientEngine.SupportsEncryption;
            supportsEncryption = supportsEncryption && ClientEngine.SupportsEncryption;

            string ip = null;
            int port;
            if (engine.Settings.ReportedAddress != null) {
                ip = engine.Settings.ReportedAddress.Address.ToString ();
                port = engine.Settings.ReportedAddress.Port;
            } else if (engine.PeerListener.LocalEndPoint != null) {
                port = engine.PeerListener.LocalEndPoint.Port;
            } else {
                port = engine.Settings.ListenEndPoint?.Port ?? -1;
            }

            // FIXME: In metadata mode we need to pretend we need to download data otherwise
            // tracker optimisations might result in no peers being sent back.
            long bytesLeft = 1000;
            if (Manager.HasMetadata)
                bytesLeft = (long) ((1 - Manager.Bitfield.PercentComplete / 100.0) * Manager.Torrent.Size);

            return new AnnounceRequest (Manager.Monitor.DataBytesDownloaded,
                                          Manager.Monitor.DataBytesUploaded,
                                          bytesLeft,
                                          clientEvent, Manager.InfoHash, requireEncryption, Manager.Engine.PeerId.AsMemory ().ToArray (),
                                          ip, port, supportsEncryption);
        }

        public ScrapeRequest CreateScrape ()
        {
            return new ScrapeRequest (Manager.InfoHash);
        }
    }
}
