//
// AnnounceRequest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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

namespace MonoTorrent.Trackers
{
    public sealed class AnnounceRequest
    {
        public long BytesDownloaded { get; private set; }
        public long BytesLeft { get; private set; }
        public long BytesUploaded { get; private set; }
        public TorrentEvent ClientEvent { get; private set; }
        public InfoHash InfoHash { get; private set; }
        public string? IPAddress { get; private set; }
        public ReadOnlyMemory<byte> PeerId { get; private set; }
        public int Port { get; private set; }
        public bool RequireEncryption { get; private set; }
        public bool SupportsEncryption { get; private set; }

        public AnnounceRequest (InfoHash infoHash)
        {
            InfoHash = infoHash;
        }

        public AnnounceRequest (long bytesDownloaded, long bytesUploaded, long bytesLeft,
                           TorrentEvent clientEvent, InfoHash infoHash, bool requireEncryption,
                           ReadOnlyMemory<byte> peerId, string? ipAddress, int port, bool supportsEncryption)
        {
            BytesDownloaded = bytesDownloaded;
            BytesUploaded = bytesUploaded;
            BytesLeft = bytesLeft;
            ClientEvent = clientEvent;
            InfoHash = infoHash;
            RequireEncryption = requireEncryption;
            PeerId = peerId;
            IPAddress = ipAddress;
            Port = port;
            SupportsEncryption = supportsEncryption;
        }

        public AnnounceRequest WithBytesDownloaded (long bytesDownloaded)
        {
            var clone = this;
            if (bytesDownloaded != BytesDownloaded) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.BytesDownloaded = bytesDownloaded;
            }
            return clone;
        }

        public AnnounceRequest WithBytesLeft (long bytesLeft)
        {
            var clone = this;
            if (bytesLeft != BytesLeft) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.BytesLeft = bytesLeft;
            }
            return clone;
        }

        public AnnounceRequest WithBytesUploaded (long bytesUploaded)
        {
            var clone = this;
            if (bytesUploaded != BytesUploaded) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.BytesUploaded = bytesUploaded;
            }
            return clone;
        }

        public AnnounceRequest WithClientEvent (TorrentEvent clientEvent)
        {
            var clone = this;
            if (clientEvent != ClientEvent) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.ClientEvent = clientEvent;
            }
            return clone;
        }

        public AnnounceRequest WithInfoHash (InfoHash infoHash)
        {
            var clone = this;
            if (infoHash != InfoHash) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.InfoHash = infoHash;
            }
            return clone;
        }

        public AnnounceRequest WithIPAddress (string ipAddress)
        {
            var clone = this;
            if (ipAddress != IPAddress) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.IPAddress = ipAddress;
            }
            return clone;
        }

        public AnnounceRequest WithPeerId (ReadOnlyMemory<byte> peerId)
        {
            var clone = this;
            if (!peerId.Equals (PeerId)) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.PeerId = peerId;
            }
            return clone;
        }

        public AnnounceRequest WithPort (int port)
        {
            var clone = this;
            if (port != Port) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.Port = port;
            }
            return clone;
        }

        public AnnounceRequest WithRequireEncryption (bool requireEncryption)
        {
            var clone = this;
            if (requireEncryption != RequireEncryption) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.RequireEncryption = requireEncryption;
            }
            return clone;
        }

        public AnnounceRequest WithSupportsEncryption (bool supportsEncryption)
        {
            var clone = this;
            if (supportsEncryption != SupportsEncryption) {
                clone = (AnnounceRequest) MemberwiseClone ();
                clone.SupportsEncryption = supportsEncryption;
            }
            return clone;
        }
    }
}
