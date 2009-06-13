//
// LocalPeerManager.cs
//
// Authors:
//   Jared Hendry hendry.jared@gmail.com
//
// Copyright (C) 2008 Jared Hendry
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using MonoTorrent;
using MonoTorrent.Common;
using MonoTorrent.Client;

namespace MonoTorrent.Client
{
    class LocalPeerManager : IDisposable
    {
        private const int port = 6771;
        
        private UdpClient socket;
        private IPEndPoint ep;

        public LocalPeerManager()
        {
            socket = new UdpClient();
            ep = new IPEndPoint(IPAddress.Broadcast, port);
        }

        public void Broadcast(TorrentManager manager)
        {
            if (manager.HasMetadata && manager.Torrent.IsPrivate)
                return;
            
            string message = String.Format("BT-SEARCH * HTTP/1.1\r\nHost: 239.192.152.143:6771\r\nPort: {0}\r\nInfohash: {1}\r\n\r\n\r\n", manager.Engine.Settings.ListenPort, manager.InfoHash.ToHex());
            byte[] data = Encoding.ASCII.GetBytes(message);
			try
			{
				socket.Send(data, data.Length, ep);
			}
			catch
			{
				// If data can't be sent, just ignore the error
			}
        }

        public void Dispose()
        {
            socket.Close();
        }
    }
}
