using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.IO;

namespace MonoTorrent.Client.PieceWriter
{
    public class DiskWriter : IPieceWriter
    {
        private FileStreamBuffer streamsBuffer;

        public int OpenFiles
        {
            get { return streamsBuffer.Count; }
        }

        public DiskWriter(int maxOpenFiles)
        {
            this.streamsBuffer = new FileStreamBuffer(maxOpenFiles);
        }

        public void CloseFileStreams(TorrentManager manager)
        {
            Array.ForEach<TorrentFile>(manager.Torrent.Files, delegate(TorrentFile f) { streamsBuffer.CloseStream(f); });
        }

        public void Dispose()
        {
            streamsBuffer.Dispose();
        }

        private static string GenerateFilePath(TorrentFile file, string baseDirectory, string savePath)
        {
            string path;

            path = Path.Combine(savePath, baseDirectory);
            path = Path.Combine(path, file.Path);

            if (!Directory.Exists(Path.GetDirectoryName(path)) && !string.IsNullOrEmpty(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            return path;
        }

        internal TorrentFileStream GetStream(FileManager manager, TorrentFile file, FileAccess access)
        {
            string filePath = GenerateFilePath(file, manager.BaseDirectory, manager.SavePath);
            return streamsBuffer.GetStream(file, filePath, access);
        }

        public int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > manager.FileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int bytesRead = 0;
            int totalRead = 0;

            for (i = 0; i < manager.Files.Length; i++)          // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < manager.Files[i].Length)           // the start of the data we want to read
                    break;

                offset -= manager.Files[i].Length;              // Offset now contains the index of the data we want
            }                                                   // to read from fileStream[i].

            while (totalRead < count)                           // We keep reading until we have read 'count' bytes.
            {
                if (i == manager.Files.Length)
                    break;

                lock (manager.Files[i])
                {
                    TorrentFileStream s = GetStream(manager, manager.Files[i], FileAccess.Read);
                    s.Seek(offset, SeekOrigin.Begin);
                    offset = 0; // Any further files need to be read from the beginning
                    bytesRead = s.Read(buffer, bufferOffset + totalRead, count - totalRead);
                    totalRead += bytesRead;
                    i++;
                }
            }
            //monitor.BytesSent(totalRead, TransferType.Data);
            return totalRead;
        }

        public void Write(PieceData data)
        {
            byte[] buffer = data.Buffer.Array;
            long offset = data.WriteOffset;
            int count = data.Count;

            FileManager manager = data.Id.TorrentManager.FileManager;
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > manager.FileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            long bytesWritten = 0;
            long totalWritten = 0;
            long bytesWeCanWrite = 0;

            for (i = 0; i < manager.Files.Length; i++)          // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < manager.Files[i].Length)           // the start of the data we want to write
                    break;

                offset -= manager.Files[i].Length;              // Offset now contains the index of the data we want
            }                                                   // to write to fileStream[i].

            while (totalWritten < count)                        // We keep writing  until we have written 'count' bytes.
            {
                lock (manager.Files[i])
                {
                    TorrentFileStream stream = GetStream(manager, manager.Files[i], FileAccess.ReadWrite);
                    stream.Seek(offset, SeekOrigin.Begin);

                    // Find the maximum number of bytes we can write before we reach the end of the file
                    bytesWeCanWrite = manager.Files[i].Length - offset;

                    // Any further files need to be written from the beginning of the file
                    offset = 0;

                    // If the amount of data we are going to write is larger than the amount we can write, just write the allowed
                    // amount and let the rest of the data be written with the next filestream
                    bytesWritten = ((count - totalWritten) > bytesWeCanWrite) ? bytesWeCanWrite : (count - totalWritten);

                    // Write the data
                    stream.Write(buffer, data.Buffer.Offset + (int)totalWritten, (int)bytesWritten);
                    stream.Flush();

                    // Any further data should be written to the next available file
                    totalWritten += bytesWritten;
                    i++;
                }
            }

            //monitor.BytesReceived((int)totalWritten, TransferType.Data);
        }

        public void Flush(TorrentManager manager)
        {
            // No buffering done here
        }
    }
}
