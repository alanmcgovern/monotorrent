using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
	class InactivePeerManager
	{

		#region Private Fields

		private int timeToWaitBeforeIdle = 600; // If we've not got anything from a peer for this number of seconds, consider them as candidates for disconnection; 0 = disable inactivity checking
		private TorrentManager owningTorrent; //The torrent to which this manager belongs
		private List<Uri> inactiveUris = new List<Uri>();

		/// <summary>
		/// Provides access to the list of URIs we've marked as inactive
		/// </summary>
		internal List<Uri> InactiveUris
		{
			get { return inactiveUris; }
		}

		/// <summary>
		/// The number of peers we have marked as inactive
		/// </summary>
		internal int InactivatedPeers
		{
			get { return inactiveUris.Count; }
		}

		#endregion

		#region Constructor

		/// <summary>
		/// Creates a new inactive peer manager for a torrent manager
		/// </summary>
		/// <param name="TorrentManager">The torrent manager this choke/unchoke manager belongs to</param>
		/// <param name="TimeToWaitBeforeIdle">Number of seconds to protect a peer from being marked as inactive</param>
        public InactivePeerManager(TorrentManager TorrentManager, int TimeToWaitBeforeIdle)
        {
            owningTorrent = TorrentManager;
            timeToWaitBeforeIdle = TimeToWaitBeforeIdle;
        }

		#endregion

		#region Public methods

		/// <summary>
		/// Executed each tick of the client engine
		/// </summary>
		public void TimePassed()
		{

			// If peer inactivation is disabled, do nothing
			if (timeToWaitBeforeIdle == 0)
				return;

			// If we've not reached the maximum peers for this torrent, there's nothing for us to do
			if (owningTorrent.Settings.MaxConnections > owningTorrent.OpenConnections)
				return;

			// If there are no available peers, there's nothing for us to do
			if (owningTorrent.Peers.Available < 0)
				return;

			// Look for a peer that has not given us any data and that is eligible for being marked inactive
			// If we find one that is not interesting, disconnect it straightaway; otherwise disconnect the first interesting one we found
			// This is a simplistic approach to start with
			int indexOfFirstInterestingCandidate = -1; // -1 indicates we haven't found one yet
			int indexOfFirstUninterestingCandidate = -1;
			for (int i = 0; i < owningTorrent.Peers.ConnectedPeers.Count; i++)
			{
				PeerId nextPeer = owningTorrent.Peers.ConnectedPeers[i];
				if (nextPeer.BytesReceived == 0 && nextPeer.WhenConnected.AddSeconds(timeToWaitBeforeIdle) < DateTime.Now)
				{
					// This one is eligible for marking as inactive
					if (!nextPeer.AmInterested)
					{
						// This is an eligible peer and we're not interested in it so stop looking
						indexOfFirstUninterestingCandidate = i;
						break;
					}
					// This is an eligible peer, but we're interested in it; remember it for potential disconnection if it's the first one we found
					if (indexOfFirstInterestingCandidate < 0)
						indexOfFirstInterestingCandidate = i;
				}
			}

			// We've finished looking for a disconnect candidate
			// Disconnect the uninteresting candidate if found;
			// otherwise disconnect the interesting candidate if found;
			// otherwise do nothing
			int peerToDisconnect = indexOfFirstUninterestingCandidate;
			if (peerToDisconnect < 0)
				peerToDisconnect = indexOfFirstInterestingCandidate;

			if (peerToDisconnect < 0)
				return;

			// We've found a peer to disconnect
			// Add it to the inactive list for this torrent and disconnect it
			inactiveUris.Add(owningTorrent.Peers.ConnectedPeers[peerToDisconnect].Uri);
			owningTorrent.Peers.ConnectedPeers[peerToDisconnect].ConnectionManager.CleanupSocket(owningTorrent.Peers.ConnectedPeers[peerToDisconnect], "Marked as inactive");

		}

		#endregion
	}
}
