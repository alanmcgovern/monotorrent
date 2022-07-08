//
// AnnounceTask.cs
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


using System.Collections.Generic;
using System.Threading.Tasks;

using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class AnnounceTask
    {
        readonly NodeId infoHash;
        readonly DhtEngine engine;
        readonly int port;

        public AnnounceTask (DhtEngine engine, InfoHash infoHash, int port)
            : this (engine, new NodeId (infoHash), port)
        {

        }

        public AnnounceTask (DhtEngine engine, NodeId infoHash, int port)
        {
            this.engine = engine;
            this.infoHash = infoHash;
            this.port = port;
        }

        public async Task ExecuteAsync ()
        {
            DhtEngine.MainLoop.CheckThread ();

            var getpeers = new GetPeersTask (engine, infoHash);
            IEnumerable<Node> nodes = await getpeers.ExecuteAsync ();

            var announceTasks = new List<Task> ();
            foreach (Node n in nodes) {
                if (n.Token != null) {
                    var query = new AnnouncePeer (engine.LocalId, infoHash, port, n.Token);
                    announceTasks.Add (engine.SendQueryAsync (query, n));
                }
            }
            await Task.WhenAll (announceTasks);
        }
    }
}
