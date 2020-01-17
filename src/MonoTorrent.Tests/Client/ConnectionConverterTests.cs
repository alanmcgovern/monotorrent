//
// ConnectionConverterTestes.cs
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

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client.Connections
{
    [TestFixture]
    public class ConnectionConverterTests
    {
        [Test]
        public void ConvertNull ()
        {
            Assert.IsNull (ConnectionConverter.Convert (null));
        }

        [Test]
        public void ConvertV1 ()
        {
            Assert.IsInstanceOf<V1ToV2Converter> (ConnectionConverter.Convert (new V1Connection ()));
        }

        [Test]
        public void ConvertV2 ()
        {
            var v2 = new V2Connection ();
            Assert.AreSame (v2, ConnectionConverter.Convert (v2));
        }

        class V2Connection : IConnection2
        {
            public byte[] AddressBytes => throw new NotImplementedException ();

            public bool Connected => throw new NotImplementedException ();

            public bool CanReconnect => throw new NotImplementedException ();

            public bool IsIncoming => throw new NotImplementedException ();

            public EndPoint EndPoint => throw new NotImplementedException ();

            public Uri Uri => throw new NotImplementedException ();

            public ReusableTask ConnectAsync ()
            {
                throw new NotImplementedException ();
            }

            public void Dispose ()
            {
                throw new NotImplementedException ();
            }

            public ReusableTask<int> ReceiveAsync (byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask<int> SendAsync (byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException ();
            }

            Task IConnection.ConnectAsync ()
            {
                throw new NotImplementedException ();
            }

            Task<int> IConnection.ReceiveAsync (byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException ();
            }

            Task<int> IConnection.SendAsync (byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException ();
            }
        }

        class V1Connection : IConnection
        {
            public byte[] AddressBytes => throw new NotImplementedException ();

            public bool Connected => throw new NotImplementedException ();

            public bool CanReconnect => throw new NotImplementedException ();

            public bool IsIncoming => throw new NotImplementedException ();

            public EndPoint EndPoint => throw new NotImplementedException ();

            public Uri Uri => throw new NotImplementedException ();

            public Task ConnectAsync ()
            {
                throw new NotImplementedException ();
            }

            public void Dispose ()
            {
                throw new NotImplementedException ();
            }

            public Task<int> ReceiveAsync (byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException ();
            }

            public Task<int> SendAsync (byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException ();
            }
        }
    }
}
