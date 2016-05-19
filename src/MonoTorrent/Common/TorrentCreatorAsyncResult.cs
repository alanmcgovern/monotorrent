using System;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Common
{
    internal class TorrentCreatorAsyncResult : AsyncResult
    {
        public TorrentCreatorAsyncResult(AsyncCallback callback, object asyncState)
            : base(callback, asyncState)
        {
        }

        public bool Aborted { get; set; }

        internal BEncodedDictionary Dictionary { get; set; }
    }
}