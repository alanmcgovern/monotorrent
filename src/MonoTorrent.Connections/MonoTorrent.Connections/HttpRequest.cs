//
// HttpRequest.cs
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
using System.IO;
using System.Net;
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Client.Connections
{
    public sealed class HttpRequest : IHttpRequest
    {
        public TimeSpan ConnectionTimeout { get; set; }

        WebResponse Response { get; set; }

        Stream Stream { get; set; }

        public HttpRequest ()
        {
            ConnectionTimeout = TimeSpan.FromSeconds (10);
        }

        public void Dispose ()
        {
            Response?.Dispose ();
            Stream?.Dispose ();
        }

        public async ReusableTask<Stream> GetStreamAsync (Uri uri, long fileOffset, long count)
        {
            Response?.Dispose ();
            Stream?.Dispose ();

            var request = (HttpWebRequest) WebRequest.Create (uri);
            request.AddRange (fileOffset, fileOffset + count - 1);

            using var cts = new CancellationTokenSource (ConnectionTimeout);
            using (cts.Token.Register (() => request.Abort ())) {
                Response = await request.GetResponseAsync ();
                Stream = Response.GetResponseStream ();
            }

            return Stream;
        }
    }
}
