using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Common
{
    internal class TorrentCreatorAsyncResult : AsyncResult
    {
        private bool aborted;
        private BEncodedDictionary dictionary;

        public bool Aborted
        {
            get { return aborted; }
            set { aborted = value; }
        }

        internal BEncodedDictionary Dictionary
        {
            get { return dictionary; }
            set { dictionary = value; }
        }

        public TorrentCreatorAsyncResult(AsyncCallback callback, object asyncState)
            : base(callback, asyncState)
        {
        }
    }
}