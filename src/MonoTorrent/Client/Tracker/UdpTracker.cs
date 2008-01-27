using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.Client.Tracker.UdpTrackerMessages;
using System.Net.Sockets;
using System.Net;

namespace MonoTorrent.Client.Tracker
{
    class UdpTracker : Tracker
    {
        private AnnounceParameters storedParams;
        private long connectionId;
        private UdpClient tracker;
        private Uri announceUrl;
        private IPEndPoint endpoint;
        bool hasConnected;
        bool amConnecting;

        public UdpTracker(Uri announceUrl)
        {
            base.CanScrape = false;
            this.announceUrl = announceUrl;
            endpoint = new IPEndPoint(IPAddress.Parse(announceUrl.Host), announceUrl.Port);
            tracker = new UdpClient(announceUrl.Host, announceUrl.Port);
        }

        public override WaitHandle Announce(AnnounceParameters parameters)
        {
            if (!hasConnected && amConnecting)
                return null;

            if (!hasConnected)
            {
                storedParams = parameters;
                amConnecting = true;
                Connect();
                return null;
            }

            AnnounceMessage m = new AnnounceMessage(connectionId, parameters);
            tracker.Send(m.Encode(), m.ByteLength);
            byte[] data = tracker.Receive(ref endpoint);
            AnnounceResponseMessage response = new AnnounceResponseMessage();
            response.Decode(data, 0, data.Length);
            CompleteAnnounce(response.Peers);

            return null;
        }

        private void CompleteAnnounce(List<Peer> list)
        {
            TrackerConnectionID id = new TrackerConnectionID(this, false, MonoTorrent.Common.TorrentEvent.None, null);
            AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(id);
            e.Successful = true;
            e.Peers.AddRange(list);
            RaiseAnnounceComplete(e);
        }

        private void Connect()
        {
            ConnectMessage message = new ConnectMessage();
            tracker.Connect(announceUrl.Host, announceUrl.Port);
            tracker.Send(message.Encode(), message.ByteLength);
            byte[] response = tracker.Receive(ref endpoint);
            ConnectResponseMessage m = new ConnectResponseMessage();
            m.Decode(response, 0, response.Length);

            connectionId = m.ConnectionId;
            hasConnected = true;
            amConnecting = false;
            Announce(storedParams);
            storedParams = null;
        }

        public override WaitHandle Scrape(byte[] infohash, TrackerConnectionID id)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
