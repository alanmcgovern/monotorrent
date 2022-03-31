//
// ConnectionMonitor.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.ComponentModel;

using MonoTorrent.Messages.Peer;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class is used to track upload/download speed and bytes uploaded/downloaded for each connection
    /// </summary>
    public class ConnectionMonitor
    {

        internal SpeedMonitor DataDown { get; }
        internal SpeedMonitor DataUp { get; }
        internal SpeedMonitor ProtocolDown { get; }
        internal SpeedMonitor ProtocolUp { get; }

        [Obsolete ("Use DataBytesReceived instead")]
        [EditorBrowsable (EditorBrowsableState.Never)]
        public long DataBytesDownloaded => DataBytesReceived;

        [Obsolete ("Use DataBytesSent instead")]
        [EditorBrowsable (EditorBrowsableState.Never)]
        public long DataBytesUploaded => DataBytesSent;

        [Obsolete ("Use DownloadRate instead")]
        [EditorBrowsable (EditorBrowsableState.Never)]
        public long DownloadSpeed => DownloadRate;

        [Obsolete ("Use ProtocolBytesReceived instead")]
        [EditorBrowsable (EditorBrowsableState.Never)]
        public long ProtocolBytesDownloaded => ProtocolBytesReceived;

        [Obsolete ("Use ProtocolBytesSent instead")]
        [EditorBrowsable (EditorBrowsableState.Never)]
        public long ProtocolBytesUploaded => ProtocolBytesSent;

        [Obsolete ("Use UploadRate instead")]
        [EditorBrowsable (EditorBrowsableState.Never)]
        public long UploadSpeed => UploadRate;

        /// <summary>
        /// Total bytes of <see cref="ITorrentFile"/> data received from this peer. This value tracks the size of the payload of each <see cref="PieceMessage"/> received from this peer, which is <see cref="PieceMessage.RequestLength"/> bytes.
        /// </summary>
        public long DataBytesReceived => DataDown.Total;

        /// <summary>
        /// Total bytes of <see cref="ITorrentFile"/> data sent to this peer. This value tracks the size of the payload of each <see cref="PieceMessage"/> sent to this peer, which is <see cref="PieceMessage.RequestLength"/> bytes.
        /// </summary>
        public long DataBytesSent => DataUp.Total;

        /// <summary>
        /// Current receive rate, in bytes/second, for this peer. This value includes 'data' as well as 'protocol'.
        /// </summary>
        public long DownloadRate => DataDown.Rate + ProtocolDown.Rate;

        /// <summary>
        /// Total bytes of protocol data received from this peer. This value tracks the size of each <see cref="PeerMessage"/> received from this peer, excluding the payload portion of each <see cref="PieceMessage"/>, which is <see cref="PieceMessage.RequestLength"/> bytes.
        /// </summary>
        public long ProtocolBytesReceived => ProtocolDown.Total;

        /// <summary>
        /// Total bytes of protocol data sent to this peer. This value tracks the size of each <see cref="PeerMessage"/> sent to this peer, excluding the payload portion of each <see cref="PieceMessage"/>, which is <see cref="PieceMessage.RequestLength"/> bytes.
        /// </summary>
        public long ProtocolBytesSent => ProtocolUp.Total;

        /// <summary>
        /// Current send rate, in bytes/second, for this peer. This value includes 'data' as well as 'protocol'.
        /// </summary>
        public long UploadRate => DataUp.Rate + ProtocolUp.Rate;

        internal ConnectionMonitor ()
            : this (12)
        {

        }

        internal ConnectionMonitor (int averagingPeriod)
        {
            DataDown = new SpeedMonitor (averagingPeriod);
            DataUp = new SpeedMonitor (averagingPeriod);
            ProtocolDown = new SpeedMonitor (averagingPeriod);
            ProtocolUp = new SpeedMonitor (averagingPeriod);
        }

        internal void Reset ()
        {
            DataDown.Reset ();
            DataUp.Reset ();
            ProtocolDown.Reset ();
            ProtocolUp.Reset ();
        }

        internal void Tick ()
        {
            DataDown.Tick ();
            DataUp.Tick ();
            ProtocolDown.Tick ();
            ProtocolUp.Tick ();
        }
    }
}
