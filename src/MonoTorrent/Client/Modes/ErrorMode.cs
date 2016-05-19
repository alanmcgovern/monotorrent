using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    // In the error mode, we just disable all connections
    // Usually we enter this because the HD is full
    public enum Reason
    {
        ReadFailure,
        WriteFailure
    }

    public class Error
    {
        private Exception exception;
        private Reason reason;

        public Error(Reason reason, Exception exception)
        {
            this.reason = reason;
            this.exception = exception;
        }

        public Exception Exception
        {
            get { return exception; }
        }

        public Reason Reason
        {
            get { return reason; }
        }
    }

    internal class ErrorMode : Mode
    {
        public override TorrentState State
        {
            get { return TorrentState.Error; }
        }

        public ErrorMode(TorrentManager manager)
            : base(manager)
        {
            CanAcceptConnections = false;
            CloseConnections();
        }

        public override void Tick(int counter)
        {
            Manager.Monitor.Reset();
            CloseConnections();
        }

        private void CloseConnections()
        {
            foreach (var peer in Manager.Peers.ConnectedPeers)
                peer.CloseConnection();
        }
    }
}