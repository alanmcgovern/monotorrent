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
        LocalStream ActiveStream { get; set; }
        ClientEngine Engine { get; }
        StreamingPiecePicker Picker { get; }

        public bool Active { get; private set; }
        public bool Paused { get; private set; }
        public TorrentManager Manager { get; }

        public StreamProvider (ClientEngine engine, string saveDirectory, Torrent torrent)
        {
            Engine = engine;
            Manager = new TorrentManager (torrent, saveDirectory);
            Picker = new StreamingPiecePicker (new StandardPicker ());
        }

        public StreamProvider (ClientEngine engine, string saveDirectory, MagnetLink metadataLink, string metadataSaveDirectory)
        {
            Engine = engine;
            Manager = new TorrentManager (metadataLink, saveDirectory, new TorrentSettings (), metadataSaveDirectory);
        }

        public async Task StartAsync ()
        {
            if (Active)
                throw new InvalidOperationException ("The StreamProvider has already been started.");

            if (Manager.Engine != null)
                throw new InvalidOperationException ("The TorrentManager has already been registered with the ClientEngine. This should not occur.");

            if (Engine.Contains (Manager.InfoHash)) {
                throw new InvalidOperationException (
                    "This Torrent/MagnetLink is already being downloaded by the ClientEngine. You must choose to either " +
                    "stream the torrent using StreamProvider or to download it normally with the ClientEngine.");
            }

            await Engine.Register (Manager);
            await Manager.StartAsync ();
            await Manager.ChangePickerAsync (Picker);
            Active = true;
        }

        public async Task PauseAsync ()
        {
            if (!Active)
                throw new InvalidOperationException ("The StreamProvider can only be Paused if it is Active.");
            if (Paused)
                throw new InvalidOperationException ("The StreamProvider cannot be Paused again as it is already paused.");

            await Manager.PauseAsync ();
            Paused = true;
        }

        public async Task ResumeAsync ()
        {
            if (!Paused)
                throw new InvalidOperationException ("The StreamProvider cannot be resumed as it is not currently paused.");

            await Manager.StartAsync ();
            Paused = false;
        }

        public async Task StopAsync ()
        {
            if (!Active)
                throw new InvalidOperationException ("The StreamProvider can only be stopped if it is Active");

            if (Manager.State == TorrentState.Stopped) {
                throw new InvalidOperationException (
                    "The TorrentManager associated with this StreamProvider has already been stopped. " +
                    "It is an error to directly call StopAsync, PauseAsync or StartAsync on the TorrentManager.");
            }
            await Manager.StopAsync ();
            await Engine.Unregister (Manager);
            Active = false;
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public Task<Stream> CreateStreamAsync (TorrentFile file)
        {
            if (file == null)
                throw new ArgumentNullException (nameof (file));
            if (!Manager.Torrent.Files.Contains (file))
                throw new ArgumentException ("The TorrentFile is not from this TorrentManager", nameof (file));
            if (ActiveStream != null && !ActiveStream.Disposed)
                throw new InvalidOperationException ("You must Dispose the previous stream before creating a new one.");
            Picker.SeekToPosition (file, 0);
            ActiveStream = new LocalStream (Manager, file, Picker);
            return Task.FromResult<Stream> (ActiveStream);
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