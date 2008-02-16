using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriters
{
	public abstract class PieceWriter
	{
		protected PieceWriter()
		{

		}

		public int ReadChunk(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
		{
			int read = 0;
			int totalRead = 0;

			while (totalRead != count)
			{
				read = Read(manager, buffer, bufferOffset + totalRead, offset + totalRead, count - totalRead);
				totalRead += read;

				if (read == 0)
					return totalRead;
			}

			return totalRead;
		}

		public abstract int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count);

		public abstract void Write(PieceData data);

		public abstract void CloseFileStreams(TorrentManager manager);

		public abstract  void Flush(TorrentManager manager);

		public virtual void Dispose()
		{

		}
	}
}