using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
	internal class InactivePeerManager
	{

		#region Private Fields
		private TorrentManager owningTorrent; //The torrent to which this manager belongs
		private List<Uri> inactivePeerList = new List<Uri>();

		/// <summary>
		/// Provides access to the list of URIs we've marked as inactive
		/// </summary>
		internal List<Uri> InactivePeerList
		{
			get { return inactivePeerList; }
		}

		/// <summary>
		/// The number of peers we have marked as inactive
		/// </summary>
		internal int InactivePeers
		{
			get { return inactivePeerList.Count; }
		}

		#endregion

		#region Constructor

		/// <summary>
		/// Creates a new inactive peer manager for a torrent manager
		/// </summary>
		/// <param name="TorrentManager">The torrent manager this choke/unchoke manager belongs to</param>
		/// <param name="TimeToWaitBeforeIdle">Number of seconds to protect a peer from being marked as inactive</param>
        public InactivePeerManager(TorrentManager TorrentManager)
        {
            owningTorrent = TorrentManager;
        }

		#endregion

		#region Internal methods

		/// <summary>
		/// Executed each tick of the client engine
		/// </summary>
		internal void TimePassed()
		{

			// If peer inactivation is disabled, do nothing
			if (owningTorrent.Settings.TimeToWaitUntilIdle.TotalSeconds == 0)
				return;

			// If we've not reached the maximum peers for this torrent, there's nothing for us to do
			if (owningTorrent.Settings.MaxConnections > owningTorrent.OpenConnections)
				return;

			// If there are no available peers, there's nothing for us to do
			if (owningTorrent.Peers.Available < 0)
				return;

			// Look for a peer that has not given us any data and that is eligible for being marked inactive
			// If we find one that is not interesting, disconnect it straightaway; otherwise disconnect the first interesting one we found
			// If there are no eligible peers that have sent us no data, look for peers that have sent data but not for a while
			int indexOfFirstUninterestingCandidate = -1; // -1 indicates we haven't found one yet
			int indexOfFirstInterestingCandidate = -1;
			int leastAttractiveCandidate = -1; // The least attractive peer that has sent us data
			int longestCalculatedInactiveTime = 0; // Seconds we calculated for the least attractive candidate
			
            // FIXME These three variables aren't used in the calculation - need to fix this.
			int candidateSecondsConnected = 0;
			int candidateSecondsSinceLastBlock = -1;
			int candidateDataBytes = -1;
			for (int i = 0; i < owningTorrent.Peers.ConnectedPeers.Count; i++)
			{
				PeerId nextPeer = owningTorrent.Peers.ConnectedPeers[i];
				if (nextPeer.Monitor.DataBytesDownloaded == 0 && nextPeer.WhenConnected.Add(owningTorrent.Settings.TimeToWaitUntilIdle) < DateTime.Now)
				{
					// This one is eligible for marking as inactive
					if (!nextPeer.AmInterested)
					{
						// This is an eligible peer and we're not interested in it so stop looking
						indexOfFirstUninterestingCandidate = i;
						candidateSecondsConnected = (int)(DateTime.Now.Subtract(nextPeer.WhenConnected)).TotalSeconds;
						candidateSecondsSinceLastBlock = -1;
						candidateDataBytes = -1;
						break;
					}
					// This is an eligible peer, but we're interested in it; remember it for potential disconnection if it's the first one we found
					if (indexOfFirstInterestingCandidate < 0)
					{
						indexOfFirstInterestingCandidate = i;
						candidateSecondsConnected = (int)(DateTime.Now.Subtract(nextPeer.WhenConnected)).TotalSeconds;
						candidateSecondsSinceLastBlock = -1;
						candidateDataBytes = -1;
					}
				}
				else
				{
					// No point looking for inactive peers that have sent us data if we found a candidate that's sent us nothing or if we aren't allowed
					// to disconnect peers that have sent us data.
					// If the number of available peers is running low (less than max number of peer connections), don't try to inactivate peers that have given us data
					if (indexOfFirstInterestingCandidate < 0 
						&& owningTorrent.Settings.ConnectionRetentionFactor > 0
						&& nextPeer.Monitor.DataBytesDownloaded > 0 
						&& owningTorrent.Peers.Available >= owningTorrent.Settings.MaxConnections )
					{
						// Calculate an inactive time.
						// Base time is time since the last message (in seconds)
						// Give the peer an extra second for every 'ConnectionRetentionFactor' bytes
						TimeSpan timeSinceLastBlock = DateTime.Now.Subtract(nextPeer.LastBlockReceived);
						int calculatedInactiveTime = Convert.ToInt32(timeSinceLastBlock.TotalSeconds - Convert.ToInt32(nextPeer.Monitor.DataBytesDownloaded / owningTorrent.Settings.ConnectionRetentionFactor));
						// Register as the least attractive candidate if the calculated time is more than the idle wait time and more than any other candidate
						if (calculatedInactiveTime > owningTorrent.Settings.TimeToWaitUntilIdle.TotalSeconds  && calculatedInactiveTime > longestCalculatedInactiveTime)
						{
							longestCalculatedInactiveTime = calculatedInactiveTime;
							leastAttractiveCandidate = i;
							candidateSecondsConnected = (int)(DateTime.Now.Subtract(nextPeer.WhenConnected)).TotalSeconds;
							candidateSecondsSinceLastBlock = (int)(DateTime.Now.Subtract(nextPeer.LastBlockReceived)).TotalSeconds;
							candidateDataBytes = (int)nextPeer.Monitor.DataBytesDownloaded;
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
			inactivePeerList.Add(owningTorrent.Peers.ConnectedPeers[peerToDisconnect].Uri);
			owningTorrent.Peers.ConnectedPeers[peerToDisconnect].ConnectionManager.CleanupSocket(owningTorrent.Peers.ConnectedPeers[peerToDisconnect], "Marked as inactive");

		}

		#endregion
	}

}
