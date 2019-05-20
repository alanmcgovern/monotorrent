using System;
using System.Threading.Tasks;

using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public static class TorrentManagerExtensions
    {
        public static Task WaitForState(this TorrentManager manager, TorrentState state)
        {
            var tcs = new TaskCompletionSource<object> ();
            manager.TorrentStateChanged += (o, e) => {
                if (e.NewState == state)
                    tcs.TrySetResult (null);
            };

            return tcs.Task;
        }
    }
}
