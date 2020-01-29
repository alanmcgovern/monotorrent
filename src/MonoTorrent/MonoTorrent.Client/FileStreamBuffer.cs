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

namespace MonoTorrent.Client
{
    class FileStreamBuffer : IDisposable
    {
        // A list of currently open filestreams. Note: The least recently used is at position 0
        // The most recently used is at the last position in the array
        readonly int maxStreams;

        public int Count => Streams.Count;

        public List<TorrentFileStream> Streams { get; }

        public FileStreamBuffer (int maxStreams)
        {
            this.maxStreams = maxStreams;
            Streams = new List<TorrentFileStream> (maxStreams);
        }

        void Add (TorrentFileStream stream)
        {
            Logger.Log (null, "Opening filestream: {0}", stream.Path);

            // If we have our maximum number of streams open, just dispose and dump the least recently used one
            if (maxStreams != 0 && Streams.Count >= Streams.Capacity) {
                Logger.Log (null, "We've reached capacity: {0}", Streams.Count);
                CloseAndRemove (Streams[0]);
            }
            Streams.Add (stream);
        }

        public TorrentFileStream FindStream (string path)
        {
            for (int i = 0; i < Streams.Count; i++)
                if (Streams[i].Path == path)
                    return Streams[i];
            return null;
        }

        internal TorrentFileStream GetStream (TorrentFile file, FileAccess access)
        {
            TorrentFileStream s = FindStream (file.FullPath);

            if (s != null) {
                // If we are requesting write access and the current stream does not have it
                if (((access & FileAccess.Write) == FileAccess.Write) && !s.CanWrite) {
                    Logger.Log (null, "Didn't have write permission - reopening");
                    CloseAndRemove (s);
                    s = null;
                } else {
                    // Place the filestream at the end so we know it's been recently used
                    Streams.Remove (s);
                    Streams.Add (s);
                }
            }

            if (s == null) {
                if (!File.Exists (file.FullPath)) {
                    Directory.CreateDirectory (Path.GetDirectoryName (file.FullPath));
                    NtfsSparseFile.CreateSparse (file.FullPath, file.Length);
                }
                s = new TorrentFileStream (file, FileMode.OpenOrCreate, access, FileShare.ReadWrite);

                // Ensure that we truncate existing files which are too large
                if (s.Length > file.Length) {
                    if (!s.CanWrite) {
                        s.Close ();
                        s = new TorrentFileStream (file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                    }
                    s.SetLength (file.Length);
                }

                Add (s);
            }

            return s;
        }

        #region IDisposable Members

        public void Dispose ()
        {
            Streams.ForEach (delegate (TorrentFileStream s) { s.Dispose (); });
            Streams.Clear ();
        }

        #endregion

        internal bool CloseStream (string path)
        {
            TorrentFileStream s = FindStream (path);
            if (s != null)
                CloseAndRemove (s);

            return s != null;
        }

        void CloseAndRemove (TorrentFileStream s)
        {
            Logger.Log (null, "Closing and removing: {0}", s.Path);
            Streams.Remove (s);
            s.Dispose ();
        }
    }
}
