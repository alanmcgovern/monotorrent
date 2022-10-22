using System;
using System.Collections.Generic;
using System.Threading;

using MonoTorrent.Messages.Peer;

namespace MonoTorrent.Client
{
    class ChokeUnchokeManager : IUnchoker
    {
        readonly TimeSpan minimumTimeBetweenReviews = TimeSpan.FromSeconds (30); //  Minimum time that needs to pass before we execute a review

        ValueStopwatch timeSinceLastReview; //When we last reviewed the choke/unchoke position
        PeerId? optimisticUnchokePeer; //This is the peer we have optimistically unchoked, or null

        //Lists of peers held by the choke/unchoke manager
        readonly List<PeerId> chokedInterestedPeers = new List<PeerId> ();
        readonly PeerList nascentPeers = new PeerList (PeerListType.NascentPeers); //Peers that have yet to be unchoked and downloading for a full review period
        readonly PeerList candidatePeers = new PeerList (PeerListType.CandidatePeers); //Peers that are candidates for unchoking based on past performance
        readonly PeerList optimisticUnchokeCandidates = new PeerList (PeerListType.OptimisticUnchokeCandidatePeers); //Peers that are candidates for unchoking in case they perform well

        readonly IUnchokeable Unchokeable; //The torrent to which this manager belongs

        #region Constructors

