using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriter
{
    public interface IPieceWriter : IDisposable
    {
        int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count);
        
        void Write(PieceData data);

        void CloseFileStreams(TorrentManager manager);

        void Flush(TorrentManager manager);
    }
}
