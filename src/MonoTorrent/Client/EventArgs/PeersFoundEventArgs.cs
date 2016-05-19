#if !DISABLE_DHT
//
// PeersFoundEventArgs.cs
//
// Authors:
//   Olivier Dufour <olivier.duff@gmail.com>
//
// Copyright (C) 2008 Olivier Dufour
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
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;

namespace MonoTorrent
{
    public class PeersFoundEventArgs : EventArgs
    {
        private List<Peer> peers;
        private InfoHash infoHash;
        
        public PeersFoundEventArgs(InfoHash infoHash, List<Peer> peers)
        {
            this.peers = peers;
            this.infoHash = infoHash;
        }
        public List<Peer> Peers
        {
            get { return peers; }
        }
        public InfoHash InfoHash
        {
            get { return infoHash; }
        }
    }
}
#endif