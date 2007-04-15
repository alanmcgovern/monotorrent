using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
	public struct WriterLock : IDisposable
	{
		private bool upgraded;
		private LockCookie cookie;
		public ReaderWriterLock Locker;

		public WriterLock(ReaderWriterLock locker)
		{
			Locker = locker;
			upgraded = locker.IsReaderLockHeld;
			cookie = default(LockCookie);

			if (upgraded)
				cookie = locker.UpgradeToWriterLock(1000);
			else
				locker.AcquireWriterLock(1000);
		}

		public void Dispose()
		{
			if (upgraded)
				Locker.DowngradeFromWriterLock(ref cookie);
			else
				Locker.ReleaseWriterLock();
		}
	}
}
