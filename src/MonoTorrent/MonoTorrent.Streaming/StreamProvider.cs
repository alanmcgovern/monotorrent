//
// StreamProvider.cs
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonoTorrent.Client;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Streaming
{
    /// <summary>
    /// Prepare the TorrentManager so individual files can be accessed while they are downloading.
    /// </summary>
    public class StreamProvider
    {
        TorrentManager Manager { get; }
        StreamingPiecePicker Picker { get; set; }

        public StreamProvider (TorrentManager manager)
        {
            Manager = manager ?? throw new ArgumentNullException (nameof (manager));
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public async Task<Stream> CreateStreamAsync (TorrentFile file)
        {
            if (file == null)
                throw new ArgumentNullException (nameof (file));
            if (!Manager.Torrent.Files.Contains (file))
                throw new ArgumentException ("The TorrentFile is not from this TorrentManager", nameof (file));

            if (Picker == null) {
                await Manager.StartAsync ();

                Picker = new StreamingPiecePicker (new StandardPicker ());
                await Manager.ChangePickerAsync (Picker);
            }

            Picker.SeekToPosition (file, 0);
            return new LocalStream (Manager, file, Picker);
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public async Task<IUriStream> CreateHttpStreamAsync (TorrentFile file)
        {
            var stream = await CreateStreamAsync (file);
            var httpStreamer = new HttpStream (stream);
            return httpStreamer;
        }
    }
}