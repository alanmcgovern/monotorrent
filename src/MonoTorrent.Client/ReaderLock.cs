using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
	internal struct ReaderLock : IDisposable
	{
		public ReaderWriterLock Locker;

		public ReaderLock(ReaderWriterLock locker)
		{
			Locker = locker;
			locker.AcquireReaderLock(1000);
		}

		public void Dispose()
		{
			Locker.ReleaseReaderLock();
		}
	}
}




