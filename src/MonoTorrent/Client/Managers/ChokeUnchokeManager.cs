using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;
using System.IO;

namespace MonoTorrent.Client
{
    class ChokeUnchokeManager
    {
        #region Private Fields

        private int minimumTimeBetweenReviews = 30; //seconds.  Minimum time that needs to pass before we execute a review
        private int percentOfMaxRateToSkipReview = 90; //If the latest download/upload rate is >= to this percentage of the maximum rate we should skip the review

        private DateTime timeOfLastReview; //When we last reviewed the choke/unchoke position
        private bool firstCall = true; //Indicates the first call to the TimePassed method
        private bool isDownloading = true; //Allows us to identify change in state from downloading to seeding
        private TorrentManager owningTorrent; //The torrent to which this manager belongs
        private PeerIdInternal optimisticUnchokePeer = null; //This is the peer we have optimistically unchoked, or null

        //Lists of peers held by the choke/unchoke manager
        private PeerList nascentPeers = new PeerList(PeerListType.NascentPeers); //Peers that have yet to be unchoked and downloading for a full review period
        private PeerList candidatePeers = new PeerList(PeerListType.CandidatePeers); //Peers that are candidates for unchoking based on past performance
        private PeerList optimisticUnchokeCandidates = new PeerList(PeerListType.OptimisticUnchokeCandidatePeers); //Peers that are candidates for unchoking in case they perform well

        #endregion Private Fields

        #region Constructors

