//
// UdpListener.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Alan McGovern
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
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Dht.Messages;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Dht.Listeners
{
    public class UdpListener : DhtListener
    {

        private UdpClient client;

        public UdpListener(IPEndPoint endpoint)
            :base(endpoint)
        {
            
        }

        private void EndReceive(IAsyncResult result)
        {
            try
            {
                IPEndPoint e = new IPEndPoint(IPAddress.Any, Endpoint.Port);
                byte[] buffer = client.EndReceive(result, ref e);

                RaiseMessageReceived(buffer, e);
                client.BeginReceive(EndReceive, null);
            }
            catch (ObjectDisposedException)
            {
                // Ignore, we're finished!
            }
            catch (SocketException)
            {
                client.Close();
                if (Status == ListenerStatus.Listening)
                {
                    client = new UdpClient(Endpoint);
                    client.BeginReceive(EndReceive, null);
                }
            }
        }

        public override void Send(byte[] buffer, IPEndPoint endpoint)
        {
            try
            {
               if (endpoint.Address != IPAddress.Any)
                    client.Send(buffer, buffer.Length, endpoint);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);// FIXME: Shoulnd't need a try/catch
            }
        }

        public override void Start()
        {
            try
            {
                client = new UdpClient(Endpoint);
                client.BeginReceive(EndReceive, null);
                RaiseStatusChanged(ListenerStatus.Listening);
            }
            catch (SocketException)
            {
                RaiseStatusChanged(ListenerStatus.PortNotFree);
            }
            catch (ObjectDisposedException)
            {
                // Do Nothing
            }
        }

        public override void Stop()
        {
            try
            {
                client.Close();
            }
            catch
            {
                // FIXME: Not needed
            }
        }
    }
}
