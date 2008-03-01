using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;

namespace MonoTorrent.Tracker
{
    public class RequestMonitor
    {
        private long lastUpdated;
        private ConnectionMonitor monitor;


        public int AnnounceRate
        {
            get
            {
                CheckUpdate();
                return monitor.DownloadSpeed;
            }
        }

        public int TotalAnnounces
        {
            get { return (int)monitor.DataBytesDownloaded; }
        }

        public int TotalScrapes
        {
            get { return (int)monitor.DataBytesUploaded; }
        }


        public RequestMonitor()
        {
            lastUpdated = 0;
            monitor = new ConnectionMonitor();
        }


        internal void AnnounceReceived()
        {
            CheckUpdate();
            monitor.BytesReceived(1, TransferType.Data);
        }

        private void CheckUpdate()
        {
            // In the general case, we skip taking the lock
            long difference = Environment.TickCount - lastUpdated;
            if (difference < 1000)
                return;

            lock (monitor)
            {
                // If two threads make it past the block above,
                // make sure that the second thread doesn't update
                // the monitor
                difference = Environment.TickCount - lastUpdated;
                if (difference < 1000)
                    return;

                lastUpdated = Environment.TickCount;
                monitor.TimePeriodPassed();
            }
        }

        public int ScrapeRate
        {
            get
            {
                CheckUpdate();
                return monitor.UploadSpeed;
            }
        }

        internal void ScrapeReceived()
        {
            CheckUpdate();
            monitor.BytesSent(1, TransferType.Data);
        }
    }
}
