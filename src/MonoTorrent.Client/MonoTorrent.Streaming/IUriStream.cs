//
// IUriStream.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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

using MonoTorrent.Client;

namespace MonoTorrent.Streaming
{
    public interface IHttpStream : IDisposable
    {
        /// <summary>
        /// The HTTP prefix registered with the underlying HttpListener. This is the same value as configured in the
        /// <see cref="EngineSettings.HttpStreamingPrefix"/> property.
        /// </summary>
        string HttpPrefix { get; }

        /// <summary>
        /// The full Uri to the underlying file. This is created by concatenating <see cref="HttpPrefix"/> and <see cref="RelativeUri"/>.
        /// If the HttpPrefix is of the form 'http://*:12345/' or 'http://+:12345' then the full Uri will need to be constructed using
        /// an addressable IP address, or hostname.
        /// </summary>
        string FullUri { get; }

        /// <summary>
        /// The unique identifier used to access the underlying file.
        /// </summary>
        string RelativeUri { get; }
    }
}
