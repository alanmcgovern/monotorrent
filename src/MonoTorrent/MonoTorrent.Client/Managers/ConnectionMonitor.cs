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


namespace MonoTorrent.Client
{
    /// <summary>
    /// This class is used to track upload/download speed and bytes uploaded/downloaded for each connection
    /// </summary>
    public class ConnectionMonitor
    {
        #region Member Variables

        internal SpeedMonitor DataDown { get; }
        internal SpeedMonitor DataUp { get; }
        internal SpeedMonitor ProtocolDown { get; }
        internal SpeedMonitor ProtocolUp { get; }

        #endregion Member Variables


        #region Public Properties

        public long DataBytesDownloaded => DataDown.Total;

        public long DataBytesUploaded => DataUp.Total;

        public long DownloadSpeed => DataDown.Rate + ProtocolDown.Rate;

        public long ProtocolBytesDownloaded => ProtocolDown.Total;

        public long ProtocolBytesUploaded => ProtocolUp.Total;

        public long UploadSpeed => DataUp.Rate + ProtocolUp.Rate;

        #endregion Public Properties


        #region Constructors

        internal ConnectionMonitor ()
            : this (12)
        {

        }

        internal ConnectionMonitor (int averagingPeriod)
        {
            DataDown = new SpeedMonitor (averagingPeriod);
            DataUp = new SpeedMonitor (averagingPeriod);
            ProtocolDown = new SpeedMonitor (averagingPeriod);
            ProtocolUp = new SpeedMonitor (averagingPeriod);
        }

        #endregion


        #region Methods

        internal void Reset ()
        {
            DataDown.Reset ();
            DataUp.Reset ();
            ProtocolDown.Reset ();
            ProtocolUp.Reset ();
        }

        internal void Tick ()
        {
            DataDown.Tick ();
            DataUp.Tick ();
            ProtocolDown.Tick ();
            ProtocolUp.Tick ();
        }

        #endregion
    }
}
