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


using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    class HashingMode : Mode
    {
        CancellationTokenSource Cancellation { get; }

        public override bool CanHashCheck => false;
        public override TorrentState State => TorrentState.Hashing;

        public HashingMode(TorrentManager manager)
            : base(manager)
        {
            CanAcceptConnections = false;
            Cancellation = new CancellationTokenSource();
        }

        public Task WaitForHashingToComplete ()
            => WaitForHashingToComplete (Cancellation.Token);

        async Task WaitForHashingToComplete (CancellationToken token)
        {
            if (!Manager.HasMetadata)
                throw new TorrentException ("A hash check cannot be performed if TorrentManager.HasMetadata is false.");

            Manager.HashFails = 0;
            if (await Manager.Engine.DiskManager.CheckAnyFilesExistAsync (Manager)) {
                for (int index = 0; index < Manager.Torrent.Pieces.Count; index++) {
                    var hash = await Manager.Engine.DiskManager.GetHashAsync(Manager, index);

                    if (token.IsCancellationRequested) {
                        await Manager.Engine.DiskManager.CloseFilesAsync (Manager);
                        token.ThrowIfCancellationRequested();
                    }

                    var hashPassed = hash != null && Manager.Torrent.Pieces.IsValid(hash, index);
                    Manager.OnPieceHashed (index, hashPassed);
                }
            } else {
                for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
                    Manager.OnPieceHashed(i, false);
            }
        }

        public override void Tick (int counter)
        {
            // Do not run any of the default 'Tick' logic as nothing happens during 'Hashing' mode, except for hashing.
        }

        public override void Dispose ()
            => Cancellation.Cancel ();
    }
}
