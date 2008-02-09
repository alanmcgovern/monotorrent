using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Tracker.UdpTrackerMessages;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    public abstract class UdpTrackerMessage : Message
    {
        public static UdpTrackerMessage DecodeMessage(ArraySegment<byte> buffer, int offset, int count)
        {
            return DecodeMessage(buffer.Array, buffer.Offset + offset, count);
        }

        public static UdpTrackerMessage DecodeMessage(byte[] buffer, int offset, int count)
        {
            UdpTrackerMessage m = null;
            switch (ReadInt(buffer, offset))
            {
                case 0:
                    m = new ConnectResponseMessage();
                    break;
                case 1:
                    m = new AnnounceResponseMessage();
                    break;
                case 2:
                    m = new ScrapeResponseMessage();
                    break;
                case 3:
                    m = new ErrorMessage();
                    break;
                default:
                    throw new ProtocolException(string.Format("Invalid udp message received: {0}", buffer[offset]));
            }

            try
            {
                m.Decode(buffer, offset, count);
            }
            catch
            {
                m = new ErrorMessage("Couldn't decode the tracker response");
            }
            return m;
        }
    }
}
