//
// TorrentFileStream.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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

namespace MonoTorrent.Client
{
    class TorrentFileStream : FileStream, ITorrentFileStream
    {
        bool disposed;
        bool rented;

        public TorrentFileStream (string path, FileAccess access)
            : base (path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1, FileOptions.Asynchronous | FileOptions.RandomAccess)
        {
        }

        protected override void Dispose (bool disposing)
        {
            disposed = true;
            base.Dispose (disposing);
        }

        bool ITorrentFileStream.Disposed => disposed;

        bool ITorrentFileStream.Rented => rented;

        async ReusableTask ITorrentFileStream.FlushAsync ()
        {
            await FlushAsync ();
        }

        async ReusableTask<int> ITorrentFileStream.ReadAsync (byte[] buffer, int offset, int count)
        {
            if (!rented)
                throw new InvalidOperationException ("Cannot read from the stream without renting it");
            return await ReadAsync (buffer, offset, count);
        }

        ReusableTask ITorrentFileStream.SeekAsync (long position)
        {
            Seek (position, SeekOrigin.Begin);
            return ReusableTask.CompletedTask;
        }

        public ReusableTask SetLengthAsync (long length)
        {
            if (!rented)
                throw new InvalidOperationException ("Cannot set the stream length without renting it");

            SetLength (length);
            return ReusableTask.CompletedTask;
        }

        async ReusableTask ITorrentFileStream.WriteAsync (byte[] buffer, int offset, int count)
        {
            if (!rented)
                throw new InvalidOperationException ("Cannot write to the stream without renting it");
            await WriteAsync (buffer, offset, count);
        }

        void ITorrentFileStream.Rent ()
        {
            if (rented)
                throw new InvalidOperationException ("This stream is already in use");
            rented = true;
        }

        void ITorrentFileStream.Release ()
        {
            if (!rented)
                throw new InvalidOperationException ("This stream has not been rented");
            rented = false;
        }
    }
}
