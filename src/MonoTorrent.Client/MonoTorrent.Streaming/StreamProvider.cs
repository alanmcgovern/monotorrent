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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client;
using MonoTorrent.PiecePicking;

namespace MonoTorrent.Streaming
{
    /// <summary>
    /// Prepare the TorrentManager so individual files can be accessed while they are downloading.
    /// </summary>
    public class StreamProvider
    {
        LocalStream? ActiveStream { get; set; }
        CancellationTokenSource Cancellation { get; set; }
        TorrentManager Manager { get; }
        IStreamingPieceRequester PieceRequester { get; }

        internal StreamProvider (TorrentManager manager, IStreamingPieceRequester pieceRequester)
        {
            Cancellation = new CancellationTokenSource ();
            Manager = manager;
            PieceRequester = pieceRequester;
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. The first and last pieces of
        /// this file will be buffered before the stream is created. Finally, this stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <returns></returns>
        public Task<Stream> CreateStreamAsync (ITorrentManagerFile file)
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
        public Task<Stream> CreateStreamAsync (ITorrentManagerFile file, CancellationToken token)
            => CreateStreamAsync (file, prebuffer: true, token);

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. The first and last pieces of
        /// this file will be buffered before the stream is created if <paramref name="prebuffer"/> is
        /// set to true. Finally, this stream must be disposed before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="prebuffer">True if the first and last piece should be downloaded before the Stream is created.</param>
        /// <returns></returns>
        public Task<Stream> CreateStreamAsync (ITorrentManagerFile file, bool prebuffer)
            => CreateStreamAsync (file, prebuffer, CancellationToken.None);

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
        public async Task<Stream> CreateStreamAsync (ITorrentManagerFile file, bool prebuffer, CancellationToken token)
        {
            if (file == null)
                throw new ArgumentNullException (nameof (file));
            if (Manager.Files == null)
                throw new InvalidOperationException ("The metadata for this torrent has not been downloaded. You must call WaitForMetadataAsync before creating a stream.");
            if (Manager.State == TorrentState.Stopped || Manager.State == TorrentState.Stopping || Manager.State == TorrentState.Error)
                throw new InvalidOperationException ($"The torrent state was {Manager.State}. StreamProvider cannot be used unless the torrent manager has been successfully started.");
            if (!Manager.Files.Contains (file))
                throw new ArgumentException ("The TorrentFile is not from this TorrentManager", nameof (file));
            if (ActiveStream != null && !ActiveStream.Disposed)
                throw new InvalidOperationException ("You must Dispose the previous stream before creating a new one.");

            ActiveStream = new LocalStream (Manager, file, PieceRequester);

            var tcs = CancellationTokenSource.CreateLinkedTokenSource (Cancellation.Token, token);
            if (prebuffer) {
                ActiveStream.Seek (ActiveStream.Length - Manager.Torrent!.PieceLength * 2, SeekOrigin.Begin);
                await ActiveStream.ReadAsync (new byte[1], 0, 1, tcs.Token);

                ActiveStream.Seek (0, SeekOrigin.Begin);
                await ActiveStream.ReadAsync (new byte[1], 0, 1, tcs.Token);
            }

            ActiveStream.Seek (0, SeekOrigin.Begin);
            return ActiveStream;
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. This stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <returns></returns>
        public Task<IHttpStream> CreateHttpStreamAsync (ITorrentManagerFile file)
            => CreateHttpStreamAsync (file, prebuffer: true, CancellationToken.None);

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. This stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="token">The cancellation token</param>
        /// <returns></returns>
        public Task<IHttpStream> CreateHttpStreamAsync (ITorrentManagerFile file, CancellationToken token)
            => CreateHttpStreamAsync (file, prebuffer: true, token);

        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. This stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="prebuffer">True if the first and last piece should be downloaded before the Stream is created.</param>
        /// <returns></returns>
        public Task<IHttpStream> CreateHttpStreamAsync (ITorrentManagerFile file, bool prebuffer)
            => CreateHttpStreamAsync (file, prebuffer, CancellationToken.None);


        /// <summary>
        /// Creates a <see cref="Stream"/> which can be used to access the given <see cref="TorrentFile"/>
        /// while it is downloading. This stream is seekable and readable. This stream must be disposed
        /// before another stream can be created.
        /// </summary>
        /// <param name="file">The file to open</param>
        /// <param name="prebuffer">True if the first and last piece should be downloaded before the Stream is created.</param>
        /// <param name="token">The cancellation token</param>
        /// <returns></returns>

        public async Task<IHttpStream> CreateHttpStreamAsync (ITorrentManagerFile file, bool prebuffer, CancellationToken token)
        {
            var stream = await CreateStreamAsync (file, prebuffer, token);
            var httpStreamer = new HttpStream (Manager.Engine!.Settings.HttpStreamingPrefix, stream);
            return httpStreamer;
        }
    }
}
