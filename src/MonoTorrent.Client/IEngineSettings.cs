//
// IEngineSettings.cs
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
    /// The interface with which to access the EngineSettings
    /// </summary>
    public interface IEngineSettings
    {
        /// <summary>
        /// The maximum combined download speed for all torrents currently running
        /// </summary>
        int GlobalMaxDownloadSpeed { get; set; }

        /// <summary>
        /// The maximum combined upload speed for all torrents currently running
        /// </summary>
        int GlobalMaxUploadSpeed { get; set; }


        /// <summary>
        /// The default directory to save torrents to
        /// </summary>
        string DefaultSavePath { get; set; }


        /// <summary>
        /// The maximum combined concurrent open connections
        /// </summary>
        int GlobalMaxConnections { get; set; }


        /// <summary>
        /// The maximum combined concurrent half open connections
        /// </summary>
        int GlobalMaxHalfOpenConnections { get; set; }


        /// <summary>
        /// The port to accept incoming connections on
        /// </summary>
        int ListenPort { get; set; }
    }
}