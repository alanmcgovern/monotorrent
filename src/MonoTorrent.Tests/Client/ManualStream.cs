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

using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    class ManualStream : ITorrentFileStream
    {
        public bool CanWrite { get; private set; }

        public bool Disposed { get; private set; }

        public long Length { get; private set; }

        public bool Rented { get; private set; }

        public long Position { get; private set; }

        public ReusableTaskCompletionSource<int> WriteTcs { get; set; }

        public ManualStream (ITorrentFileInfo file, FileAccess access)
        {
            CanWrite = access.HasFlag (FileAccess.Write);
        }

        public void Dispose ()
        {
            Disposed = true;
            WriteTcs?.SetException (new ObjectDisposedException (nameof (ManualStream)));
        }

        public ReusableTask FlushAsync ()
        {
            throw new System.NotImplementedException ();
        }

        public ReusableTask<int> ReadAsync (byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException ();
        }

        public void Release ()
        {
            if (!Rented)
                throw new InvalidOperationException ();
            Rented = false;
        }

        public void Rent ()
        {
            if (Rented)
                throw new InvalidOperationException ();
            Rented = true;
        }

        public ReusableTask SeekAsync (long position)
        {
            if (!Rented)
                throw new InvalidOperationException ();
            Position = position;
            return ReusableTask.CompletedTask;
        }

        public ReusableTask SetLengthAsync (long length)
        {
            throw new System.NotImplementedException ();
        }

        public async ReusableTask WriteAsync (byte[] buffer, int offset, int count)
        {
            if (WriteTcs != null)
                await WriteTcs.Task;
        }
    }
}
