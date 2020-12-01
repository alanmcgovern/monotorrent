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
using System.Threading.Tasks;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
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

        public bool Disposed => false;

        public DhtState State => DhtState.NotReady;

        public void Add (BEncodedList nodes)
        {

        }

        public void Announce (InfoHash infohash, int port)
        {

        }

        public void Dispose ()
        {

        }

        public void GetPeers (InfoHash infohash)
        {

        }

        public Task<byte[]> SaveNodesAsync ()
        {
            return Task.FromResult (new byte[0]);
        }

        public Task StartAsync ()
        {
            return Task.CompletedTask;
        }

        public Task StartAsync (byte[] initialNodes)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync ()
        {
            return Task.CompletedTask;
        }
    }
}
