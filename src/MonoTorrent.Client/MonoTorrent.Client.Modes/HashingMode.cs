//
// HashingMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;

namespace MonoTorrent.Client.Modes
{
    class HashingMode : IMode
    {
        public bool CanAcceptConnections => false;
        public bool CanHandleMessages => false;
        public bool CanHashCheck => false;
        public TorrentState State => PausedCompletionSource.Task.IsCompleted ? TorrentState.Hashing : TorrentState.HashingPaused;
        public CancellationToken Token => Cancellation.Token;

        CancellationTokenSource Cancellation { get; }
        DiskManager DiskManager { get; }
        TorrentManager Manager { get; }
        TaskCompletionSource<object?> PausedCompletionSource { get; set; }

        public HashingMode (TorrentManager manager, DiskManager diskManager)
        {
            (Cancellation, Manager, DiskManager) = (new CancellationTokenSource (), manager, diskManager);

            // Mark it as completed so we are *not* paused by default;
            PausedCompletionSource = new TaskCompletionSource<object?> ();
            PausedCompletionSource.SetResult (null);
        }

        public void HandleFilePriorityChanged (ITorrentManagerFile file, Priority oldPriority)
        {
            // Nothing
        }

        public void Pause ()
        {
            if (State == TorrentState.HashingPaused)
                return;

            PausedCompletionSource?.TrySetResult (null);
            PausedCompletionSource = new TaskCompletionSource<object?> ();
            Cancellation.Token.Register (() => PausedCompletionSource.TrySetCanceled ());
            Manager.RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (Manager, TorrentState.Hashing, State));
        }

        public void Resume ()
        {
            if (State == TorrentState.Hashing)
                return;

            PausedCompletionSource.TrySetResult (null);
            Manager.RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (Manager, TorrentState.HashingPaused, State));
        }

        public async Task WaitForHashingToComplete ()
        {
            if (!Manager.HasMetadata)
                throw new TorrentException ("A hash check cannot be performed if TorrentManager.HasMetadata is false.");

            Manager.HashFails = 0;

            // Delete any existing fast resume data. We will need to recreate it after hashing completes.
            await Manager.MaybeDeleteFastResumeAsync ();

            // Files can only exist if their save directory exists.
            bool anyExisted = Directory.Exists (Manager.SavePath);

            // Check the size of all files first. It's easier than trying to insert it into the logic below as it'd have to be in several places.
            if (anyExisted) {
                // If the directory existed, it's worth checking if any files exist while we gather their sizes.
                anyExisted = false;
                foreach (TorrentFileInfo file in Manager.Files) {
                    var length = await DiskManager.GetLengthAsync (file);
                    file.CachedActualLength = length;

                    anyExisted |= length.HasValue;
                    // If this is a zero length file, mark it as downloaded if it exists.
                    if (file.Length == 0)
                        file.BitField[0] = length.HasValue && length.Value == 0;
                }
            }

            bool atLeastOneDoNotDownload = Manager.Files.Any (t => t.Priority == Priority.DoNotDownload);
            if (anyExisted) {
                int piecesHashed = 0;
                Cancellation.Token.ThrowIfCancellationRequested ();
                // bep52: Properly support this
                using var hashBuffer = MemoryPool.Default.Rent (Manager.InfoHashes.GetMaxByteCount (), out Memory<byte> hashMemory);
                var hashes = new PieceHash (hashMemory);
                for (int index = 0; index < Manager.Torrent!.PieceCount; index++) {
                    if (atLeastOneDoNotDownload && !Manager.Files.Any (f => index >= f.StartPieceIndex && index <= f.EndPieceIndex && f.Priority != Priority.DoNotDownload)) {
                        // If a file is marked 'do not download' ensure we update the TorrentFiles
                        // so they also report that the piece is not available/downloaded.
                        Manager.OnPieceHashed (index, false, piecesHashed, Manager.PartialProgressSelector.TrueCount);
                        // Then mark this piece as being unhashed so we don't try to download it.
                        Manager.UnhashedPieces[index] = true;
                        continue;
                    }

                    await PausedCompletionSource.Task;
                    Cancellation.Token.ThrowIfCancellationRequested ();

                    var successful = await DiskManager.GetHashAsync (Manager, index, hashes);

                    if (Cancellation.Token.IsCancellationRequested) {
                        await DiskManager.CloseFilesAsync (Manager);
                        Cancellation.Token.ThrowIfCancellationRequested ();
                    }

                    bool hashPassed = successful && Manager.PieceHashes.IsValid (hashes, index);
                    Manager.OnPieceHashed (index, hashPassed, ++piecesHashed, Manager.PartialProgressSelector.TrueCount);
                }
            } else {
                await PausedCompletionSource.Task;
                for (int i = 0; i < Manager.Torrent!.PieceCount; i++)
                    Manager.OnPieceHashed (i, false, i + 1, Manager.Torrent.PieceCount);
            }

            // If we successfully hashed all the files we still need to ensure they are the correct length
            Manager.RefreshAllFilesDownloadableOrDownloaded ();
        }

        public void Dispose ()
            => Cancellation.Cancel ();

        public void HandleMessage (PeerId id, PeerMessage message, PeerMessage.Releaser releaser)
            => new NotSupportedException ();

        public void HandlePeerConnected (PeerId id)
            => throw new NotSupportedException ();

        public void HandlePeerDisconnected (PeerId id)
            => throw new NotSupportedException ();

        public bool ShouldConnect (Peer peer)
            => false;

        public void Tick (int counter)
        {
            // Do not run any of the default 'Tick' logic as nothing happens during 'Hashing' mode, except for hashing.
        }
    }
}