        /// <summary>
        /// Creates a new choke/unchoke manager for a torrent manager
        /// </summeary>
        /// <param name="TorrentManager">The torrent manager this choke/unchoke manager belongs to</param>
        public ChokeUnchokeManager(TorrentManager TorrentManager, int MinimumTimeBetweenReviews, int PercentOfMaxRateToSkipReview)
        {
            owningTorrent = TorrentManager;
            minimumTimeBetweenReviews = MinimumTimeBetweenReviews;
            percentOfMaxRateToSkipReview = PercentOfMaxRateToSkipReview;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executed each tick of the client engine
        /// </summary>
        public void TimePassed()
        {
            //Start by identifying:
            //  the choked and interested peers
            //  the number of unchoked peers
            //Choke peers that have become disinterested at the same time
            List<PeerIdInternal> chokedInterestedPeers = new List<PeerIdInternal>();
            int interestedCount = 0;
            int unchokedCount = 0;

            foreach (PeerIdInternal connectedPeer in owningTorrent.ConnectedPeers)
            {
                lock (connectedPeer)
                {
                    if (connectedPeer.Connection == null)
                        continue;

                    // If the peer is interesting try to queue up some piece requests off him
                    // If he is choking, we will only queue a piece if there is a FastPiece we can choose
                    if (connectedPeer.Connection.AmInterested)
                        while (owningTorrent.PieceManager.AddPieceRequest(connectedPeer)) { }

                    if (!connectedPeer.Peer.IsSeeder)
                    {
                        if (!connectedPeer.Connection.IsInterested && !connectedPeer.Connection.AmChoking)
                            //This peer is disinterested and unchoked; choke it
                            Choke(connectedPeer);

                        else if (connectedPeer.Connection.IsInterested)
                        {
                            interestedCount++;
                            if (!connectedPeer.Connection.AmChoking)       //This peer is interested and unchoked, count it
                                unchokedCount++;
                            else
                                chokedInterestedPeers.Add(connectedPeer); //This peer is interested and choked, remember it and count it
                        }
                    }
                }
            }
           
            if (firstCall)
            {
                //This is the first time we've been called for this torrent; set current status and run an initial review
                isDownloading = !owningTorrent.Complete; //If progress is less than 100% we must be downloading
                firstCall = false;
                ExecuteReview();
            }
            else if (isDownloading && owningTorrent.Complete)
            {
                //The state has changed from downloading to seeding; set new status and run an initial review
                isDownloading = false;
                ExecuteReview();
            }

            else if (interestedCount <= owningTorrent.Settings.UploadSlots)
                //Since we have enough slots to satisfy everyone that's interested, unchoke them all; no review needed
                UnchokePeerList(chokedInterestedPeers);

            else if (minimumTimeBetweenReviews > 0 && (SecondsBetween(timeOfLastReview, DateTime.Now) >= minimumTimeBetweenReviews) &&
                ((isDownloading && (owningTorrent.Monitor.DownloadSpeed < (owningTorrent.Settings.MaxDownloadSpeed * percentOfMaxRateToSkipReview / 100.0))) ||
                (!isDownloading && (owningTorrent.Monitor.UploadSpeed < (owningTorrent.Settings.MaxUploadSpeed * percentOfMaxRateToSkipReview / 100.0)))))
                //Based on the time of the last review, a new review is due
                //There are more interested peers than available upload slots
                //If we're downloading, the download rate is insufficient to skip the review
                //If we're seeding, the upload rate is insufficient to skip the review
                //So, we need a review
                ExecuteReview();

            else
                //We're not going to do a review this time
                //Allocate any available slots based on the results of the last review
                AllocateSlots(unchokedCount);
        }

        #endregion

        #region Private Methods

        private IEnumerable<PeerList> AllLists()
        {
            yield return nascentPeers;
            yield return candidatePeers;
            yield return optimisticUnchokeCandidates;
        }

        private void AllocateSlots(int alreadyUnchoked)
        {
            PeerIdInternal peer = null;

            //Allocate interested peers to slots based on the latest review results
            //First determine how many slots are available to be allocated
            int availableSlots = owningTorrent.Settings.UploadSlots - alreadyUnchoked;

            // If there are no slots, just return
            if (availableSlots <= 0)
                return;

            // Check the peer lists (nascent, then candidate then optimistic unchoke)
            // for an interested choked peer, if one is found, unchoke it.
            foreach (PeerList list in AllLists())
                while ((peer = list.GetFirstInterestedChokedPeer()) != null && (availableSlots-- > 0))
                    Unchoke(peer);

            // Is this section of code needed? Shouldn't all the peers be in one of the lists above?
            while (availableSlots-- > 0)
            {
                //No peers left, look for any interested choked peers
                bool peerFound = false;
                foreach (PeerIdInternal connectedPeer in owningTorrent.ConnectedPeers)
                {
                    lock (connectedPeer)
                        if (connectedPeer.Connection != null)
                        {
                            if (connectedPeer.Connection.IsInterested && connectedPeer.Connection.AmChoking)
                            {
                                Unchoke(connectedPeer);
                                peerFound = true;
                                break;
                            }
                        }
                }
                if (!peerFound)
                    //No interested choked peers anywhere, we're done
                    break;
            }
        }



        private void Choke(PeerIdInternal peer)
        {
            //Choke the supplied peer

            if (peer.Connection.AmChoking)
                //We're already choking this peer, nothing to do
                return;

            // FIXME: This doesn't need to be done anymore. This was a horrible hack
            // so i could implement a 'stupid' choke/unchoke algorithm. The new algorithm doesn't
            // use this, so don't reset down to zero :)
            peer.Connection.PiecesSent = 0;
            peer.Connection.AmChoking = true;
            owningTorrent.UploadingTo--;
            RejectPendingRequests(peer);
            peer.Connection.EnqueueAt(new ChokeMessage(), 0);
            Logger.Log("Choking: " + owningTorrent.UploadingTo);
            //			Send2Log("Choking: " + PeerToChoke.Location);
        }

        private void ExecuteReview()
        {
            //Review current choke/unchoke position and adjust as necessary
            //Start by populating the lists of peers, then allocate available slots oberving the unchoke limit

            //Clear the lists to start with
            nascentPeers.Clear();
            candidatePeers.Clear();
            optimisticUnchokeCandidates.Clear();

            //No review needed or disabled by the torrent settings

            /////???Remove when working
            ////Log peer status - temporary
            //if (isLogging)
            //{
            //    StringBuilder logEntry = new StringBuilder(1000);
            //    logEntry.Append(B2YN(owningTorrent.State == TorrentState.Seeding) + timeOfLastReview.ToString() + "," + DateTime.Now.ToString() + ";");
            //    foreach (PeerIdInternal connectedPeer in owningTorrent.Peers.ConnectedPeers)
            //    {
            //        if (connectedPeer.Connection != null)
            //            if (!connectedPeer.Peer.IsSeeder)
            //            {
            //                {
            //                    logEntry.Append(
            //                        B2YN(connectedPeer.Peer.IsSeeder) +
            //                        B2YN(connectedPeer.Connection.AmChoking) +
            //                        B2YN(connectedPeer.Connection.AmInterested) +
            //                        B2YN(connectedPeer.Connection.IsInterested) +
            //                        B2YN(connectedPeer.Peer.FirstReviewPeriod) +
            //                        connectedPeer.Connection.Monitor.DataBytesDownloaded.ToString() + "," +
            //                        connectedPeer.Peer.BytesDownloadedAtLastReview.ToString() + "," +
            //                        connectedPeer.Connection.Monitor.DataBytesUploaded.ToString() + "," +
            //                        connectedPeer.Peer.BytesUploadedAtLastReview.ToString() + "," +
            //                        connectedPeer.Peer.Location);
            //                    DateTime? lastUnchoked = connectedPeer.Peer.LastUnchoked;
            //                    if (lastUnchoked.HasValue)
            //                        logEntry.Append(
            //                            "," +
            //                            lastUnchoked.ToString() + "," +
            //                            SecondsBetween(lastUnchoked.Value, DateTime.Now).ToString());
            //                    logEntry.Append(";");
            //                }
            //            }
            //    }
            //    Send2Log(logEntry.ToString());
            //}

            //Scan the peers building the lists as we go and count number of unchoked peers

            int unchokedPeers = 0;

            foreach (PeerIdInternal connectedPeer in owningTorrent.ConnectedPeers)
            {
                lock (connectedPeer)
                    if (connectedPeer.Connection != null)
                    {
                        if (!connectedPeer.Peer.IsSeeder)
                        {
                            //Determine common values for use in this routine
                            double timeSinceLastReview = SecondsBetween(timeOfLastReview, DateTime.Now);
                            double timeUnchoked = 0;
                            if (!connectedPeer.Connection.AmChoking)
                            {
                                timeUnchoked = SecondsBetween(connectedPeer.Connection.LastUnchoked.Value, DateTime.Now);
                                unchokedPeers++;
                            }
                            long bytesTransferred = 0;
                            if (!isDownloading)
                                //We are seeding the torrent; determine bytesTransferred as bytes uploaded
                                bytesTransferred = connectedPeer.Connection.Monitor.DataBytesUploaded - connectedPeer.Connection.BytesUploadedAtLastReview;
                            else
                                //The peer is unchoked and we are downloading the torrent; determine bytesTransferred as bytes downloaded
                                bytesTransferred = connectedPeer.Connection.Monitor.DataBytesDownloaded - connectedPeer.Connection.BytesDownloadedAtLastReview;

                            //Reset review up and download rates to zero; peers are therefore non-responders unless we determine otherwise
                            connectedPeer.Connection.LastReviewDownloadRate = 0;
                            connectedPeer.Connection.LastReviewUploadRate = 0;

                            if (!connectedPeer.Connection.AmChoking &&
                                (timeUnchoked < minimumTimeBetweenReviews ||
                                (connectedPeer.Connection.FirstReviewPeriod && bytesTransferred > 0)))
                                //The peer is unchoked but either it has not been unchoked for the warm up interval,
                                // or it is the first full period and only just started transferring data
                                nascentPeers.Add(connectedPeer);

                            else if ((timeUnchoked >= minimumTimeBetweenReviews) && bytesTransferred > 0)
                            //The peer is unchoked, has been for the warm up period and has transferred data in the period
                            {
                                //Add to peers that are candidates for unchoking based on their performance
                                candidatePeers.Add(connectedPeer);
                                //Calculate the latest up/downloadrate
                                connectedPeer.Connection.LastReviewUploadRate = (connectedPeer.Connection.Monitor.DataBytesUploaded - connectedPeer.Connection.BytesUploadedAtLastReview) / timeSinceLastReview;
                                connectedPeer.Connection.LastReviewDownloadRate = (connectedPeer.Connection.Monitor.DataBytesDownloaded - connectedPeer.Connection.BytesDownloadedAtLastReview) / timeSinceLastReview;
                            }

                            else if (isDownloading && connectedPeer.Connection.IsInterested && connectedPeer.Connection.AmChoking && bytesTransferred > 0)
                            //A peer is optimistically unchoking us.  Take the maximum of their current download rate and their download rate over the
                            //	review period since they might have only just unchoked us and we don't want to miss out on a good opportunity.  Upload
                            // rate is less important, so just take an average over the period.
                            {
                                //Add to peers that are candidates for unchoking based on their performance
                                candidatePeers.Add(connectedPeer);
                                //Calculate the latest up/downloadrate
                                connectedPeer.Connection.LastReviewUploadRate = (connectedPeer.Connection.Monitor.DataBytesUploaded - connectedPeer.Connection.BytesUploadedAtLastReview) / timeSinceLastReview;
                                connectedPeer.Connection.LastReviewDownloadRate = Math.Max((connectedPeer.Connection.Monitor.DataBytesDownloaded - connectedPeer.Connection.BytesDownloadedAtLastReview) / timeSinceLastReview,
                                    connectedPeer.Connection.Monitor.DownloadSpeed * 1000);
                            }

                            else if (connectedPeer.Connection.IsInterested)
                                //All other interested peers are candidates for optimistic unchoking
                                optimisticUnchokeCandidates.Add(connectedPeer);

                            //Remember the number of bytes up and downloaded for the next review
                            connectedPeer.Connection.BytesUploadedAtLastReview = connectedPeer.Connection.Monitor.DataBytesUploaded;
                            connectedPeer.Connection.BytesDownloadedAtLastReview = connectedPeer.Connection.Monitor.DataBytesDownloaded;

                            //If the peer has been unchoked for longer than one review period, unset FirstReviewPeriod
                            if (timeUnchoked >= minimumTimeBetweenReviews)
                                connectedPeer.Connection.FirstReviewPeriod = false;
                        }
                    }
            }
            //				Send2Log(nascentPeers.Count.ToString() + "," + candidatePeers.Count.ToString() + "," + optimisticUnchokeCandidates.Count.ToString());

            //Now sort the lists of peers so we are ready to reallocate them
            nascentPeers.Sort(owningTorrent.State == TorrentState.Seeding);
            candidatePeers.Sort(owningTorrent.State == TorrentState.Seeding);
            optimisticUnchokeCandidates.Sort(owningTorrent.State == TorrentState.Seeding);
            //				if (isLogging)
            //				{
            //					string x = "";
            //					while (optimisticUnchokeCandidates.MorePeers)
            //						x += optimisticUnchokeCandidates.GetNextPeer().Location + ";";
            //					Send2Log(x);
            //					optimisticUnchokeCandidates.StartScan();
            //				}

            //If there is an optimistic unchoke peer and it is nascent, we should reallocate all the available slots
            //Otherwise, if all the slots are allocated to nascent peers, don't try an optimistic unchoke this time
            if (nascentPeers.Count >= owningTorrent.Settings.UploadSlots || nascentPeers.Includes(optimisticUnchokePeer))
                ReallocateSlots(owningTorrent.Settings.UploadSlots, unchokedPeers);
            else
            {
                //We should reallocate all the slots but one and allocate the last slot to the next optimistic unchoke peer
                ReallocateSlots(owningTorrent.Settings.UploadSlots - 1, unchokedPeers);
                //In case we don't find a suitable peer, make the optimistic unchoke peer null
                PeerIdInternal oup = optimisticUnchokeCandidates.GetOUPeer();
                if (oup != null)
                {
                    //						Send2Log("OUP: " + oup.Location);
                    Unchoke(oup);
                    optimisticUnchokePeer = oup;
                }
            }

            //Finally, deallocate (any) remaining peers from the three lists
            while (nascentPeers.MorePeers)
            {
                PeerIdInternal nextPeer = nascentPeers.GetNextPeer();
                if (!nextPeer.Connection.AmChoking)
                    Choke(nextPeer);
            }
            while (candidatePeers.MorePeers)
            {
                PeerIdInternal nextPeer = candidatePeers.GetNextPeer();
                if (!nextPeer.Connection.AmChoking)
                    Choke(nextPeer);
            }
            while (optimisticUnchokeCandidates.MorePeers)
            {
                PeerIdInternal nextPeer = optimisticUnchokeCandidates.GetNextPeer();
                if (!nextPeer.Connection.AmChoking)
                    //This peer is currently unchoked, choke it unless it is the optimistic unchoke peer
                    if (optimisticUnchokePeer == null)
                        //There isn't an optimistic unchoke peer
                        Choke(nextPeer);
                    else if (!nextPeer.Equals(optimisticUnchokePeer))
                        //This isn't the optimistic unchoke peer
                        Choke(nextPeer);
            }

            timeOfLastReview = DateTime.Now;
        }

        /// <summary>
        /// Reallocates the specified number of upload slots
        /// </summary>
        /// <param name="NumberOfSlots"></param>The number of slots we should reallocate
        private void ReallocateSlots(int NumberOfSlots, int NumberOfUnchokedPeers)
        {
            //First determine the maximum number of peers we can unchoke in this review = maximum of:
            //  half the number of upload slots; and
            //  slots not already unchoked
            int maximumUnchokes = NumberOfSlots / 2;
            maximumUnchokes = Math.Max(maximumUnchokes, NumberOfSlots - NumberOfUnchokedPeers);

            //Now work through the lists of peers in turn until we have allocated all the slots
            while (NumberOfSlots > 0)
            {
                if (nascentPeers.MorePeers)
                    ReallocateSlot(ref NumberOfSlots, ref maximumUnchokes, nascentPeers.GetNextPeer());
                else if (candidatePeers.MorePeers)
                    ReallocateSlot(ref NumberOfSlots, ref maximumUnchokes, candidatePeers.GetNextPeer());
                else if (optimisticUnchokeCandidates.MorePeers)
                    ReallocateSlot(ref NumberOfSlots, ref maximumUnchokes, optimisticUnchokeCandidates.GetNextPeer());
                else
                    //No more peers left, we're done
                    break;
            }
        }

        /// <summary>
        /// Reallocates the next slot with the specified peer if we can
        /// </summary>
        /// <param name="NumberOfSlots"></param>The number of slots left to reallocate
        /// <param name="MaximumUnchokes"></param>The number of peers we can unchoke
        /// <param name="Peer"></param>The peer to consider for reallocation
        private void ReallocateSlot(ref int NumberOfSlots, ref int MaximumUnchokes, PeerIdInternal peer)
        {
            if (!peer.Connection.AmChoking)
            {
                //This peer is already unchoked, just decrement the number of slots
                NumberOfSlots--;
                //				Send2Log("Leave: " + peer.Location);
            }
            else if (MaximumUnchokes > 0)
            {
                //This peer is choked and we've not yet reached the limit of unchokes, unchoke it
                Unchoke(peer);
                MaximumUnchokes--;
                NumberOfSlots--;
            }
        }

        /// <summary>
        /// Checks the send queue of the peer to see if there are any outstanding pieces which they requested
        /// and rejects them as necessary
        /// </summary>
        /// <param name="Peer"></param>
        private void RejectPendingRequests(PeerIdInternal Peer)
        {
            IPeerMessageInternal message;
            PieceMessage pieceMessage;
            int length = Peer.Connection.QueueLength;

            for (int i = 0; i < length; i++)
            {
                message = Peer.Connection.Dequeue();
                if (!(message is PieceMessage))
                {
                    Peer.Connection.Enqueue(message);
                    continue;
                }

                pieceMessage = (PieceMessage)message;

                // If the peer doesn't support fast peer, then we will never requeue the message
                if (!(Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
                {
                    Peer.Connection.IsRequestingPiecesCount--;
                    continue;
                }

                // If the peer supports fast peer, queue the message if it is an AllowedFast piece
                // Otherwise send a reject message for the piece
                if (Peer.Connection.AmAllowedFastPieces.Contains(pieceMessage.PieceIndex))
                    Peer.Connection.Enqueue(pieceMessage);
                else
                {
                    Peer.Connection.IsRequestingPiecesCount--;
                    Peer.Connection.Enqueue(new RejectRequestMessage(pieceMessage));
                }
            }
        }

        private static double SecondsBetween(DateTime FirstTime, DateTime SecondTime)
        {
            //Calculate the number of seconds and fractions of a second that have elapsed between the first time and the second
            TimeSpan difference = SecondTime.Subtract(FirstTime);
            return difference.TotalMilliseconds / 1000;
        }

        private void Unchoke(PeerIdInternal PeerToUnchoke)
        {
            //Unchoke the supplied peer

            if (!PeerToUnchoke.Connection.AmChoking)
                //We're already unchoking this peer, nothing to do
                return;

            PeerToUnchoke.Connection.PiecesSent = 0;
            PeerToUnchoke.Connection.AmChoking = false;
            owningTorrent.UploadingTo++;
            PeerToUnchoke.Connection.EnqueueAt(new UnchokeMessage(), 0);
            PeerToUnchoke.Connection.LastUnchoked = DateTime.Now;
            PeerToUnchoke.Connection.FirstReviewPeriod = true;
            Logger.Log("Unchoking: " + owningTorrent.UploadingTo);
            //			Send2Log("Unchoking: " + PeerToUnchoke.Location);
        }

        private void UnchokePeerList(List<PeerIdInternal> PeerList)
        {
            //Unchoke all the peers in the supplied list
            foreach (PeerIdInternal PeerToUnchoke in PeerList)
                Unchoke(PeerToUnchoke);
        }

        #endregion

        #region Temporary stuff for logging

        //FileStream logStream;
        //StreamWriter logStreamWriter;
        //bool isLogging = true;

        //private void Send2Log(string LogEntry)
        //{
        //    if (isLogging)
        //    {
        //        if (logStream == null)
        //        {
        //            string logFileName = owningTorrent.Torrent.Name + ".ChokeUnchoke.Log";
        //            logStream = new FileStream(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),logFileName),FileMode.Append);
        //            logStreamWriter = new StreamWriter(logStream, System.Text.Encoding.ASCII);
        //            logStreamWriter.AutoFlush=true;
        //        }
        //        logStreamWriter.WriteLine(DateTime.Now.ToString() + ":" + LogEntry);
        //    }
        //}

        //private string B2YN(bool Boolean)
        //{
        //    if (Boolean)
        //        return "Y,";
        //    else
        //        return "N,";
        //}

        #endregion
    }
}