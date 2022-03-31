//
// NullDhtEngine.cs
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
using MonoTorrent.Dht;

namespace MonoTorrent.Client
{
    class NullTransferMonitor : ITransferMonitor
    {
        public long BytesSent { get; }
        public long BytesReceived { get; }
        public long DownloadRate { get; }
        public long UploadRate { get; }
    }

    class NullDhtEngine : IDhtEngine
    {
#pragma warning disable 0067
        public event EventHandler<PeersFoundEventArgs> PeersFound {
            add { }
            remove { }
        }

        public event EventHandler StateChanged {
            add { }
            remove { }
        }
#pragma warning restore 0067

        public TimeSpan AnnounceInterval { get; }
        public bool Disposed => false;
        public TimeSpan MinimumAnnounceInterval { get; }
        public int NodeCount => 0;
        public ITransferMonitor Monitor { get; } = new NullTransferMonitor ();

        public DhtState State => DhtState.NotReady;

        public void Add (IEnumerable<ReadOnlyMemory<byte>> nodes)
        {

        }

        public void Announce (InfoHash infoHash, int port)
        {

        }

        public void Dispose ()
        {

        }

        public void GetPeers (InfoHash infoHash)
        {

        }

        public Task<ReadOnlyMemory<byte>> SaveNodesAsync ()
        {
            return Task.FromResult (ReadOnlyMemory<byte>.Empty);
        }

        public Task SetListenerAsync (IDhtListener listener)
        {
            return Task.CompletedTask;
        }

        public Task StartAsync ()
        {
            return Task.CompletedTask;
        }

        public Task StartAsync (ReadOnlyMemory<byte> initialNodes)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync ()
        {
            return Task.CompletedTask;
        }
    }
}
