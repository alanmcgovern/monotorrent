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


namespace MonoTorrent.Client.Tracker
{
    class TrackerRequestFactory : ITrackerRequestFactory
    {
        TorrentManager Manager { get; }

        public TrackerRequestFactory (TorrentManager manager)
        {
            Manager = manager;
        }

        public AnnounceParameters CreateAnnounce (TorrentEvent clientEvent)
        {
            ClientEngine engine = Manager.Engine;

            EncryptionTypes e = engine.Settings.AllowedEncryption;
            bool requireEncryption = !e.HasFlag (EncryptionTypes.PlainText);
            bool supportsEncryption = e.HasFlag (EncryptionTypes.RC4Full) || e.HasFlag (EncryptionTypes.RC4Header);

            requireEncryption = requireEncryption && ClientEngine.SupportsEncryption;
            supportsEncryption = supportsEncryption && ClientEngine.SupportsEncryption;

            string ip = null;
            int port;
            if (engine.Settings.ReportedAddress != null) {
                ip = engine.Settings.ReportedAddress.Address.ToString ();
                port = engine.Settings.ReportedAddress.Port;
            } else {
                port = engine.Settings.ListenPort;
            }

            // FIXME: In metadata mode we need to pretend we need to download data otherwise
            // tracker optimisations might result in no peers being sent back.
            long bytesLeft = 1000;
            if (Manager.HasMetadata)
                bytesLeft = (long) ((1 - Manager.Bitfield.PercentComplete / 100.0) * Manager.Torrent.Size);

            return new AnnounceParameters (Manager.Monitor.DataBytesDownloaded,
                                          Manager.Monitor.DataBytesUploaded,
                                          bytesLeft,
                                          clientEvent, Manager.InfoHash, requireEncryption, Manager.Engine.PeerId,
                                          ip, port, supportsEncryption);
        }

        public ScrapeParameters CreateScrape ()
        {
            return new ScrapeParameters (Manager.InfoHash);
        }
    }
}
