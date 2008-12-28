using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.UdpTracker;

namespace MonoTorrent.Client.Tracker
{
    //public class UdpTracker : Tracker
    //{
    //    private AnnounceParameters storedParams;
    //    private long connectionId;
    //    private UdpClient tracker;
    //    private IPEndPoint endpoint;
    //    bool hasConnected;
    //    bool amConnecting;

    //    public UdpTracker(Uri announceUrl)
    //        :base(announceUrl)
    //    {
    //        CanScrape = false;
    //        tracker = new UdpClient(announceUrl.Host, announceUrl.Port);
    //        endpoint = (IPEndPoint)tracker.Client.RemoteEndPoint;
    //    }

    //    public override void Announce(AnnounceParameters parameters, object state)
    //    {
    //        LastUpdated = DateTime.Now;
    //        if (!hasConnected && amConnecting)
    //            return null;

    //        if (!hasConnected)
    //        {
    //            storedParams = parameters;
    //            amConnecting = true;
    //            Connect();
    //            return null;
    //        }

    //        AnnounceMessage m = new AnnounceMessage(0, connectionId, parameters);
    //        byte[] data = null;
    //        try
    //        {
    //            tracker.Send(m.Encode(), m.ByteLength);
    //            data = tracker.Receive(ref endpoint);
    //        }
    //        catch (SocketException)
    //        {
    //            TrackerConnectionID id = new TrackerConnectionID(this, false, MonoTorrent.Common.TorrentEvent.None, null);
    //            AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(id);
    //            e.Successful = false;
    //            RaiseAnnounceComplete(e);
    //            return null;
    //        }

    //        UdpTrackerMessage message = UdpTrackerMessage.DecodeMessage(data, 0, data.Length, MessageType.Response);

    //        CompleteAnnounce(message);

    //        return null;
    //    }

    //    private void CompleteAnnounce(UdpTrackerMessage message)
    //    {
    //        TrackerConnectionID id = new TrackerConnectionID(this, false, MonoTorrent.Common.TorrentEvent.None, null);
    //        AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(id);
    //        ErrorMessage error = message as ErrorMessage;
    //        if (error != null)
    //        {
    //            e.Successful = false;
    //            FailureMessage = error.Error;
    //        }
    //        else
    //        {
    //            AnnounceResponseMessage response = (AnnounceResponseMessage)message;
    //            e.Successful = true;
    //            e.Peers.AddRange(response.Peers);
    //        }

    //        RaiseAnnounceComplete(e);
    //    }

    //    private void Connect()
    //    {
    //        ConnectMessage message = new ConnectMessage();

    //        byte[] response = null;
    //        try
    //        {
    //            tracker.Connect(Uri.Host, Uri.Port);
    //            tracker.Send(message.Encode(), message.ByteLength);
    //            response = tracker.Receive(ref endpoint);
    //        }
    //        catch (SocketException)
    //        {
    //            TrackerConnectionID id = new TrackerConnectionID(this, false, MonoTorrent.Common.TorrentEvent.None, null);
    //            AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(id);
    //            e.Successful = false;
    //            RaiseAnnounceComplete(e);
    //            return;
    //        }

    //        ConnectResponseMessage m = (ConnectResponseMessage)UdpTrackerMessage.DecodeMessage(response, 0, response.Length, MessageType.Response);// new ConnectResponseMessage();

    //        connectionId = m.ConnectionId;
    //        hasConnected = true;
    //        amConnecting = false;
    //        Announce(storedParams);
    //        storedParams = null;
    //    }

    //    public override void Scrape(ScrapeParameters parameters, object state)
    //    {
    //        throw new Exception("The method or operation is not implemented.");
    //    }
    //}
}
