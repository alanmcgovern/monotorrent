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
   public class UdpTracker : Tracker
   {
       private long connectionId;
       private UdpClient tracker;
       private IPEndPoint endpoint;
       bool hasConnected;
       bool amConnecting;
       int timeout;

       public UdpTracker(Uri announceUrl)
           : base(announceUrl)
       {
           CanScrape = true;
           CanAnnounce = true;
           tracker = new UdpClient(announceUrl.Host, announceUrl.Port);
           endpoint = (IPEndPoint)tracker.Client.RemoteEndPoint;
       }

       #region announce

       public override void Announce(AnnounceParameters parameters, object state)
       {
           //LastUpdated = DateTime.Now;
           if (!hasConnected && amConnecting)
               return;

           if (!hasConnected)
           {
               amConnecting = true;
               try
               {
                   Connect(new ConnectAnnounceState(parameters, state), new AsyncCallback(ConnectAnnounceCallback));
               }
               catch (SocketException e)
               {
                   DoAnnounceComplete(false, state, null);
                   return;
               }               
           }
           else
               DoAnnounce(parameters, state);
       }

       private void DoAnnounce(AnnounceParameters parameters, object state)
       {
           AnnounceMessage m = new AnnounceMessage(DateTime.Now.GetHashCode(), connectionId, parameters);
           try
           {
               SendAndReceive(m, new AsyncCallback(AnnounceCallback), state);
           }
           catch (SocketException e)
           {
               DoAnnounceComplete(false, state, null);
           }
       }

       private void ConnectAnnounceCallback(IAsyncResult ar)
       {
           ConnectAnnounceState state = (ConnectAnnounceState)((UDPTrackerState)ar.AsyncState).state;
           if (ar is FakeAsyncResult)
           {
               FailureMessage = "Send 4 times connect for announce to tracker but timeout!";
               amConnecting = false;
               DoAnnounceComplete(false, state, null);
               return;
           }
           if (!ConnectCallback(ar))//bad transaction id
           {
               DoAnnounceComplete(false, state, null);
               return;
           }
           DoAnnounce(state.parameters, state.state);
       }

       private void AnnounceCallback(IAsyncResult ar)
       {
           object state = ((UDPTrackerState)ar.AsyncState).state;
           if (ar is FakeAsyncResult)
           {
               FailureMessage = "Send 4 times announce to tracker but timeout!";
               DoAnnounceComplete(false, state, null);
               return;
           }
           UdpTrackerMessage rsp = Receive(ar);
           if (rsp == null)
           {
               DoScrapeComplete(false, state);
               return;
           }

           MinUpdateInterval = ((AnnounceResponseMessage)rsp).Interval;
           CompleteAnnounce(rsp, state);
       }

       private void CompleteAnnounce(UdpTrackerMessage message, object state)
       {
           ErrorMessage error = message as ErrorMessage;
           if (error != null)
           {
               FailureMessage = error.Error;
               DoAnnounceComplete(false, state, null);
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
           AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(this, state, false);
           e.Successful = successful;
           if (successful)
                e.Peers.AddRange(peers);
           RaiseAnnounceComplete(e);
       }

       #endregion

       #region connect

       private void Connect(object state, AsyncCallback callback )
       {
           ConnectMessage message = new ConnectMessage();
           tracker.Connect(Uri.Host, Uri.Port);
           SendAndReceive(message, callback, state);
       }

       private bool ConnectCallback(IAsyncResult ar)
       {
           UdpTrackerMessage msg = Receive(ar);
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
                    Connect(new ConnectScrapeState(parameters, state), new AsyncCallback(ConnectScrapeCallback));
               }
               catch (SocketException e)
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
           ConnectScrapeState state = (ConnectScrapeState)((UDPTrackerState)ar.AsyncState).state;
           if (ar is FakeAsyncResult)
           {
               FailureMessage = "Send 4 times connect for scrape to tracker but timeout!";
               amConnecting = false;
               DoScrapeComplete(false, state);
               return;
           }
           if (!ConnectCallback(ar))//bad transaction id
           {
               DoScrapeComplete(false, state);
               return;
           }
           DoScrape(state.parameters, state.state);
       }
       private void DoScrape(ScrapeParameters parameters, object state)
       {
           //strange because here only one infohash???
           //or get all torrent infohash so loop on torrents of client engine
           List<byte[]> infohashs= new List<byte[]>(1);
           infohashs.Add(parameters.InfoHash.Hash);
           ScrapeMessage m = new ScrapeMessage(DateTime.Now.GetHashCode(), connectionId, infohashs);
           try
           {
               SendAndReceive(m, new AsyncCallback(ScrapeCallback), state);
           }
           catch (SocketException e)
           {
               DoScrapeComplete(false, state);
           }
       }
       private void ScrapeCallback(IAsyncResult ar)
       {
           object state = ((UDPTrackerState)ar.AsyncState).state;
           if (ar is FakeAsyncResult)
           {
               FailureMessage = "Send 4 times scrape to tracker but timeout!";
               DoScrapeComplete(false, state);
               return;
           }
           UdpTrackerMessage rsp = Receive(ar);
           if (rsp == null)
           {
               DoScrapeComplete(false, state);
               return;
           }
           CompleteScrape(rsp, state);
       }
              
       private void CompleteScrape(UdpTrackerMessage message, object state)
       {
           ScrapeResponseEventArgs e = new ScrapeResponseEventArgs(this, state, false);
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

       private void SendAndReceive(UdpTrackerMessage m, AsyncCallback callback, object state)
       {
           timeout = 1;
           SendRequest(m, callback, state);
           tracker.BeginReceive(ClientEngine.MainLoop.Wrap(callback), new UDPTrackerState(m, state));
       }
       private void SendRequest(UdpTrackerMessage message, AsyncCallback callback, object state)
       {
           //TODO BeginSend
           tracker.Send(message.Encode(), message.ByteLength);

           //response timeout: we try 4 times every 15 sec
           ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromSeconds(15), delegate
           {
               if (timeout == 0)//we receive data
                   return true;

               if (timeout <= 4)
               {
                   timeout++;
                   SendRequest(message, callback, state);
               }
               else
               {
                   timeout = 0;
                   callback(new FakeAsyncResult(state));
               }
               return false;
           });
       }

       private UdpTrackerMessage Receive(IAsyncResult ar)
       {
           timeout = 0;//we have receive so unactive the timeout
           byte[] data = tracker.EndReceive(ar, ref endpoint);
           UdpTrackerMessage rsp = UdpTrackerMessage.DecodeMessage(data, 0, data.Length, MessageType.Response);

           if (((UDPTrackerState)ar.AsyncState).m.TransactionId != rsp.TransactionId)
           {
               FailureMessage = "Invalid transaction Id in response from udp tracker!";
               return null;//to raise event fail outside
           }
           return rsp;
       }

#endregion

       public override string ToString()
       {
           return "udptracker:"+connectionId;
       }

       #region async state

       class UDPTrackerState
       {
           public object state;
           public UdpTrackerMessage m;
           public UDPTrackerState(UdpTrackerMessage m, object state)
           { 
                this.m = m;
                this.state = state;
           }
       }

       class ConnectAnnounceState
       {
           public object state;
           public AnnounceParameters parameters;
           public ConnectAnnounceState(AnnounceParameters parameters, object state)
           {
               this.parameters = parameters;
               this.state = state;
           }
       }
       class ConnectScrapeState
       {
           public object state;
           public ScrapeParameters parameters;
           public ConnectScrapeState(ScrapeParameters parameters, object state)
           {
               this.parameters = parameters;
               this.state = state;
           }
       }

       //if use it outside of timeout, we have to store a string error and return it in FailureMessage
       class FakeAsyncResult : IAsyncResult
       {
           private object state;
           public FakeAsyncResult(object state)
           {
               this.state = state;
           }
           public object AsyncState
           {
               get { return state; }
           }

           public WaitHandle AsyncWaitHandle
           {
               get { throw new NotImplementedException(); }
           }

           public bool CompletedSynchronously
           {
               get { throw new NotImplementedException(); }
           }

           public bool IsCompleted
           {
               get { return false; }
           }
       }

       #endregion
   }
}