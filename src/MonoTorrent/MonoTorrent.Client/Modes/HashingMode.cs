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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Client.Modes
{
    class HashingMode : Mode
    {

        TaskCompletionSource<object> PausedCompletionSource { get; set; }

        public override bool CanAcceptConnections => false;
        public override bool CanHandleMessages => false;
        public override bool CanHashCheck => false;
        public override TorrentState State => PausedCompletionSource.Task.IsCompleted ? TorrentState.Hashing : TorrentState.HashingPaused;

        public HashingMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
            : base (manager, diskManager, connectionManager, settings)
        {
            // Mark it as completed so we are *not* paused by default;
            PausedCompletionSource = new TaskCompletionSource<object> ();
            PausedCompletionSource.TrySetResult (null);
        }

        public void Pause ()
        {
            if (State == TorrentState.HashingPaused)
                return;

            PausedCompletionSource?.TrySetResult (null);
            PausedCompletionSource = new TaskCompletionSource<object> ();
            Cancellation.Token.Register (() => PausedCompletionSource.TrySetCanceled ());
            Manager.RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (Manager, TorrentState.HashingPaused, State));
        }

        public void Resume ()
        {
            if (State == TorrentState.Hashing)
                return;

            PausedCompletionSource.TrySetResult (null);
            Manager.RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (Manager, TorrentState.Hashing, State));
        }

        public async Task WaitForHashingToComplete ()
        {
            if (!Manager.HasMetadata)
                throw new TorrentException ("A hash check cannot be performed if TorrentManager.HasMetadata is false.");

            Manager.HashFails = 0;
            if (await DiskManager.CheckAnyFilesExistAsync (Manager.Torrent)) {
                Cancellation.Token.ThrowIfCancellationRequested ();
                for (int index = 0; index < Manager.Torrent.Pieces.Count; index++) {
                    if (!Manager.Torrent.Files.Any (f => index >= f.StartPieceIndex && index <= f.EndPieceIndex && f.Priority != Priority.DoNotDownload)) {
                        // If a file is marked 'do not download' ensure we update the TorrentFiles
                        // so they also report that the piece is not available/downloaded.
                        Manager.OnPieceHashed (index, false);
                        // Then mark this piece as being unhashed so we don't try to download it.
                        Manager.UnhashedPieces[index] = true;
                        continue;
                    }

                    await PausedCompletionSource.Task;
                    Cancellation.Token.ThrowIfCancellationRequested ();

                    var hash = await DiskManager.GetHashAsync(Manager.Torrent, index);

                    if (Cancellation.Token.IsCancellationRequested) {
                        await DiskManager.CloseFilesAsync (Manager.Torrent);
                        Cancellation.Token.ThrowIfCancellationRequested();
                    }

                    var hashPassed = hash != null && Manager.Torrent.Pieces.IsValid(hash, index);
                    Manager.OnPieceHashed (index, hashPassed);
                }
            } else {
                await PausedCompletionSource.Task;

                for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
                    Manager.OnPieceHashed(i, false);
            }
        }

        public override void Tick (int counter)
        {
            // Do not run any of the default 'Tick' logic as nothing happens during 'Hashing' mode, except for hashing.
        }
    }
}
