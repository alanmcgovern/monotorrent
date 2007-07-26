using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Common
{
    public class TorrentCreatorAsyncResult : IAsyncResult
    {
        #region Member Variables

        private bool aborted;
        private object asyncState;
        private AsyncCallback callback;
        private bool isCompleted;
        private Exception savedException;
        private ManualResetEvent waitHandle;

        #endregion Member Variables


        #region Properties

        public object AsyncState
        {
            get { return asyncState; }
        }

        public bool Aborted
        {
            get { return this.aborted; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return waitHandle; }
        }

        internal AsyncCallback Callback
        {
            get { return callback; }
        }

        public bool CompletedSynchronously
        {
            get { return false; }
        }

        public bool IsCompleted
        {
            get { return isCompleted; }
            internal set { isCompleted = value; }
        }

        internal Exception SavedException
        {
            get { return this.savedException; }
            set { this.savedException = value; }
        }

        #endregion Properties


        #region Constructors

        public TorrentCreatorAsyncResult(object asyncState, AsyncCallback callback)
        {
            this.asyncState = asyncState;
            this.callback = callback;
            this.waitHandle = new ManualResetEvent(false);
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
