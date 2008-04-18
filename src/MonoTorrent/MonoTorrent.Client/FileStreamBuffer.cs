using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.IO;

namespace MonoTorrent.Client
{
    class FileStreamBuffer : IDisposable
    {
        // A list of currently open filestreams. Note: The least recently used is at position 0
        // The most recently used is at the last position in the array
        private List<TorrentFileStream> list;
        private int maxStreams;
		
		public int Count
		{
			get { return list.Count; }
		}
		
        public FileStreamBuffer(int maxStreams)
        {
            this.maxStreams = maxStreams;
            list = new List<TorrentFileStream>(maxStreams);
        }

        private void Add(TorrentFileStream stream)
        {
            Logger.Log (null, "Opening filestream: {0}", stream.Path);

            // If we have our maximum number of streams open, just dispose and dump the least recently used one
            if (maxStreams != 0 && list.Count >= list.Capacity)
            {
                Logger.Log (null, "We've reached capacity: {0}", list.Count);
                CloseAndRemove(list[0]);
            }
            list.Add(stream);
        }

        internal TorrentFileStream GetStream(TorrentFile file, string filePath, FileAccess access)
        {
            TorrentFileStream s = list.Find(delegate(TorrentFileStream stream) {
                return stream.Path == filePath;
            });

            if (s != null)
            {
                // If we are requesting write access and the current stream does not have it
                if (((access & FileAccess.Write) == FileAccess.Write) && !s.CanWrite)
                {
                    Logger.Log (null, "Didn't have write permission - reopening");
                    CloseAndRemove(s);
                    s = null;
                }
                else
                {
                    // Place the filestream at the end so we know it's been recently used
                    list.Remove(s);
                    list.Add(s);
                }
            }

            if (s == null)
            {
                s = new TorrentFileStream(filePath, FileMode.OpenOrCreate, access, FileShare.Read);
                Add(s);
            }

            return s;
        }

        #region IDisposable Members

        public void Dispose()
        {
            list.ForEach(delegate (TorrentFileStream s) { s.Dispose(); }); 
            list.Clear();
        }

        #endregion

        internal bool CloseStream(string path)
        {
            TorrentFileStream s = list.Find(delegate(TorrentFileStream stream) { return stream.Path == path; });
            if (s != null)
                CloseAndRemove(s);

            return s != null;
        }

        private void CloseAndRemove(TorrentFileStream s)
        {
            Logger.Log (null, "Closing and removing: {0}", s.Path);
            list.Remove(s);
            s.Dispose();
        }
    }
}
