//
// RefreshBucketTask.cs
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


using System.Threading.Tasks;

using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class RefreshBucketTask
    {
        readonly Bucket bucket;
        readonly DhtEngine engine;

        public RefreshBucketTask (DhtEngine engine, Bucket bucket)
        {
            this.engine = engine;
            this.bucket = bucket;
        }

        public async Task Execute ()
        {
            if (bucket.Nodes.Count == 0)
                return;

            bucket.SortBySeen ();

            foreach (Node node in bucket.Nodes.ToArray ()) {
                var message = new FindNode (engine.LocalId, node.Id);

                SendQueryEventArgs args = await engine.SendQueryAsync (message, node);
                if (!args.TimedOut)
                    return;
            }
        }
    }
}
