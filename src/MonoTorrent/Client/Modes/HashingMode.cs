using System.Threading;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class HashingMode : Mode
    {
        private readonly bool autostart;
        private readonly bool filesExist;
        private readonly MainLoopResult pieceCompleteCallback;
        internal ManualResetEvent hashingWaitHandle;
        private int index = -1;

        public HashingMode(TorrentManager manager, bool autostart)
            : base(manager)
        {
            CanAcceptConnections = false;
            hashingWaitHandle = new ManualResetEvent(false);
            this.autostart = autostart;
            filesExist = Manager.HasMetadata && manager.Engine.DiskManager.CheckAnyFilesExist(Manager);
            pieceCompleteCallback = PieceComplete;
        }

        public override TorrentState State
        {
            get { return TorrentState.Hashing; }
        }

        private void QueueNextHash()
        {
            if (Manager.Mode != this || index == Manager.Torrent.Pieces.Count)
                HashingComplete();
            else
                Manager.Engine.DiskManager.BeginGetHash(Manager, index, pieceCompleteCallback);
        }

        private void PieceComplete(object hash)
        {
            if (Manager.Mode != this)
            {
                HashingComplete();
            }
            else
            {
                Manager.Bitfield[index] = hash == null ? false : Manager.Torrent.Pieces.IsValid((byte[]) hash, index);
                Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, index, Manager.Bitfield[index]));
                index++;
                QueueNextHash();
            }
        }

        private void HashingComplete()
        {
            Manager.HashChecked = index == Manager.Torrent.Pieces.Count;

            if (Manager.HasMetadata && !Manager.HashChecked)
            {
                Manager.Bitfield.SetAll(false);
                for (var i = 0; i < Manager.Torrent.Pieces.Count; i++)
                    Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, i, false));
            }

            if (Manager.Engine != null && filesExist)
                Manager.Engine.DiskManager.CloseFileStreams(Manager);

            hashingWaitHandle.Set();

            if (!Manager.HashChecked)
                return;

            if (autostart)
            {
                Manager.Start();
            }
            else
            {
                Manager.Mode = new StoppedMode(Manager);
            }
        }

        public override void HandlePeerConnected(PeerId id, Direction direction)
        {
            id.CloseConnection();
        }

        public override void Tick(int counter)
        {
            if (!filesExist)
            {
                Manager.Bitfield.SetAll(false);
                for (var i = 0; i < Manager.Torrent.Pieces.Count; i++)
                    Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, i, false));
                index = Manager.Torrent.Pieces.Count;
                HashingComplete();
            }
            else if (index == -1)
            {
                index++;
                QueueNextHash();
            }
            // Do nothing in hashing mode
        }
    }
}