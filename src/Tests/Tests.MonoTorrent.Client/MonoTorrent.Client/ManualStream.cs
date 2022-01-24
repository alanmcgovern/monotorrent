//
// ManualStream.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using System.Threading.Tasks;

namespace MonoTorrent.Client.PieceWriters
{
    class ManualStream : Stream
    {
        bool canWrite;
        long length;
        long position;

        public override bool CanSeek => true;
        public override bool CanRead => true;
        public override bool CanWrite => canWrite;

        public bool Disposed { get; private set; }

        public override long Length => length;

        public override long Position {
            get => position;
            set => position = value;
        }

        public override void SetLength (long value)
        {
            length = value;
        }

        public TaskCompletionSource<int> WriteTcs { get; set; }

        public ManualStream (ITorrentManagerFile file, FileAccess access)
        {
            canWrite = access.HasFlag (FileAccess.Write);
        }

        protected override void Dispose (bool disposing)
        {
            Disposed = true;
            WriteTcs?.TrySetException (new ObjectDisposedException (nameof (ManualStream)));
        }

        public override void Flush ()
        {

        }

        public override int Read (byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override long Seek (long offset, SeekOrigin origin)
        {
            position = offset;
            return offset;
        }

        public override void Write (byte[] buffer, int offset, int count)
        {
            if (WriteTcs != null)
                WriteTcs.Task.GetAwaiter ().GetResult ();
        }
    }
}
