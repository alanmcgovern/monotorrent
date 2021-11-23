//
// FileStreamBuffer.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    class FileStreamBuffer : IDisposable
    {
        internal readonly struct RentedStream : IDisposable
        {
            internal readonly Stream Stream;
            readonly ReusableExclusiveSemaphore.Releaser Releaser;

            public RentedStream (Stream stream, ReusableExclusiveSemaphore.Releaser releaser)
            {
                Stream = stream;
                Releaser = releaser;
            }

            public void Dispose ()
            {
                Releaser.Dispose ();
            }
        }

        class StreamData
        {
            public long LastUsedStamp = Stopwatch.GetTimestamp ();
            public ReusableExclusiveSemaphore Locker = new ReusableExclusiveSemaphore ();
            public Stream Stream;
        }

        // A list of currently open filestreams. Note: The least recently used is at position 0
        // The most recently used is at the last position in the array
        readonly int MaxStreams;

        public int Count { get; private set; }

        Func<ITorrentFileInfo, FileAccess, Stream> StreamCreator { get; }

        Dictionary<ITorrentFileInfo, StreamData> Streams { get; }

        internal FileStreamBuffer (Func<ITorrentFileInfo, FileAccess, Stream> streamCreator, int maxStreams)
        {
            StreamCreator = streamCreator;
            MaxStreams = maxStreams;
            Streams = new Dictionary<ITorrentFileInfo, StreamData> (maxStreams);
        }

        internal async ReusableTask<bool> CloseStreamAsync (ITorrentFileInfo file)
        {
            if (Streams.TryGetValue (file, out StreamData data)) {
                using var releaser = await data.Locker.EnterAsync ();
                if (data.Stream != null) {
                    data.Stream.Dispose ();
                    data.Stream = null;
                    Count--;
                    return true;
                }
            }

            return false;
        }

        internal async ReusableTask FlushAsync (ITorrentFileInfo file)
        {
            using var rented = await GetStream (file);
            if (rented.Stream != null)
                await rented.Stream.FlushAsync ();
        }

        internal async ReusableTask<RentedStream> GetStream (ITorrentFileInfo file)
        {
            if (Streams.TryGetValue (file, out StreamData data)) {
                var releaser = await data.Locker.EnterAsync ();
                if (data.Stream == null) {
                    releaser.Dispose ();
                } else {
                    data.LastUsedStamp = Stopwatch.GetTimestamp ();
                    return new RentedStream (data.Stream, releaser);
                }
            }
            return new RentedStream (null, default);
        }

        internal async ReusableTask<RentedStream> GetOrCreateStreamAsync (ITorrentFileInfo file, FileAccess access)
        {
            if (!Streams.TryGetValue (file, out StreamData data))
                data = Streams[file] = new StreamData ();

            var releaser = await data.Locker.EnterAsync ();
            if (data.Stream != null) {
                // If we are requesting write access and the current stream does not have it
                if (((access & FileAccess.Write) == FileAccess.Write) && !data.Stream.CanWrite) {
                    data.Stream.Dispose ();
                    data.Stream = null;
                    Count--;
                }
            }

            if (data.Stream == null) {
                if (!File.Exists (file.FullPath)) {
                    if (!string.IsNullOrEmpty (Path.GetDirectoryName (file.FullPath)))
                        Directory.CreateDirectory (Path.GetDirectoryName (file.FullPath));
                    NtfsSparseFile.CreateSparse (file.FullPath, file.Length);
                }
                data.Stream = StreamCreator (file, access);
                Count++;

                // Ensure that we truncate existing files which are too large
                if (data.Stream.Length > file.Length) {
                    if (!data.Stream.CanWrite) {
                        data.Stream.Dispose ();
                        data.Stream = StreamCreator (file, FileAccess.ReadWrite);
                    }
                    data.Stream.SetLength (file.Length);
                }
            }

            data.LastUsedStamp = Stopwatch.GetTimestamp ();
            MaybeRemoveOldestStream ();

            return new RentedStream (data.Stream, releaser);
        }

        async void MaybeRemoveOldestStream ()
        {
            if (MaxStreams != 0 && Count > MaxStreams) {
                var oldest = Streams.OrderBy (t => t.Value.LastUsedStamp).Where (t => t.Value.Stream != null).FirstOrDefault ();

                using (await oldest.Value.Locker.EnterAsync ()) {
                    oldest.Value.Stream.Dispose ();
                    oldest.Value.Stream = null;
                    Count--;
                }
            }
        }

        public void Dispose ()
        {
            foreach (var stream in Streams)
                stream.Value.Stream?.Dispose ();

            Streams.Clear ();
            Count = 0;
        }
    }
}
