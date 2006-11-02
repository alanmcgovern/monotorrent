using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    internal struct RateLimiter
    {
        #region Member Variables
        public int SavedErrorDownload;
        public int SavedErrorUpload;
        public int DownloadChunks;
        public int UploadChunks;
        #endregion


        #region Methods
        /// <summary>
        /// Method which calculates how many chunks of data we are able to send to allow for rate limiting (if enabled)
        /// </summary>
        public void UpdateDownloadChunks(int maxDownloadSpeed, int maxUploadSpeed, int actualDownloadSpeed, int actualUploadSpeed)
        {
            int errorRateDown = maxDownloadSpeed - actualDownloadSpeed;
            int changeAmountDown = (int)(0.4 * errorRateDown + 0.6 * this.SavedErrorDownload);
            this.SavedErrorDownload = errorRateDown;

            int errorRateUp = maxUploadSpeed - actualUploadSpeed;
            int changeAmountUp = (int)(0.4 * errorRateUp + 0.6 * this.SavedErrorUpload);
            this.SavedErrorUpload = errorRateUp;


            int increaseAmount = (int)((maxDownloadSpeed + this.SavedErrorDownload) / ConnectionManager.ChunkLength);
            Interlocked.Add(ref this.DownloadChunks, increaseAmount);
            if (this.DownloadChunks > (increaseAmount * 3))
                Interlocked.Exchange(ref this.DownloadChunks, (increaseAmount * 3));

            increaseAmount = (int)((maxUploadSpeed + this.SavedErrorUpload) / ConnectionManager.ChunkLength);
            Interlocked.Add(ref this.UploadChunks, increaseAmount);
            if (this.UploadChunks > increaseAmount * 3)
                Interlocked.Exchange(ref this.UploadChunks, (increaseAmount * 3));
        }
        #endregion
    }
}
