//
// RateLimiter.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


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
            maxDownloadSpeed = (int)(maxDownloadSpeed * 1.05);
            maxUploadSpeed = (int)(maxUploadSpeed * 1.05);
            int errorRateDown = maxDownloadSpeed - actualDownloadSpeed;
            int changeAmountDown = (int)(0.4 * errorRateDown + 0.6 * this.SavedErrorDownload);
            this.SavedErrorDownload = errorRateDown;

            int errorRateUp = maxUploadSpeed - actualUploadSpeed;
            int changeAmountUp = (int)(0.4 * errorRateUp + 0.6 * this.SavedErrorUpload);
            this.SavedErrorUpload = errorRateUp;


            int increaseAmount = (int)((maxDownloadSpeed + changeAmountDown) / ConnectionManager.ChunkLength);
            Interlocked.Add(ref this.DownloadChunks, increaseAmount);
            if (this.DownloadChunks > (maxDownloadSpeed * 1.2 / ConnectionManager.ChunkLength))
                Interlocked.Exchange(ref this.DownloadChunks, (int)(maxDownloadSpeed * 1.2 / ConnectionManager.ChunkLength));

            increaseAmount = (int)((maxUploadSpeed + changeAmountUp) / ConnectionManager.ChunkLength);
            Interlocked.Add(ref this.UploadChunks, increaseAmount);
            if (this.UploadChunks > (maxUploadSpeed * 1.2 / ConnectionManager.ChunkLength))
                Interlocked.Exchange(ref this.UploadChunks, (int)(maxUploadSpeed * 1.2 / ConnectionManager.ChunkLength));

            if (this.UploadChunks < (maxUploadSpeed / ConnectionManager.ChunkLength) / 2)
                Interlocked.Exchange(ref this.UploadChunks, (maxUploadSpeed / ConnectionManager.ChunkLength / 2));

            if (this.DownloadChunks < (maxDownloadSpeed / ConnectionManager.ChunkLength / 2))
                Interlocked.Exchange(ref this.DownloadChunks, (maxDownloadSpeed / ConnectionManager.ChunkLength / 2));

            if (maxDownloadSpeed == 0)
                DownloadChunks = 0;
            if (maxUploadSpeed == 0)
                UploadChunks = 0;
        }
        #endregion
    }
}
