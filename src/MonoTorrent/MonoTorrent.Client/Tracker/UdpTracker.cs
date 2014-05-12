using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.UdpTracker;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public class UdpTracker : Tracker
    {
        private long connectionId;
        private UdpClient tracker;
        private IPEndPoint endpoint;
        bool hasConnected;
        bool amConnecting;
        internal TimeSpan RetryDelay;
        int timeout;
        IAsyncResult ReceiveAsyncResult;

        public UdpTracker(Uri announceUrl)
            : base(announceUrl)
        {
            CanScrape = true;
            CanAnnounce = true;
            RetryDelay = TimeSpan.FromSeconds(15);
            tracker = new UdpClient(announceUrl.Host, announceUrl.Port);
            endpoint = (IPEndPoint)tracker.Client.RemoteEndPoint;
        }

        #region announce

        public override void Announce(AnnounceParameters parameters, object state)
        {

            //LastUpdated = DateTime.Now;
            if (!hasConnected && amConnecting)
            {
                IAsyncResult ar = ReceiveAsyncResult;
                if (ar != null)
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
                        return;
            }

            if (!hasConnected)
            {
                amConnecting = true;
                try
                {
                    Connect(new ConnectAnnounceState(parameters, ConnectAnnounceCallback, state));
                }
                catch (SocketException)
                {
                    DoAnnounceComplete(false, state, new List<Peer>());
                    return;
                }
            }
            else
                DoAnnounce(parameters, state);
        }

        private void DoAnnounce(AnnounceParameters parameters, object state)
        {
            ConnectAnnounceState announceState = new ConnectAnnounceState(parameters, AnnounceCallback, state);
            announceState.Message = new AnnounceMessage(DateTime.Now.GetHashCode(), connectionId, parameters);
            try
            {
                SendAndReceive(announceState);
            }
            catch (SocketException)
            {
                DoAnnounceComplete(false, state, new List<Peer>());
            }
        }

        private void ConnectAnnounceCallback(IAsyncResult ar)
        {
            ConnectAnnounceState announceState = (ConnectAnnounceState)ar;
            try
            {
                if (announceState.SavedException != null)
                {
                    FailureMessage = announceState.SavedException.Message;
                    amConnecting = false;
                    DoAnnounceComplete(false, announceState.AsyncState, new List<Peer>());
                    return;
                }
                if (!ConnectCallback(ar))//bad transaction id
                {
                    DoAnnounceComplete(false, announceState.AsyncState, new List<Peer>());
                    return;
                }
                DoAnnounce(announceState.Parameters, announceState.AsyncState);
            }
            catch
            {
                DoAnnounceComplete(false, announceState.AsyncState, null);
            }
        }

        private void AnnounceCallback(IAsyncResult ar)
        {
            ConnectAnnounceState announceState = (ConnectAnnounceState)ar;
            try
            {
                if (announceState.SavedException != null)
                {
                    FailureMessage = announceState.SavedException.Message;
                    DoAnnounceComplete(false, announceState.AsyncState, new List<Peer>());
                    return;
                }
                UdpTrackerMessage rsp = Receive(announceState, announceState.Data);
                if (!(rsp is AnnounceResponseMessage))
                {
                    DoAnnounceComplete(false, announceState.AsyncState, new List<Peer>());
                    return;
                }

                MinUpdateInterval = ((AnnounceResponseMessage)rsp).Interval;
                CompleteAnnounce(rsp, announceState.AsyncState);
            }
            catch
            {
                DoAnnounceComplete(false, announceState.AsyncState, null);
            }
        }

        private void CompleteAnnounce(UdpTrackerMessage message, object state)
        {
            ErrorMessage error = message as ErrorMessage;
            if (error != null)
            {
                FailureMessage = error.Error;
                DoAnnounceComplete(false, state, new List<Peer>());
            }
            else
            {
                AnnounceResponseMessage response = (AnnounceResponseMessage)message;
                DoAnnounceComplete(true, state, response.Peers);

                //TODO seeders and leechers is not used in event.
            }
        }

        private void DoAnnounceComplete(bool successful, object state, List<Peer> peers)
        {
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, successful, peers));
        }

        #endregion

        #region connect

        private void Connect(UdpTrackerAsyncState connectState)
        {
            connectState.Message = new ConnectMessage();
            tracker.Connect(Uri.Host, Uri.Port);
            SendAndReceive(connectState);
        }

        private bool ConnectCallback(IAsyncResult ar)
        {
            UdpTrackerAsyncState trackerState = (UdpTrackerAsyncState)ar;
            try
            {
                UdpTrackerMessage msg = Receive(trackerState, trackerState.Data);
                if (msg == null)
                    return false;//bad transaction id
                ConnectResponseMessage rsp = msg as ConnectResponseMessage;
                if (rsp == null)
                {
                    //is there a possibility to have a message which is not error message or connect rsp but udp msg
                    FailureMessage = ((ErrorMessage)msg).Error;
                    return false;//error message
                }
                connectionId = rsp.ConnectionId;
                hasConnected = true;
                amConnecting = false;
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region scrape

        public override void Scrape(ScrapeParameters parameters, object state)
        {
            //LastUpdated = DateTime.Now;
            if (!hasConnected && amConnecting)
                return;

            if (!hasConnected)
            {
                amConnecting = true;
                try
                {
                    Connect(new ConnectScrapeState(parameters, ConnectScrapeCallback, state));
                }
                catch (SocketException)
                {
                    DoScrapeComplete(false, state);
                    return;
                }
            }
            else
                DoScrape(parameters, state);
        }
        private void ConnectScrapeCallback(IAsyncResult ar)
        {
            ConnectScrapeState scrapeState = (ConnectScrapeState)ar;
            try
            {
                if (scrapeState.SavedException != null)
                {
                    FailureMessage = scrapeState.SavedException.Message;
                    amConnecting = false;
                    DoScrapeComplete(false, scrapeState.AsyncState);
                    return;
                }
                if (!ConnectCallback(ar))//bad transaction id
                {
                    DoScrapeComplete(false, scrapeState.AsyncState);
                    return;
                }
                DoScrape(scrapeState.Parameters, scrapeState.AsyncState);
            }
            catch
            {
                DoScrapeComplete(false, scrapeState.AsyncState);
            }
        }
        private void DoScrape(ScrapeParameters parameters, object state)
        {
            //strange because here only one infohash???
            //or get all torrent infohash so loop on torrents of client engine
            List<byte[]> infohashs = new List<byte[]>(1);
            infohashs.Add(parameters.InfoHash.Hash);
            ConnectScrapeState scrapeState = new ConnectScrapeState(parameters, ScrapeCallback, state);
            scrapeState.Message = new ScrapeMessage(DateTime.Now.GetHashCode(), connectionId, infohashs);
            try
            {
                SendAndReceive(scrapeState);
            }
            catch (SocketException)
            {
                DoScrapeComplete(false, state);
            }
        }
        private void ScrapeCallback(IAsyncResult ar)
        {
            try
            {
                ConnectScrapeState scrapeState = (ConnectScrapeState)ar;
                if (scrapeState.SavedException != null)
                {
                    FailureMessage = scrapeState.SavedException.Message;
                    DoScrapeComplete(false, scrapeState.AsyncState);
                    return;
                }
                UdpTrackerMessage rsp = Receive(scrapeState, scrapeState.Data);
                if (!(rsp is ScrapeResponseMessage))
                {
                    DoScrapeComplete(false, scrapeState.AsyncState);
                    return;
                }
                CompleteScrape(rsp, scrapeState.AsyncState);
            }
            catch
            {
                // Nothing to do i think
            }
        }

        private void CompleteScrape(UdpTrackerMessage message, object state)
        {
            ErrorMessage error = message as ErrorMessage;
            if (error != null)
            {
                FailureMessage = error.Error;
                DoScrapeComplete(false, state);
            }
            else
            {
                //response.Scrapes not used for moment
                //ScrapeResponseMessage response = (ScrapeResponseMessage)message;
                DoScrapeComplete(true, state);
            }
        }

        private void DoScrapeComplete(bool successful, object state)
        {
            ScrapeResponseEventArgs e = new ScrapeResponseEventArgs(this, state, successful);
            RaiseScrapeComplete(e);
        }
        #endregion

        #region TimeOut System

        private void SendAndReceive(UdpTrackerAsyncState messageState)
        {
            timeout = 1;
            SendRequest(messageState);
            ReceiveAsyncResult = tracker.BeginReceive(EndReceiveMessage, messageState);
        }

        private void EndReceiveMessage(IAsyncResult result)
        {
            ReceiveAsyncResult = null;

            UdpTrackerAsyncState trackerState = (UdpTrackerAsyncState)result.AsyncState;
            try
            {
                IPEndPoint endpoint = null;
                trackerState.Data = tracker.EndReceive(result, ref endpoint);
                trackerState.Callback(trackerState);
            }
            catch (Exception ex)
            {
                trackerState.Complete(ex);
            }
        }

        private void SendRequest(UdpTrackerAsyncState requestState)
        {
            //TODO BeginSend
            byte[] buffer = requestState.Message.Encode();
            tracker.Send(buffer, buffer.Length);

            //response timeout: we try 4 times every 15 sec
            ClientEngine.MainLoop.QueueTimeout(RetryDelay, delegate
            {
                if (timeout == 0)//we receive data
                    return false;

                if (timeout <= 4)
                {
                    timeout++;
                    try
                    {
                        tracker.Send(buffer, buffer.Length);
                    }
                    catch (Exception ex)
                    {
                        timeout = 0;
                        requestState.Complete(ex);
                        return false;
                    }
                }
                else
                {
                    timeout = 0;
                    requestState.Complete(new Exception("Tracker did not respond to the connect requests"));
                    return false;
                }
                return true;
            });
        }

        private UdpTrackerMessage Receive(UdpTrackerAsyncState trackerState, byte[] receivedMessage)
        {
            timeout = 0;//we have receive so unactive the timeout
            byte[] data = receivedMessage;
            UdpTrackerMessage rsp = UdpTrackerMessage.DecodeMessage(data, 0, data.Length, MessageType.Response);

            if (trackerState.Message.TransactionId != rsp.TransactionId)
            {
                FailureMessage = "Invalid transaction Id in response from udp tracker!";
                return null;//to raise event fail outside
            }
            return rsp;
        }

        #endregion

        public override string ToString()
        {
            return "udptracker:" + connectionId;
        }

        #region async state

        abstract class UdpTrackerAsyncState : AsyncResult
        {
            public byte[] Data;
            public UdpTrackerMessage Message;

            protected UdpTrackerAsyncState(AsyncCallback callback, object state)
                : base(callback, state)
            {

            }
        }

        class ConnectAnnounceState : UdpTrackerAsyncState
        {
            public AnnounceParameters Parameters;

            public ConnectAnnounceState(AnnounceParameters parameters, AsyncCallback callback, object state)
                : base(callback, state)
            {
                Parameters = parameters;
            }
        }

        class ConnectScrapeState : UdpTrackerAsyncState
        {
            public ScrapeParameters Parameters;

            public ConnectScrapeState(ScrapeParameters parameters, AsyncCallback callback, object state)
                : base(callback, state)
            {
                Parameters = parameters;
            }
        }

        #endregion
    }
}