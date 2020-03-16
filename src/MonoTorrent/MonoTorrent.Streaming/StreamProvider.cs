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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
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

        /// <summary>
        /// Returns true when the <see cref="StreamProvider"/> has been started.
        /// </summary>
        public bool Active { get; private set; }

        /// <summary>
        /// Can be used to cancel pending operations when StopAsync is invoked.
        /// </summary>
        CancellationTokenSource Cancellation { get; set; }

        /// <summary>
        /// Returns true when the <see cref="StreamProvider"/> has been paused.
        /// </summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// If the <see cref="StreamProvider"/> was created using a <see cref="Torrent"/> then
        /// the files will be available immediately. Otherwise the files will be available as soon
        /// as the metadata has been downloaded. The <see cref="Task"/> returned by <see cref="WaitForMetadataAsync()"/>
        /// will complete after this has been set to the list of files.
        /// </summary>
        public IList<TorrentFile> Files {
            get; private set;
        }
        /// <summary>
        /// The underlying <see cref="TorrentManager"/> used to download the data.
        /// It is safe to attach to events, retrieve state and also change any of
        /// the settings associated with this TorrentManager. You should never
        /// call StartAsync, StopAsync, PauseAsync or similar life-cycle methods,
        /// nor should you attempt to register this with a <see cref="ClientEngine"/>.
        /// </summary>
        public TorrentManager Manager { get; }

        /// <summary>
        /// Creates a StreamProvider for the given <see cref="Torrent"/> so that files
        /// contained within the torrent can be accessed as they are downloading.
        /// </summary>
        /// <param name="engine">The engine used to host the download.</param>
        /// <param name="saveDirectory">The directory where the torrents data will be saved</param>
        /// <param name="torrent">The torrent to download</param>
        public StreamProvider (ClientEngine engine, string saveDirectory, Torrent torrent)
        {
            Engine = engine;
            Manager = new TorrentManager (torrent, saveDirectory);
            Manager.ChangePicker (Picker = new StreamingPiecePicker (new StandardPicker ()));
            Files = Array.AsReadOnly (torrent.Files);
        }

        /// <summary>
        /// Creates a StreamProvider for the given <see cref="MagnetLink"/> so that files
        /// contained within the torrent can be accessed as they are downloading.
        /// </summary>
        /// <param name="engine">The engine used to host the download.</param>
        /// <param name="saveDirectory">The directory where the torrents data will be saved</param>
        /// <param name="magnetLink">The MagnetLink to download</param>
        /// <param name="metadataSaveDirectory">The directory where the metadata will be saved. The
        /// filename will be constucted by appending '.torrent' to the value returned by <see cref="InfoHash.ToHex ()"/>
        /// </param>
        public StreamProvider (ClientEngine engine, string saveDirectory, MagnetLink magnetLink, string metadataSaveDirectory)
        {
            Engine = engine;
            var path = Path.Combine (metadataSaveDirectory, $"{magnetLink.InfoHash.ToHex ()}.torrent");
            Manager = new TorrentManager (magnetLink, saveDirectory, new TorrentSettings (), path);
            Manager.ChangePicker (Picker = new StreamingPiecePicker (new StandardPicker ()));

            // If the metadata for this MagnetLink has been downloaded/cached already, we will synchronously
            // load it here and will have access to the list of Files. Otherwise we need to wait.
            if (Manager.HasMetadata)
                Files = Array.AsReadOnly (Manager.Torrent.Files);
            else
                Manager.MetadataReceived += (o, e) => Files = Array.AsReadOnly (e.torrent.Files);
        }

        /// <summary>
        /// Registers <see cref="Manager"/> with the <see cref="ClientEngine"/>
        /// and calls <see cref="TorrentManager.StartAsync()"/>.
        /// </summary>
        /// <returns></returns>
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

            Cancellation = new CancellationTokenSource ();
            await Engine.Register (Manager);
            await Manager.StartAsync ();
            Active = true;
        }

        /// <summary>
        /// Calls <see cref="TorrentManager.PauseAsync()"/> to pause Hashing, Seeding or Downloading.
        /// </summary>
        /// <returns></returns>
        public async Task PauseAsync ()
        {
            if (!Active)
                throw new InvalidOperationException ("The StreamProvider can only be Paused if it is Active.");
            if (Paused)
                throw new InvalidOperationException ("The StreamProvider cannot be Paused again as it is already paused.");

            await Manager.PauseAsync ();
            Paused = true;
        }

        /// <summary>
        /// Calls <see cref="TorrentManager.StartAsync()"/> to resume Hashing, Seeding or Downloading.
        /// </summary>
        /// <returns></returns>
        public async Task ResumeAsync ()
        {
            if (!Paused)
                throw new InvalidOperationException ("The StreamProvider cannot be resumed as it is not currently paused.");

            await Manager.StartAsync ();
            Paused = false;
        }

        /// <summary>
        /// Calls <see cref="TorrentManager.StopAsync()"/> on <see cref="Manager"/> and unregisters
        /// it from the <see cref="ClientEngine"/>. This will dispose the stream returned by the
        /// most recent invocation of <see cref="CreateHttpStreamAsync(TorrentFile)"/> or
        /// <see cref="CreateStreamAsync(TorrentFile)"/>.
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync ()
        {
            if (!Active)
                throw new InvalidOperationException ("The StreamProvider can only be stopped if it is Active");

            if (Manager.State == TorrentState.Stopped) {
                throw new InvalidOperationException (
                    "The TorrentManager associated with this StreamProvider has already been stopped. " +
                    "It is an error to directly call StopAsync, PauseAsync or StartAsync on the TorrentManager.");
            }

            Cancellation.Cancel ();
            await Manager.StopAsync ();
            await Engine.Unregister (Manager);
            ActiveStream.SafeDispose ();
            Active = false;
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. The first and last pieces of
        /// this file will be buffered before the stream is created. Finally, this stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <returns></returns>
        public Task<Stream> CreateStreamAsync (TorrentFile file)
            => CreateStreamAsync (file, prebuffer: true, CancellationToken.None);

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. The first and last pieces of
        /// this file will be buffered before the stream is created. Finally, this stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="token">The cancellation token</param>
        /// <returns></returns>
        public Task<Stream> CreateStreamAsync (TorrentFile file, CancellationToken token)
            => CreateStreamAsync (file, prebuffer: true, token);

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. The first and last pieces of
        /// this file will be buffered before the stream is created if <paramref name="prebuffer"/> is
        /// set to true. Finally, this stream must be disposed before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="prebuffer">True if the first and last piece should be downloaded before the Stream is created.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Stream> CreateStreamAsync (TorrentFile file, bool prebuffer, CancellationToken token)
        {
            if (file == null)
                throw new ArgumentNullException (nameof (file));
            if (!Manager.Torrent.Files.Contains (file))
                throw new ArgumentException ("The TorrentFile is not from this TorrentManager", nameof (file));
            if (!Active)
                throw new InvalidOperationException ("You must call StartAsync before creating a stream.");
            if (ActiveStream != null && !ActiveStream.Disposed)
                throw new InvalidOperationException ("You must Dispose the previous stream before creating a new one.");

            Picker.SeekToPosition (file, 0);
            ActiveStream = new LocalStream (Manager, file, Picker);

            var tcs = CancellationTokenSource.CreateLinkedTokenSource (Cancellation.Token, token);
            if (prebuffer) {
                ActiveStream.Seek (ActiveStream.Length - 1, SeekOrigin.Begin);
                await ActiveStream.ReadAsync (new byte[1], 0, 1, tcs.Token);

                ActiveStream.Seek (0, SeekOrigin.Begin);
                await ActiveStream.ReadAsync (new byte[1], 0, 1, tcs.Token);
            }
            return ActiveStream;
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. This stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <returns></returns>
        public Task<IUriStream> CreateHttpStreamAsync (TorrentFile file)
            => CreateHttpStreamAsync (file, prebuffer: true, CancellationToken.None);

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. This stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="token">The cancellation token</param>
        /// <returns></returns>
        public Task<IUriStream> CreateHttpStreamAsync (TorrentFile file, CancellationToken token)
            => CreateHttpStreamAsync (file, prebuffer: true, token);

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. This stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="prebuffer">True if the first and last piece should be downloaded before the Stream is created.</param>
        /// <param name="token">The cancellation token</param>
        /// <returns></returns>
        public async Task<IUriStream> CreateHttpStreamAsync (TorrentFile file, bool prebuffer, CancellationToken token)
        {
            var stream = await CreateStreamAsync (file, prebuffer, token);
            var httpStreamer = new HttpStream (stream);
            return httpStreamer;
        }

        /// <summary>
        /// If the <see cref="StreamProvider"/> was created using a MagnetLink, the <see cref="Task"/>
        /// returned by this method will complete when the <see cref="Files"/> property is non-null. If
        /// the <see cref="StreamProvider"/> was created using a <see cref="Torrent"/> instance then
        /// this will return a completed task as <see cref="Files"/> will already be non-null.
        /// This operation will be cancelled if <see cref="StopAsync"/> is invoked.
        /// </summary>
        public async Task WaitForMetadataAsync ()
            => await WaitForMetadataAsync (CancellationToken.None);

        /// <summary>
        /// If the <see cref="StreamProvider"/> was created using a MagnetLink, the <see cref="Task"/>
        /// returned by this method will complete when the <see cref="Files"/> property is non-null. If
        /// the <see cref="StreamProvider"/> was created using a <see cref="Torrent"/> instance then
        /// this will return a completed task as <see cref="Files"/> will already be non-null.
        /// This operation will be cancelled if <see cref="StopAsync"/> is invoked.
        /// </summary>
        /// <param name="token">The cancellation token</param>
        /// <returns></returns>
        public async Task WaitForMetadataAsync (CancellationToken token)
        {
            if (!Active)
                throw new InvalidOperationException ("You must call StartAsync first.");

            if (Files != null)
                return;

            // Proxy to the main thread so there's no race condition
            // between the metadata downloading and us attaching the
            // EventHandler.
            await ClientEngine.MainLoop;
            token.ThrowIfCancellationRequested ();

            // Cancel if the user call StopAsync or if they cancel the token they passed in
            var cts = CancellationTokenSource.CreateLinkedTokenSource (token, Cancellation.Token);

            // If the files still aren't available then let's wait for the metadata
            // to be downloaded.
            if (Files == null) {
                var tcs = new TaskCompletionSource<bool> ();
                using var reg = cts.Token.Register (() => tcs.TrySetCanceled ());
                // The EventHandler set during StartAsync will ensure the list of
                // files has been set.
                Manager.MetadataReceived += (o, e) => tcs.TrySetResult (true);
                await tcs.Task;
            }
        }
    }
}