using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.IO;
using System.Threading;

namespace MonoTorrent.Client.PieceWriters
{
    public class EncryptedDiskWriter : PieceWriter
    {
        private Dictionary<TorrentFile, string> paths;
        private FileStreamBuffer streamsBuffer;

        public int OpenFiles
        {
            get { return streamsBuffer.Count; }
        }

        public EncryptedDiskWriter()
            : this(10)
        {

        }

        public EncryptedDiskWriter(int maxOpenFiles)
        {
            paths = new Dictionary<TorrentFile, string>();
            this.streamsBuffer = new FileStreamBuffer(maxOpenFiles);
        }

        public override void Close(TorrentManager manager)
        {
            for (int i = 0; i < manager.Torrent.Files.Length; i++)
                lock (manager.Torrent.Files[i])
                    streamsBuffer.CloseStream(GenerateFilePath(manager.Torrent.Files[i], manager.FileManager.BaseDirectory, manager.SavePath));
        }

        public override void Dispose()
        {
            streamsBuffer.Dispose();
        }

        protected virtual string GenerateFilePath(TorrentFile file, string baseDirectory, string savePath)
        {
            if (paths.ContainsKey(file))
                return paths[file];

            string path;

            path = Path.Combine(savePath, baseDirectory);
            path = Path.Combine(path, file.Path);

            if (!Directory.Exists(Path.GetDirectoryName(path)) && !string.IsNullOrEmpty(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            paths[file] = path;

            return path;
        }

        internal TorrentFileStream GetStream(FileManager manager, TorrentFile file, FileAccess access)
        {
            string filePath = GenerateFilePath(file, manager.BaseDirectory, manager.SavePath);
            lock (streamsBuffer)
                return streamsBuffer.GetStream(file, filePath, access);
        }

        public override int Read(BufferedIO data)
        {
            if (data == null)
                throw new ArgumentNullException("buffer");

            long offset = data.Offset;
            int count = data.Count;
            FileManager manager = data.Manager.FileManager;
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
                    bytesRead = s.Read(data.buffer.Array, data.buffer.Offset + totalRead, count - totalRead);
                    totalRead += bytesRead;
                    i++;
                }
            }
            //monitor.BytesSent(totalRead, TransferType.Data);
            DecryptEncrypt(data);
            data.ActualCount += totalRead;
            return totalRead;
        }

        public override void Write(BufferedIO data)
        {
            byte[] buffer = data.buffer.Array;
            long offset = data.Offset;
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

            DecryptEncrypt(data);

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
                    stream.Write(buffer, data.buffer.Offset + (int)totalWritten, (int)bytesWritten);

                    // Any further data should be written to the next available file
                    totalWritten += bytesWritten;
                    i++;
                }
            }
            ClientEngine.BufferManager.FreeBuffer(ref data.buffer);
            //monitor.BytesReceived((int)totalWritten, TransferType.Data);
        }

        public override void Flush(TorrentManager manager)
        {
            // No buffering done here
        }
        public override void Flush(TorrentManager manager, int pieceIndex)
        {
            // No buffering done here
        }

        // xor with 69
        private void DecryptEncrypt(BufferedIO data)
        {
            for (int i = 0; i < data.Buffer.Count; i++)
            {
                data.Buffer.Array[data.Buffer.Offset + i] ^= 69;
            }
        }


    }
}
