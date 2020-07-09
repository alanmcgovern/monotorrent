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
using System.IO;

using MonoTorrent.Logging;

using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    class FileStreamBuffer : IDisposable
    {
        static readonly Logger logger = Logger.Create ();

        internal readonly struct RentedStream : IDisposable
        {
            internal readonly ITorrentFileStream Stream;

            public RentedStream (ITorrentFileStream stream)
            {
                Stream = stream;
                stream?.Rent ();
            }

            public void Dispose ()
            {
                Stream?.Release ();
            }
        }

        // A list of currently open filestreams. Note: The least recently used is at position 0
        // The most recently used is at the last position in the array
        readonly int MaxStreams;

        public int Count => Streams.Count;

        Func<ITorrentFileInfo, FileAccess, ITorrentFileStream> StreamCreator { get; }
        Dictionary<ITorrentFileInfo, ITorrentFileStream> Streams { get; }
        List<ITorrentFileInfo> UsageOrder { get; }

        internal FileStreamBuffer (Func<ITorrentFileInfo, FileAccess, ITorrentFileStream> streamCreator, int maxStreams)
        {
            StreamCreator = streamCreator;
            MaxStreams = maxStreams;
            Streams = new Dictionary<ITorrentFileInfo, ITorrentFileStream> (maxStreams);
            UsageOrder = new List<ITorrentFileInfo> ();
        }

        internal async ReusableTask<bool> CloseStreamAsync (ITorrentFileInfo file)
        {
            using var rented = GetStream (file);
            if (rented.Stream != null) {
                await rented.Stream.FlushAsync ();
                CloseAndRemove (file, rented.Stream);
                return true;
            }

            return false;
        }

        internal async ReusableTask FlushAsync (ITorrentFileInfo file)
        {
            using var rented = GetStream (file);
            if (rented.Stream != null)
                await rented.Stream.FlushAsync ();
        }

        internal RentedStream GetStream (ITorrentFileInfo file)
        {
            if (Streams.TryGetValue (file, out ITorrentFileStream stream))
                return new RentedStream (stream);
            return new RentedStream (null);
        }

        internal async ReusableTask<RentedStream> GetStreamAsync (ITorrentFileInfo file, FileAccess access)
        {
            if (!Streams.TryGetValue (file, out ITorrentFileStream s))
                s = null;

            if (s != null) {
                // If we are requesting write access and the current stream does not have it
                if (((access & FileAccess.Write) == FileAccess.Write) && !s.CanWrite) {
                    logger.InfoFormatted ("Didn't have write permission for {0} - reopening", file.Path);
                    CloseAndRemove (file, s);
                    s = null;
                } else {
                    // Place the filestream at the end so we know it's been recently used
                    Streams.Remove (file);
                    Streams.Add (file, s);
                }
            }

            if (s == null) {
                if (!File.Exists (file.FullPath)) {
                    if (!string.IsNullOrEmpty (Path.GetDirectoryName (file.FullPath)))
                        Directory.CreateDirectory (Path.GetDirectoryName (file.FullPath));
                    NtfsSparseFile.CreateSparse (file.FullPath, file.Length);
                }
                s = StreamCreator(file, access);

                // Ensure that we truncate existing files which are too large
                if (s.Length > file.Length) {
                    if (!s.CanWrite) {
                        s.Dispose ();
                        s = StreamCreator(file, FileAccess.ReadWrite);
                    }
                    await s.SetLengthAsync (file.Length);
                }

                Add (file, s);
            }

            return new RentedStream (s);
        }

        void Add (ITorrentFileInfo file, ITorrentFileStream stream)
        {
            logger.InfoFormatted ("Opening filestream: {0}", file.FullPath);

            if (MaxStreams != 0 && Streams.Count >= MaxStreams) {
                for (int i = 0; i < UsageOrder.Count; i++) {
                    if (!Streams[UsageOrder[i]].Rented) {
                        CloseAndRemove (UsageOrder[i], Streams[UsageOrder[i]]);
                        break;
                    }
                }
            }
            Streams.Add (file, stream);
            UsageOrder.Add (file);
        }

        void CloseAndRemove (ITorrentFileInfo file, ITorrentFileStream s)
        {
            logger.InfoFormatted ("Closing and removing: {0}", file.Path);
            Streams.Remove (file);
            UsageOrder.Remove (file);
            s.Dispose ();
        }

        public void Dispose ()
        {
            foreach (var stream in Streams)
                stream.Value.Dispose ();

            Streams.Clear ();
            UsageOrder.Clear ();
        }
    }
}
