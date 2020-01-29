//
// ConnectionConverter.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.Net;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent.Client.Connections
{
    static class ConnectionConverter
    {
        public static IConnection2 Convert (IConnection connection)
        {
            if (connection is null)
                return null;

            if (connection is IConnection2 v2)
                return v2;
            return new V1ToV2Converter (connection);
        }
    }

    class V1ToV2Converter : IConnection2
    {
        IConnection Connection { get; }

        public byte[] AddressBytes => Connection.AddressBytes;

        public bool Connected => Connection.Connected;

        public bool CanReconnect => Connection.CanReconnect;

        public bool IsIncoming => Connection.IsIncoming;

        public EndPoint EndPoint => Connection.EndPoint;

        public Uri Uri => Connection.Uri;

        public V1ToV2Converter (IConnection connection)
        {
            Connection = connection;
        }

        public async ReusableTask ConnectAsync ()
        {
            await Connection.ConnectAsync ();
        }

        public async ReusableTask<int> ReceiveAsync (byte[] buffer, int offset, int count)
        {
            return await Connection.ReceiveAsync (buffer, offset, count);
        }

        public async ReusableTask<int> SendAsync (byte[] buffer, int offset, int count)
        {
            return await Connection.SendAsync (buffer, offset, count);
        }

        Task IConnection.ConnectAsync ()
        {
            return Connection.ConnectAsync ();
        }

        Task<int> IConnection.ReceiveAsync (byte[] buffer, int offset, int count)
        {
            return Connection.ReceiveAsync (buffer, offset, count);
        }

        Task<int> IConnection.SendAsync (byte[] buffer, int offset, int count)
        {
            return Connection.SendAsync (buffer, offset, count);
        }

        public void Dispose ()
        {
            Connection.Dispose ();
        }
    }
}
