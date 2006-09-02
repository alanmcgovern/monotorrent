//
// IClientEngine.cs
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
    /// The interface to access the ClientEngine with
    /// </summary>
    public interface IClientEngine
    {
        /// <summary>
        /// Loads a .torrent into the engine
        /// </summary>
        /// <param name="path">The path to load the .torrent from</param>
        /// <returns></returns>
        ITorrentManager LoadTorrent(string path);

        /// <summary>
        /// The settings for the engine
        /// </summary>
        IEngineSettings Settings { get; set; }


        /// <summary>
        /// Allows a TorrentManager to begin uploading/downloading
        /// </summary>
        /// <param name="torrent">The TorrentManager to start</param>
        void Start(ITorrentManager torrent);


        /// <summary>
        /// Suspends downloading and uploading without disposing of peer information
        /// </summary>
        /// <param name="torrent">The TorrentManager to suspend</param>
        void Pause(ITorrentManager torrent);


        /// <summary>
        /// Stops uploading and downloading and disposes of peer information
        /// </summary>
        /// <param name="torrent">The TorrentManager to stop</param>
        void Stop(ITorrentManager torrent);
    }
}