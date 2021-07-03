//
// SocketStream.cs
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


using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent
{
    public class SocketStream : Stream
    {
        class Data
        {
            public byte[] buffer;
            public int offset;
        }

        readonly CancellationTokenSource cancellation = new CancellationTokenSource ();
        readonly Queue<Tuple<TaskCompletionSource<Data>, IDisposable>> Writes = new Queue<Tuple<TaskCompletionSource<Data>, IDisposable>> ();

        public override bool CanRead => true;

        public override bool CanSeek => throw new NotSupportedException ();

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException ();

        public override long Position {
            get => throw new NotSupportedException ();
            set => throw new NotSupportedException ();
        }

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
            if (disposing)
                cancellation.Cancel ();
        }

        public override void Flush ()
            => throw new NotSupportedException ();

        public override int Read (byte[] buffer, int offset, int count)
            => throw new NotSupportedException ();

        public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Tuple<TaskCompletionSource<Data>, IDisposable> tcsPair;
            lock (Writes) {
                if (Writes.Count == 0) {
                    var tcs = new TaskCompletionSource<Data> ();
                    IDisposable reg = cancellation.Token.Register (() => tcs.TrySetCanceled ());
                    tcsPair = Tuple.Create (tcs, reg);
                    Writes.Enqueue (tcsPair);
                } else {
                    tcsPair = Writes.Peek ();
                }
            }

            try {
                var cache = await tcsPair.Item1.Task;
                var toCopy = Math.Min (count, cache.buffer.Length - cache.offset);
                Buffer.BlockCopy (cache.buffer, cache.offset, buffer, offset, toCopy);
                cache.offset += toCopy;
                if (cache.offset == cache.buffer.Length) {
                    lock (Writes) {
                        Writes.Dequeue ().Item2.Dispose ();
                    }
                }
                return toCopy;
            } catch {
                return 0;
            }
        }

        public override long Seek (long offset, SeekOrigin origin)
            => throw new NotSupportedException ();

        public override void SetLength (long value)
            => throw new NotSupportedException ();

        public override void Write (byte[] buffer, int offset, int count)
            => throw new NotSupportedException ();

        public override Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Tuple<TaskCompletionSource<Data>, IDisposable> tcsPair;
            lock (Writes) {
                if (Writes.Count == 0 || Writes.Peek ().Item1.Task.IsCompleted) {
                    var tcs = new TaskCompletionSource<Data> ();
                    IDisposable reg = cancellation?.Token.Register (() => tcs.TrySetCanceled ());

                    tcsPair = Tuple.Create (tcs, reg);
                    Writes.Enqueue (tcsPair);
                } else {
                    tcsPair = Writes.Peek ();
                }
            }

            var cache = new byte[count];
            Buffer.BlockCopy (buffer, offset, cache, 0, count);
            tcsPair.Item1.TrySetResult (new Data { buffer = cache });
            return Task.CompletedTask;
        }
    }
}
