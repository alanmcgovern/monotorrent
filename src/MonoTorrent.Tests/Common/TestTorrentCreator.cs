using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;
using MonoTorrent.Tests.Client;

namespace MonoTorrent.Tests.Common
{
    public class TestTorrentCreator : TorrentCreator
    {
        protected override PieceWriter CreateReader()
        {
            var writer = new TestWriter();
            writer.DontWrite = true;
            return writer;
        }
    }
}