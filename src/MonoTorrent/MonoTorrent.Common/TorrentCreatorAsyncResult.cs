using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Common
{
    public class TorrentCreatorAsyncResult : AsyncResult
    {
        #region Member Variables

        private bool aborted;
        private BEncodedDictionary dictionary;

        #endregion Member Variables


        #region Properties

        public bool Aborted
        {
            get { return this.aborted; }
        }

        internal BEncodedDictionary Dictionary
        {
            get { return dictionary; }
            set { dictionary = value; }
        }

        #endregion Properties


        #region Constructors

        public TorrentCreatorAsyncResult(AsyncCallback callback, object asyncState)
            : base(callback, asyncState)
        {

        }

        #endregion Constructors


        #region Methods

        public void Abort()
        {
            this.aborted = true;
        }

        #endregion Methods
    }

}
