using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;

namespace SampleClient
{
    class VLCStream
    {
        ClientEngine Engine { get; }
        string DownloadDirectory { get; }

        public VLCStream (ClientEngine engine)
        {
            DownloadDirectory = "streaming_cache";
            Engine = engine;
        }

        internal async Task StreamAsync (InfoHash infoHash)
            => await StreamAsync (await Engine.AddStreamingAsync (new MagnetLink (infoHash), DownloadDirectory));

        internal async Task StreamAsync (MagnetLink magnetLink)
            => await StreamAsync (await Engine.AddStreamingAsync (magnetLink, DownloadDirectory));

        internal async Task StreamAsync (Torrent torrent)
            => await StreamAsync (await Engine.AddStreamingAsync (torrent, DownloadDirectory));

        async Task StreamAsync (TorrentManager manager)
        {
            await manager.StartAsync ();
            await manager.WaitForMetadataAsync ();

            var largestFile = manager.Files.OrderBy (t => t.Length).Last ();
            var stream = await manager.StreamProvider.CreateHttpStreamAsync (largestFile);
            Process.Start (@"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe", stream.Uri.ToString ()).WaitForExit ();
        }
    }
}