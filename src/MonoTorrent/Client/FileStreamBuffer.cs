using System;
using System.Collections.Generic;
using System.IO;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class FileStreamBuffer : IDisposable
    {
        // A list of currently open filestreams. Note: The least recently used is at position 0
        // The most recently used is at the last position in the array
        private readonly int maxStreams;

        public FileStreamBuffer(int maxStreams)
        {
            this.maxStreams = maxStreams;
            Streams = new List<TorrentFileStream>(maxStreams);
        }

        public int Count
        {
            get { return Streams.Count; }
        }

        public List<TorrentFileStream> Streams { get; }

        #region IDisposable Members

        public void Dispose()
        {
            Streams.ForEach(delegate(TorrentFileStream s) { s.Dispose(); });
            Streams.Clear();
        }

        #endregion

        private void Add(TorrentFileStream stream)
        {
            Logger.Log(null, "Opening filestream: {0}", stream.Path);

            // If we have our maximum number of streams open, just dispose and dump the least recently used one
            if (maxStreams != 0 && Streams.Count >= Streams.Capacity)
            {
                Logger.Log(null, "We've reached capacity: {0}", Streams.Count);
                CloseAndRemove(Streams[0]);
            }
            Streams.Add(stream);
        }

        public TorrentFileStream FindStream(string path)
        {
            for (var i = 0; i < Streams.Count; i++)
                if (Streams[i].Path == path)
                    return Streams[i];
            return null;
        }

        internal TorrentFileStream GetStream(TorrentFile file, FileAccess access)
        {
            var s = FindStream(file.FullPath);

            if (s != null)
            {
                // If we are requesting write access and the current stream does not have it
                if (((access & FileAccess.Write) == FileAccess.Write) && !s.CanWrite)
                {
                    Logger.Log(null, "Didn't have write permission - reopening");
                    CloseAndRemove(s);
                    s = null;
                }
                else
                {
                    // Place the filestream at the end so we know it's been recently used
                    Streams.Remove(s);
                    Streams.Add(s);
                }
            }

            if (s == null)
            {
                if (!File.Exists(file.FullPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath));
                    SparseFile.CreateSparse(file.FullPath, file.Length);
                }
                s = new TorrentFileStream(file, FileMode.OpenOrCreate, access, FileShare.Read);

                // Ensure that we truncate existing files which are too large
                if (s.Length > file.Length)
                {
                    if (!s.CanWrite)
                    {
                        s.Close();
                        s = new TorrentFileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    }
                    s.SetLength(file.Length);
                }

                Add(s);
            }

            return s;
        }

        internal bool CloseStream(string path)
        {
            var s = FindStream(path);
            if (s != null)
                CloseAndRemove(s);

            return s != null;
        }

        private void CloseAndRemove(TorrentFileStream s)
        {
            Logger.Log(null, "Closing and removing: {0}", s.Path);
            Streams.Remove(s);
            s.Dispose();
        }
    }
}