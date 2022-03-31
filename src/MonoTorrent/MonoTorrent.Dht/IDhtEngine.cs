//
// IDhtEngine.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Threading.Tasks;

using MonoTorrent.Connections.Dht;

namespace MonoTorrent.Dht
{
    public interface ITransferMonitor
    {
        /// <summary>
        /// Total bytes sent since the start of the session.
        /// </summary>
        long BytesSent { get; }

        /// <summary>
        /// Total bytes received since the start of the session.
        /// </summary>
        long BytesReceived { get; }

        /// <summary>
        /// Estimate of the amount of data received every second, in bytes/second.
        /// </summary>
        long DownloadRate { get; }

        /// <summary>
        /// Estimate of the amount of data sent every second, in bytes/second.
        /// </summary>
        long UploadRate { get; }
    }

    public interface IDhtEngine : IDisposable
    {
        event EventHandler<PeersFoundEventArgs> PeersFound;
        event EventHandler StateChanged;

        TimeSpan AnnounceInterval { get; }
        bool Disposed { get; }
        ITransferMonitor Monitor { get; }
        TimeSpan MinimumAnnounceInterval { get; }
        int NodeCount { get; }
        DhtState State { get; }

        void Add (IEnumerable<ReadOnlyMemory<byte>> nodes);
        void Announce (InfoHash infoHash, int port);
        void GetPeers (InfoHash infoHash);
        Task<ReadOnlyMemory<byte>> SaveNodesAsync ();
        Task SetListenerAsync (IDhtListener listener);
        Task StartAsync ();
        Task StartAsync (ReadOnlyMemory<byte> initialNodes);
        Task StopAsync ();
    }
}