        public ChokeUnchokeManager (IUnchokeable unchokeable)
        {
            Unchokeable = unchokeable;
            Unchokeable.StateChanged += (o, e) => timeSinceLastReview = new ValueStopwatch ();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executed each tick of the client engine
        /// </summary>
        public void UnchokeReview ()
        {
            int interestedCount = 0;
            int unchokedCount = 0;
            chokedInterestedPeers.Clear ();
            nascentPeers.RemoveDisconnected ();
            candidatePeers.RemoveDisconnected ();
            optimisticUnchokeCandidates.RemoveDisconnected ();

            // Run a review even if we can unchoke all the peers who are currently choked. If more
            // peers become interested in the future we will need the results of a review to
            // choose the 'best' one.
            if (!timeSinceLastReview.IsRunning || timeSinceLastReview.Elapsed >= minimumTimeBetweenReviews) {
                //Based on the time of the last review, a new review is due
                //There are more interested peers than available upload slots
                //If we're downloading, the download rate is insufficient to skip the review
                //If we're seeding, the upload rate is insufficient to skip the review
                //So, we need a review
                ExecuteReview ();
                timeSinceLastReview = ValueStopwatch.StartNew ();
            }

            // The review may have already unchoked peers. Bail early
            // if all the slots are full.
            foreach (var peer in Unchokeable.Peers) {
                // Choke any unchoked peers which are no longer interested
                if (!peer.IsInterested && !peer.AmChoking) {
                    Choke (peer);
                } else if (peer.IsInterested) {
                    interestedCount++;
                    if (peer.AmChoking)
                        chokedInterestedPeers.Add (peer);
                    else
                        unchokedCount++;
                }
            }

            if (interestedCount > 0 && interestedCount <= Unchokeable.UploadSlots || Unchokeable.UploadSlots == 0) {
                // We have enough slots to satisfy everyone, so unchoke them all
                foreach (var peer in chokedInterestedPeers)
                    Unchoke (peer);
            } else {
                // Allocate slots based off the most recent review
                AllocateSlots (unchokedCount);
            }
        }

        #endregion

        #region Private Methods

        IEnumerable<PeerList> AllLists ()
        {
            yield return nascentPeers;
            yield return candidatePeers;
            yield return optimisticUnchokeCandidates;
        }

        void AllocateSlots (int alreadyUnchoked)
        {
            PeerId? peer;

            //Allocate interested peers to slots based on the latest review results
            //First determine how many slots are available to be allocated
            int availableSlots = Unchokeable.UploadSlots == 0 ? 10240 : Unchokeable.UploadSlots - alreadyUnchoked;

            // If there are no slots, just return
            if (availableSlots <= 0)
                return;

            // Check the peer lists (nascent, then candidate then optimistic unchoke)
            // for an interested choked peer, if one is found, unchoke it.
            foreach (PeerList list in AllLists ())
                while ((peer = list.GetFirstInterestedChokedPeer ()) != null && (availableSlots-- > 0))
                    Unchoke (peer);

            // In the time that has passed since the last review we might have connected to more peers
            // that don't appear in AllLists.  It's also possible we have not yet run a review in
            // which case AllLists will be empty.  Fill remaining slots with unchoked, interested peers
            // from the full list.
            while (availableSlots-- > 0) {
                //No peers left, look for any interested choked peers
                bool peerFound = false;
                foreach (PeerId connectedPeer in Unchokeable.Peers) {
                    if (connectedPeer.IsInterested && connectedPeer.AmChoking) {
                        Unchoke (connectedPeer);
                        peerFound = true;
                        break;
                    }
                }
                if (!peerFound)
                    //No interested choked peers anywhere, we're done
                    break;
            }
        }

        void Choke (PeerId peer)
        {
            if (peer.AmChoking)
                throw new InvalidOperationException ("Attempting to choke a peer who is already choked");

            peer.AmChoking = true;
            Unchokeable.UploadingTo--;
            peer.MessageQueue.EnqueueAt (0, ChokeMessage.Instance, default);
            RejectPendingRequests (peer);
            peer.LastUnchoked = new ValueStopwatch ();
        }

        void ExecuteReview ()
        {
            //Review current choke/unchoke position and adjust as necessary
            //Start by populating the lists of peers, then allocate available slots oberving the unchoke limit

            //Clear the lists to start with
            nascentPeers.Clear ();
            candidatePeers.Clear ();
            optimisticUnchokeCandidates.Clear ();

            int unchokedPeers = 0;

            foreach (PeerId connectedPeer in Unchokeable.Peers) {
                if (!connectedPeer.Peer.IsSeeder) {
                    //Determine common values for use in this routine
                    TimeSpan timeUnchoked = TimeSpan.Zero;
                    if (!connectedPeer.AmChoking) {
                        timeUnchoked = connectedPeer.LastUnchoked.Elapsed;
                        unchokedPeers++;
                    }
                    long bytesTransferred = 0;
                    if (Unchokeable.Seeding)
                        //We are seeding the torrent; determine bytesTransferred as bytes uploaded
                        bytesTransferred = connectedPeer.Monitor.DataBytesSent - connectedPeer.BytesUploadedAtLastReview;
                    else
                        //The peer is unchoked and we are downloading the torrent; determine bytesTransferred as bytes downloaded
                        bytesTransferred = connectedPeer.Monitor.DataBytesReceived - connectedPeer.BytesDownloadedAtLastReview;

                    //Reset review up and download rates to zero; peers are therefore non-responders unless we determine otherwise
                    connectedPeer.LastReviewDownloadRate = 0;
                    connectedPeer.LastReviewUploadRate = 0;

                    if (!connectedPeer.AmChoking &&
                        (timeUnchoked < minimumTimeBetweenReviews ||
                        (connectedPeer.FirstReviewPeriod && bytesTransferred > 0)))
                        //The peer is unchoked but either it has not been unchoked for the warm up interval,
                        // or it is the first full period and only just started transferring data
                        nascentPeers.Add (connectedPeer);

                    else if ((timeUnchoked >= minimumTimeBetweenReviews) && bytesTransferred > 0)
                    //The peer is unchoked, has been for the warm up period and has transferred data in the period
                    {
                        //Add to peers that are candidates for unchoking based on their performance
                        candidatePeers.Add (connectedPeer);
                        //Calculate the latest up/downloadrate
                        connectedPeer.LastReviewUploadRate = (connectedPeer.Monitor.DataBytesSent - connectedPeer.BytesUploadedAtLastReview) / timeSinceLastReview.Elapsed.TotalSeconds;
                        connectedPeer.LastReviewDownloadRate = (connectedPeer.Monitor.DataBytesReceived - connectedPeer.BytesDownloadedAtLastReview) / timeSinceLastReview.Elapsed.TotalSeconds;
                    } else if (!Unchokeable.Seeding && connectedPeer.IsInterested && connectedPeer.AmChoking && bytesTransferred > 0)
                    //A peer is optimistically unchoking us.  Take the maximum of their current download rate and their download rate over the
                    //	review period since they might have only just unchoked us and we don't want to miss out on a good opportunity.  Upload
                    // rate is less important, so just take an average over the period.
                    {
                        //Add to peers that are candidates for unchoking based on their performance
                        candidatePeers.Add (connectedPeer);
                        //Calculate the latest up/downloadrate
                        connectedPeer.LastReviewUploadRate = (connectedPeer.Monitor.DataBytesSent - connectedPeer.BytesUploadedAtLastReview) / timeSinceLastReview.Elapsed.TotalSeconds;
                        connectedPeer.LastReviewDownloadRate = Math.Max ((connectedPeer.Monitor.DataBytesReceived - connectedPeer.BytesDownloadedAtLastReview) / timeSinceLastReview.Elapsed.TotalSeconds,
                            connectedPeer.Monitor.DownloadRate);
                    } else if (connectedPeer.IsInterested)
                        //All other interested peers are candidates for optimistic unchoking
                        optimisticUnchokeCandidates.Add (connectedPeer);

                    //Remember the number of bytes up and downloaded for the next review
                    connectedPeer.BytesUploadedAtLastReview = connectedPeer.Monitor.DataBytesSent;
                    connectedPeer.BytesDownloadedAtLastReview = connectedPeer.Monitor.DataBytesReceived;

                    //If the peer has been unchoked for longer than one review period, unset FirstReviewPeriod
                    if (timeUnchoked >= minimumTimeBetweenReviews)
                        connectedPeer.FirstReviewPeriod = false;
                }
            }

            //Now sort the lists of peers so we are ready to reallocate them
            nascentPeers.Sort (Unchokeable.Seeding);
            candidatePeers.Sort (Unchokeable.Seeding);
            optimisticUnchokeCandidates.Sort (Unchokeable.Seeding);

            //If there is an optimistic unchoke peer and it is nascent, we should reallocate all the available slots
            //Otherwise, if all the slots are allocated to nascent peers, don't try an optimistic unchoke this time
            if (nascentPeers.Count >= Unchokeable.UploadSlots || (!(optimisticUnchokePeer is null) && nascentPeers.Contains (optimisticUnchokePeer)))
                ReallocateSlots (Unchokeable.UploadSlots, unchokedPeers);
            else {
                //We should reallocate all the slots but one and allocate the last slot to the next optimistic unchoke peer
                ReallocateSlots (Unchokeable.UploadSlots - 1, unchokedPeers);
                //In case we don't find a suitable peer, make the optimistic unchoke peer null
                PeerId? oup = optimisticUnchokeCandidates.GetOUPeer ();
                if (oup != null) {
                    Unchoke (oup);
                    optimisticUnchokePeer = oup;
                }
            }

            //Finally, deallocate (any) remaining peers from the three lists
            while (nascentPeers.MorePeers) {
                PeerId? nextPeer = nascentPeers.GetNextPeer ();
                if (nextPeer != null && !nextPeer.AmChoking)
                    Choke (nextPeer);
            }
            while (candidatePeers.MorePeers) {
                PeerId? nextPeer = candidatePeers.GetNextPeer ();
                if (nextPeer != null && !nextPeer.AmChoking)
                    Choke (nextPeer);
            }
            while (optimisticUnchokeCandidates.MorePeers) {
                PeerId? nextPeer = optimisticUnchokeCandidates.GetNextPeer ();
                if (nextPeer != null && !nextPeer.AmChoking)
                    //This peer is currently unchoked, choke it unless it is the optimistic unchoke peer
                    if (optimisticUnchokePeer == null)
                        //There isn't an optimistic unchoke peer
                        Choke (nextPeer);
                    else if (!nextPeer.Equals (optimisticUnchokePeer))
                        //This isn't the optimistic unchoke peer
                        Choke (nextPeer);
            }
        }

        /// <summary>
        /// Reallocates the specified number of upload slots
        /// </summary>
        /// <param name="NumberOfSlots">The number of slots we should reallocate</param>
        /// <param name="NumberOfUnchokedPeers">The number of peers which are currently unchoked.</param>
        void ReallocateSlots (int NumberOfSlots, int NumberOfUnchokedPeers)
        {
            //First determine the maximum number of peers we can unchoke in this review = maximum of:
            //  half the number of upload slots; and
            //  slots not already unchoked
            int maximumUnchokes = NumberOfSlots / 2;
            maximumUnchokes = Math.Max (maximumUnchokes, NumberOfSlots - NumberOfUnchokedPeers);

            //Now work through the lists of peers in turn until we have allocated all the slots
            while (NumberOfSlots > 0) {
                if (nascentPeers.MorePeers)
                    ReallocateSlot (ref NumberOfSlots, ref maximumUnchokes, nascentPeers.GetNextPeer ()!);
                else if (candidatePeers.MorePeers)
                    ReallocateSlot (ref NumberOfSlots, ref maximumUnchokes, candidatePeers.GetNextPeer ()!);
                else if (optimisticUnchokeCandidates.MorePeers)
                    ReallocateSlot (ref NumberOfSlots, ref maximumUnchokes, optimisticUnchokeCandidates.GetNextPeer ()!);
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
        /// <param name="peer"></param>The peer to consider for reallocation
        void ReallocateSlot (ref int NumberOfSlots, ref int MaximumUnchokes, PeerId peer)
        {
            if (!peer.AmChoking) {
                //This peer is already unchoked, just decrement the number of slots
                NumberOfSlots--;
            } else if (MaximumUnchokes > 0) {
                //This peer is choked and we've not yet reached the limit of unchokes, unchoke it
                Unchoke (peer);
                MaximumUnchokes--;
                NumberOfSlots--;
            }
        }

        /// <summary>
        /// Checks the send queue of the peer to see if there are any outstanding pieces which they requested
        /// and rejects them as necessary
        /// </summary>
        /// <param name="Peer"></param>
        void RejectPendingRequests (PeerId Peer)
        {
            var rejectedCount = Peer.MessageQueue.RejectRequests (Peer.SupportsFastPeer, Peer.AmAllowedFastPieces.Span);
            Interlocked.Add (ref Peer.isRequestingPiecesCount, rejectedCount);
        }

        void Unchoke (PeerId peer)
        {
            if (!peer.AmChoking)
                throw new InvalidOperationException ("Attempting to unchoke a peer who is already unchoked");

            peer.AmChoking = false;
            Unchokeable.UploadingTo++;
            peer.MessageQueue.EnqueueAt (0, UnchokeMessage.Instance, default);
            peer.LastUnchoked.Restart ();
            peer.FirstReviewPeriod = true;
        }

        #endregion
    }
}
