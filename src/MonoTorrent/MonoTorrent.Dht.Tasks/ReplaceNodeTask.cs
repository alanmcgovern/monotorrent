//
// ReplaceNodeTask.cs
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

using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class ReplaceNodeTask
    {
        readonly Bucket bucket;
        readonly DhtEngine engine;
        readonly Node newNode;

        public ReplaceNodeTask (DhtEngine engine, Bucket bucket, Node newNode)
        {
            this.engine = engine;
            this.bucket = bucket;
            this.newNode = newNode;
        }

        public async System.Threading.Tasks.Task Execute ()
        {
            if (bucket.Nodes.Count == 0)
                return;

            bucket.Changed ();
            bucket.SortBySeen ();

            if (bucket.Nodes[0].LastSeen < TimeSpan.FromMinutes (3)) {
                return;
            } else {
                Node oldest = bucket.Nodes[0];
                SendQueryEventArgs args = await engine.SendQueryAsync (new Ping (engine.LocalId), oldest);

                if (args.TimedOut) {
                    // If the node didn't respond and it's no longer in our bucket,
                    // we need to send a ping to the oldest node in the bucket
                    // Otherwise if we have a non-responder and it's still there, replace it!
                    int index = bucket.Nodes.IndexOf (oldest);
                    if (index < 0) {
                        await Execute ();
                    } else {
                        bucket.Nodes[index] = newNode;
                        return;
                    }
                } else {
                    await Execute ();
                }
            }
        }

    }
}
