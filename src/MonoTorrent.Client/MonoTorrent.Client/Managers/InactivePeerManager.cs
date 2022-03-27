using System;
using System.Collections.Generic;

namespace MonoTorrent.Client
{
    class InactivePeerManager
    {

        #region Private Fields
        /// <summary>
        /// Provides access to the list of URIs we've marked as inactive
        /// </summary>
        internal List<Uri> InactivePeerList { get; } = new List<Uri> ();

        ConnectionManager ConnectionManager { get; }
        TorrentManager TorrentManager { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new inactive peer manager for a torrent manager
        /// </summary>
        public InactivePeerManager (TorrentManager torrentManager, ConnectionManager connectionManager)
        {
            TorrentManager = torrentManager;
            ConnectionManager = connectionManager;
        }

        #endregion

        #region Internal methods

        /// <summary>
        /// Executed each tick of the client engine
        /// </summary>
        internal void TimePassed ()
        {

            // If peer inactivation is disabled, do nothing
            if (TorrentManager.Settings.TimeToWaitUntilIdle.TotalSeconds == 0)
                return;

            // If we've not reached the maximum peers for this torrent, there's nothing for us to do
            if (TorrentManager.Settings.MaximumConnections > TorrentManager.OpenConnections)
                return;

            // If there are no available peers, there's nothing for us to do
            if (TorrentManager.Peers.Available < 0)
                return;

            // Look for a peer that has not given us any data and that is eligible for being marked inactive
            // If we find one that is not interesting, disconnect it straightaway; otherwise disconnect the first interesting one we found
            // If there are no eligible peers that have sent us no data, look for peers that have sent data but not for a while
            int indexOfFirstUninterestingCandidate = -1; // -1 indicates we haven't found one yet
            int indexOfFirstInterestingCandidate = -1;
            int leastAttractiveCandidate = -1; // The least attractive peer that has sent us data
            int longestCalculatedInactiveTime = 0; // Seconds we calculated for the least attractive candidate

            // FIXME These three variables aren't used in the calculation - need to fix this.
            int candidateSecondsConnected;
            int candidateSecondsSinceLastBlock;
            int candidateDataBytes;
            for (int i = 0; i < TorrentManager.Peers.ConnectedPeers.Count; i++) {
                PeerId nextPeer = TorrentManager.Peers.ConnectedPeers[i];
                if (nextPeer.Monitor.DataBytesReceived == 0 && nextPeer.WhenConnected.Elapsed > TorrentManager.Settings.TimeToWaitUntilIdle) {
                    // This one is eligible for marking as inactive
                    if (!nextPeer.AmInterested) {
                        // This is an eligible peer and we're not interested in it so stop looking
                        indexOfFirstUninterestingCandidate = i;
                        candidateSecondsConnected = (int) nextPeer.WhenConnected.Elapsed.TotalSeconds;
                        candidateSecondsSinceLastBlock = -1;
                        candidateDataBytes = -1;
                        break;
                    }
                    // This is an eligible peer, but we're interested in it; remember it for potential disconnection if it's the first one we found
                    if (indexOfFirstInterestingCandidate < 0) {
                        indexOfFirstInterestingCandidate = i;
                        candidateSecondsConnected = (int) nextPeer.WhenConnected.Elapsed.TotalSeconds;
                        candidateSecondsSinceLastBlock = -1;
                        candidateDataBytes = -1;
                    }
                } else {
                    // No point looking for inactive peers that have sent us data if we found a candidate that's sent us nothing or if we aren't allowed
                    // to disconnect peers that have sent us data.
                    // If the number of available peers is running low (less than max number of peer connections), don't try to inactivate peers that have given us data
                    if (indexOfFirstInterestingCandidate < 0
                        && TorrentManager.Settings.ConnectionRetentionFactor > 0
                        && nextPeer.Monitor.DataBytesReceived > 0
                        && TorrentManager.Peers.Available >= TorrentManager.Settings.MaximumConnections) {
                        // Calculate an inactive time.
                        // Base time is time since the last message (in seconds)
                        // Give the peer an extra second for every 'ConnectionRetentionFactor' bytes
                        TimeSpan timeSinceLastBlock = nextPeer.LastBlockReceived.Elapsed;
                        int calculatedInactiveTime = Convert.ToInt32 (timeSinceLastBlock.TotalSeconds - Convert.ToInt32 (nextPeer.Monitor.DataBytesReceived / TorrentManager.Settings.ConnectionRetentionFactor));
                        // Register as the least attractive candidate if the calculated time is more than the idle wait time and more than any other candidate
                        if (calculatedInactiveTime > TorrentManager.Settings.TimeToWaitUntilIdle.TotalSeconds && calculatedInactiveTime > longestCalculatedInactiveTime) {
                            longestCalculatedInactiveTime = calculatedInactiveTime;
                            leastAttractiveCandidate = i;
                            candidateSecondsConnected = (int) nextPeer.WhenConnected.Elapsed.TotalSeconds;
                            candidateSecondsSinceLastBlock = (int) nextPeer.LastBlockReceived.Elapsed.TotalSeconds;
                            candidateDataBytes = (int) nextPeer.Monitor.DataBytesReceived;
                        }
                    }
                }
            }

            // We've finished looking for a disconnect candidate
            // Disconnect the uninteresting candidate if found;
            // otherwise disconnect the interesting candidate if found;
            // otherwise disconnect the least attractive candidate
            // otherwise do nothing
            int peerToDisconnect = indexOfFirstUninterestingCandidate;
            if (peerToDisconnect < 0)
                peerToDisconnect = indexOfFirstInterestingCandidate;
            if (peerToDisconnect < 0)
                peerToDisconnect = leastAttractiveCandidate;

            if (peerToDisconnect < 0)
                return;

            // We've found a peer to disconnect
            // Add it to the inactive list for this torrent and disconnect it
            InactivePeerList.Add (TorrentManager.Peers.ConnectedPeers[peerToDisconnect].Uri);
            ConnectionManager!.CleanupSocket (TorrentManager, TorrentManager.Peers.ConnectedPeers[peerToDisconnect]);

        }

        #endregion
    }

}
