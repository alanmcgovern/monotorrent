using System;
using System.Threading;

namespace MonoTorrent.Common
{
    public class AsyncResult : IAsyncResult
    {
        #region Constructors

        public AsyncResult(AsyncCallback callback, object asyncState)
        {
            AsyncState = asyncState;
            Callback = callback;
            AsyncWaitHandle = new ManualResetEvent(false);
        }

        #endregion Constructors

        #region Member Variables

        #endregion Member Variables

        #region Properties

        public object AsyncState { get; }

        WaitHandle IAsyncResult.AsyncWaitHandle
        {
            get { return AsyncWaitHandle; }
        }

        protected internal ManualResetEvent AsyncWaitHandle { get; }

        internal AsyncCallback Callback { get; }

        public bool CompletedSynchronously { get; protected internal set; }

        public bool IsCompleted { get; protected internal set; }

        protected internal Exception SavedException { get; set; }

        #endregion Properties

        #region Methods

        protected internal void Complete()
        {
            Complete(SavedException);
        }

        protected internal void Complete(Exception ex)
        {
            // Ensure we only complete once - Needed because in encryption there could be
            // both a pending send and pending receive so if there is an error, both will
            // attempt to complete the encryption handshake meaning this is called twice.
            if (IsCompleted)
                return;

            SavedException = ex;
            CompletedSynchronously = false;
            IsCompleted = true;
            AsyncWaitHandle.Set();

            if (Callback != null)
                Callback(this);
        }

        #endregion Methods
    }
}