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
    public class ConnectionMonitor
    {
        private const int ArraySize = 8;


        #region Member Variables
        private long bytesDownloaded;
        private long bytesUploaded;
        private double downloadSpeed;
        private int downloadSpeedIndex;
        private double[] downloadSpeeds;
        private int lastUpdateTime;
        private int tempSentCount;
        private int tempRecvCount;
        private double uploadSpeed;
        private int uploadSpeedIndex;
        private double[] uploadSpeeds;
        #endregion Member Variables

        #region Public Properties
        /// <summary>
        /// Returns the total bytes downloaded from this peer
        /// </summary>
        public long BytesDownloaded
        {
            get { return this.bytesDownloaded; }
        }


        /// <summary>
        /// Returns the total bytes uploaded to this peer
        /// </summary>
        public long BytesUploaded
        {
            get { return this.bytesUploaded; }
        }


        /// <summary>
        /// The current average download speed in bytes per second
        /// </summary>
        /// <returns></returns>
        public double DownloadSpeed
        {
            get { return this.downloadSpeed; }
        }


        /// <summary>
        /// The current average upload speed in byte/second
        /// </summary>
        /// <returns></returns>
        public double UploadSpeed
        {
            get { return this.uploadSpeed; }
        }
        #endregion Public Properties


        #region Constructors
        /// <summary>
        /// Creates a new ConnectionMonitor
        /// </summary>
        internal ConnectionMonitor()
        {
            this.lastUpdateTime = Environment.TickCount;
            this.uploadSpeeds = new double[ArraySize];
            this.downloadSpeeds = new double[ArraySize];
        }
        #endregion


        #region Methods
        /// <summary>
        /// Update the ConnectionManager with bytes uploaded
        /// </summary>
        /// <param name="bytesUploaded">Bytes uploaded in the last time period</param>
        internal void BytesSent(int bytesUploaded)
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
        internal void BytesReceived(int bytesDownloaded)
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
        internal void TimePeriodPassed()
        {
            lock (this.downloadSpeeds)
            {
                lock (this.uploadSpeeds)
                {
                    int count = 0;
                    double total = 0;
                    int currentTime = Environment.TickCount;
                    int difference = currentTime - this.lastUpdateTime;

                    if (difference < 0)
                        difference = currentTime;   // Accounts for the rollover of Env.TickCount


                    this.downloadSpeeds[this.downloadSpeedIndex++] = tempRecvCount / (difference / 1000.0);
                    this.uploadSpeeds[this.uploadSpeedIndex++] = tempSentCount / (difference / 1000.0);

                    if (this.downloadSpeedIndex == ArraySize)
                        this.downloadSpeedIndex = 0;

                    if (this.uploadSpeedIndex == ArraySize)
                        this.uploadSpeedIndex = 0;



                    for (int i = 0; i < this.downloadSpeeds.Length; i++)
                    {
                        if (this.downloadSpeeds[i] == 0)
                            count++;

                        total += this.downloadSpeeds[i];
                    }
                    if (count == ArraySize)
                        count--;

                    this.downloadSpeed = (total / (ArraySize - count));


                    count = 0;
                    total = 0;
                    for (int i = 0; i < this.uploadSpeeds.Length; i++)
                    {
                        if (this.uploadSpeeds[i] == 0)
                            count++;

                        total += this.uploadSpeeds[i];
                    }
                    if (count == this.uploadSpeeds.Length)
                        count--;

                    this.uploadSpeed = (total / (ArraySize - count));

                    this.tempRecvCount = 0;
                    this.tempSentCount = 0;
                    this.lastUpdateTime = currentTime;
                }
            }
        }
        #endregion
    }
}
