//
// ConnectionMonitor.cs
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
using System.Net.Sockets;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class is used to track upload/download speed and bytes uploaded/downloaded for each connection
    /// </summary>
    internal class ConnectionMonitor
    {
        #region Member Variables
        private int tempSentCount;
        private int tempRecvCount;
        private int lastUpdateTime;

        private int uploadSpeedIndex;
        private double[] uploadSpeeds;

        private int downloadSpeedIndex;
        private double[] downloadSpeeds;

        /// <summary>
        /// Returns the total bytes downloaded from this peer
        /// </summary>
        public long BytesDownloaded
        {
            get { return this.bytesDownloaded; }
        }
        private long bytesDownloaded;

        /// <summary>
        /// Returns the total bytes uploaded to this peer
        /// </summary>
        public long BytesUploaded
        {
            get { return this.bytesUploaded; }
        }
        private long bytesUploaded;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new ConnectionMonitor
        /// </summary>
        public ConnectionMonitor()
        {
            this.lastUpdateTime = Environment.TickCount;
            this.uploadSpeeds = new double[16];
            this.downloadSpeeds = new double[16];
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Calculates the current average download speed
        /// </summary>
        /// <returns></returns>
        public double DownloadSpeed()
        {
            int count = 0;
            double total = 0;

            lock (this.downloadSpeeds)
                for (int i = 0; i < this.downloadSpeeds.Length; i++)
                {
                    if (this.downloadSpeeds[i] == 0)
                        count++;

                    total += this.downloadSpeeds[i];
                }

            if (count == this.downloadSpeeds.Length)
                count--;

            return total / (this.downloadSpeeds.Length - count);
        }


        /// <summary>
        /// Calculates the current average upload speed
        /// </summary>
        /// <returns></returns>
        public double UploadSpeed()
        {
            int count = 0;
            double total = 0;

            lock (this.uploadSpeeds)
                for (int i = 0; i < this.uploadSpeeds.Length; i++)
                {
                    if (this.uploadSpeeds[i] == 0)
                        count++;

                    total += this.uploadSpeeds[i];
                }

            if (count == this.uploadSpeeds.Length)
                count--;

            return total / (this.uploadSpeeds.Length - count);
        }


        /// <summary>
        /// Update the ConnectionManager with bytes uploaded
        /// </summary>
        /// <param name="bytesUploaded">Bytes uploaded in the last time period</param>
        public void BytesSent(int bytesUploaded)
        {
            lock (this.uploadSpeeds)
            {
                this.bytesUploaded += bytesUploaded;
                this.tempSentCount += bytesUploaded;   
            }
        }


        /// <summary>
        /// Update the ConnectionManager with bytes downloaded
        /// </summary>
        /// <param name="bytesDownloaded">Bytes downloaded in the last time period</param>
        public void BytesRecieved(int bytesDownloaded)
        {
            lock (this.downloadSpeeds)
            {
                this.bytesDownloaded += bytesDownloaded;
                this.tempRecvCount += bytesDownloaded;
            }
        }


        /// <summary>
        /// Called every time you want the stats to update. Ideally between every 0.5 and 2 seconds
        /// </summary>
        public void TimePeriodPassed()
        {
            lock (this.downloadSpeeds)
            {
                lock (this.uploadSpeeds)
                {
                    int currentTime = Environment.TickCount;
                    int difference = currentTime - this.lastUpdateTime;
                    
                    if (difference < 0)
                        difference = currentTime;   // Accounts for the rollover of Env.TickCount

                    if (difference < 500)
                        return;
#warning This isn't the best way to solve the issue of NaN download speeds. I think it's because it can take >20ms for the locking to allow a thread to run through
                    this.downloadSpeeds[this.downloadSpeedIndex++] = tempRecvCount / (difference / 1000.0);
                    this.uploadSpeeds[this.uploadSpeedIndex++] = tempSentCount / (difference / 1000.0);

                    if (this.downloadSpeedIndex == this.downloadSpeeds.Length)
                        this.downloadSpeedIndex = 0;

                    if (this.uploadSpeedIndex == this.uploadSpeeds.Length)
                        this.uploadSpeedIndex = 0;


                    this.tempRecvCount = 0;
                    this.tempSentCount = 0;
                    this.lastUpdateTime = currentTime;
                }
            }
        }
        #endregion
    }
}