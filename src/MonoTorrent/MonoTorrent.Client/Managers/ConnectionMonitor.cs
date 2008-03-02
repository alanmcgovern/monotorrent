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
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class is used to track upload/download speed and bytes uploaded/downloaded for each connection
    /// </summary>
    public class ConnectionMonitor
    {
        #region Member Variables

        private SpeedMonitor dataDown;
        private SpeedMonitor dataUp;
        private SpeedMonitor protocolDown;
        private SpeedMonitor protocolUp;

        #endregion Member Variables


        #region Public Properties

        public long DataBytesDownloaded
        {
            get { return dataDown.Total; }
        }

        public long DataBytesUploaded
        {
            get { return dataUp.Total; }
        }

        public int DownloadSpeed
        {
            get { return dataDown.Rate + protocolDown.Rate; }
        }

        public long ProtocolBytesDownloaded
        {
            get { return protocolDown.Rate; }
        }

        public long ProtocolBytesUploaded
        {
            get { return protocolUp.Rate; }
        }

        public int UploadSpeed
        {
            get { return dataUp.Rate + protocolUp.Rate; }
        }

        #endregion Public Properties


        #region Constructors

        internal ConnectionMonitor()
            : this(12)
        {

        }

        internal ConnectionMonitor(int averagingPeriod)
        {
            dataDown = new SpeedMonitor(averagingPeriod);
            dataUp = new SpeedMonitor(averagingPeriod);
            protocolDown = new SpeedMonitor(averagingPeriod);
            protocolUp = new SpeedMonitor(averagingPeriod);
        }

        #endregion


        #region Methods

        internal void BytesSent(int bytesUploaded, TransferType type)
        {
            if (type == TransferType.Data)
                dataUp.AddDelta(bytesUploaded);
            else
                protocolUp.AddDelta(bytesUploaded);
        }

        internal void BytesReceived(int bytesDownloaded, TransferType type)
        {
            if (type == TransferType.Data)
                dataDown.AddDelta(bytesDownloaded);
            else
                protocolDown.AddDelta(bytesDownloaded);
        }

        internal void Reset()
        {
            dataDown.Reset();
            dataUp.Reset();
            protocolDown.Reset();
            protocolUp.Reset();
        }

        internal void TickMonitors()
        {
            dataDown.Tick();
            dataUp.Tick();
            protocolDown.Tick();
            protocolUp.Tick();
        }

        #endregion
    }
}
