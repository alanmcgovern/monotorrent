using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    public struct ReaderLock : IDisposable
    {
        public ReaderWriterLock Locker;

        public ReaderLock(ReaderWriterLock locker)
        {
            Locker = locker;
            locker.AcquireReaderLock(5000);
        }

        public void Dispose()
        {
            Locker.ReleaseReaderLock();
        }
    }
}




