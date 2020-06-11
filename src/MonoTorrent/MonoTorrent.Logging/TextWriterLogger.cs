using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using MonoTorrent.Client;

namespace MonoTorrent.Logging
{
    public class TextWriterLogger : ILogger
    {
        TextWriter Writer { get; }

        public TextWriterLogger (TextWriter writer)
        {
            Writer = TextWriter.Synchronized (writer);
        }

        public void Log (LogType type, params object[] data)
        {
            string result = type switch
            {
                LogType.ConnectionBlocked => $"Connection blocked: {((Uri) data[0]).OriginalString}",
                LogType.ConnectionClosed => $"Connection closed: {((Uri) data[0]).OriginalString}",
                LogType.IncomingConnectionEstablished => $"Incoming connection established: {((Uri) data[0]).OriginalString}",
                LogType.OutgoingConnectionEstablished => $"Outgoing connection established: {((Uri) data[0]).OriginalString}",
                LogType.PieceFailedHashCheck => $"Hash Check failed: {((TorrentManager) data[0]).InfoHash} - {(int) data[1]}",
                LogType.PortForwardingError => $"Port forwarding error: {((ClientEngine) data[0]).PortForwardingEnabled} - {(Exception) data[1]}",
                _ => throw new NotImplementedException (),
            };

            Writer.Write (result);
        }
    }
}
