using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	class PeerList
	{
		#region Private Fields

		private List<PeerIdInternal> peers; //Peers held
		private PeerListType listType; //The type of list this represents
		private int scanIndex = 0; //Position in the list when scanning peers

		#endregion Private Fields

		#region Constructors

		/// <summary>
		/// Creates a new, empty peer list
		/// </summary>
		/// <param name="ListType">The type of list</param>
		public PeerList(PeerListType ListType)
		{
			peers = new List<PeerIdInternal>();
			listType = ListType;
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Count of peers in the list
		/// </summary>
		public int Count
		{
			get { return peers.Count; }
		}

		/// <summary>
		/// True if there are more peers left to scan
		/// </summary>
		public bool MorePeers
		{
			get
			{
				if (scanIndex < peers.Count)
					return true;
				else
					return false;
			}
		}

		/// <summary>
		/// Number of unchoked peers in the list
		/// </summary>
		public int UnchokedPeers
		{
			get
			{
				int peersCount = 0;
				foreach (PeerIdInternal peer in peers)
					if (!peer.Connection.AmChoking)
						peersCount++;
				return peersCount;
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Adds a peer to the peer list
		/// </summary>
        public void Add(PeerIdInternal peer)
		{
			peers.Add(peer);
		}

		/// <summary>
		/// Clears the peer list
		/// </summary>
		public void Clear()
		{
			peers.Clear();
			scanIndex = 0;
		}

		/// <summary>
		/// Gets the next peer to be scanned, returns null if there are no more
		/// </summary>
        public PeerIdInternal GetNextPeer()
		{
			if (scanIndex < peers.Count)
			{
				scanIndex++;
				return peers[scanIndex - 1];
			}
			else
				return null;
		}

		/// <summary>
		/// Gets the first choked peer in the list, or null if none found
		/// </summary>
        public PeerIdInternal GetFirstInterestedChokedPeer()
		{
			//Look for a choked peer
            foreach (PeerIdInternal peer in peers)
				if (peer.Connection != null)
					if (peer.Connection.IsInterested && peer.Connection.AmChoking)
						return peer;
			//None found, return null
			return null;
		}

		/// <summary>
		/// Looks for a choked peer that we can optimistically unchoke, or null if none found
		/// </summary>
        public PeerIdInternal GetOUPeer()
		{
			//Look for an untried peer that we haven't unchoked, or else return the choked peer with the longest unchoke interval
            PeerIdInternal longestIntervalPeer = null;
			double longestIntervalPeerTime = 0;
            foreach (PeerIdInternal peer in peers)
				if (peer.Connection != null)
					if (peer.Connection.AmChoking)
					{
                        if (!peer.Connection.LastUnchoked.HasValue)
							//This is an untried peer that we haven't unchoked, return it
							return peer;
						else
						{
							//This is an unchoked peer that we have unchoked in the past
							//If this is the first one we've found, remember it
							if (longestIntervalPeer == null)
								longestIntervalPeer = peer;
							else
							{
								//Compare dates to determine whether the new one has a longer interval (but halve the interval
								//  if the peer has never sent us any data)
                                double newInterval = SecondsBetween(peer.Connection.LastUnchoked.Value, DateTime.Now);
								if (peer.Connection.Monitor.DataBytesDownloaded == 0)
									newInterval = newInterval / 2;
								if (newInterval > longestIntervalPeerTime)
								{
									//The new peer has a longer interval than the current one, replace it
									longestIntervalPeer = peer;
									longestIntervalPeerTime = newInterval;
								}
							}
						}
					}
			//Return the peer with the longest interval since it was unchoked, or null if none found
			return longestIntervalPeer;
		}

		/// <summary>
		/// Tests to see if the list includes a given peer
		/// </summary>
		/// <param name="Peer">The peer we are testing for</param>
        public bool Includes(PeerIdInternal peer)
		{
			//Return false if the supplied peer is null
			if (peer == null)
				return false;
			return peers.Contains(peer);
		}

		/// <summary>
		/// Sorts the peer list based on list type and whether we are seeding, or not
		/// </summary>
		/// <param name="IsSeeding">Indicates whether the torrent using the peers is seeding, or not</param>
		public void Sort(bool IsSeeding)
		{
			switch (listType)
			{
				case (PeerListType.NascentPeers):
					peers.Sort(CompareNascentPeers);
					break;

				case (PeerListType.CandidatePeers):
					if (IsSeeding)
						peers.Sort(CompareCandidatePeersWhileSeeding);
					else
						peers.Sort(CompareCandidatePeersWhileDownloading);
					break;

				case (PeerListType.OptimisticUnchokeCandidatePeers):
					if (IsSeeding)
						peers.Sort(CompareOptimisticUnchokeCandidatesWhileSeeding);
					else
						peers.Sort(CompareOptimisticUnchokeCandidatesWhileDownloading);
					break;
			}
		}

		/// <summary>
		/// Start a scan of the peer list; puts current scan position to the first peer
		/// </summary>
		public void StartScan()
		{
			scanIndex = 0;
		}

		#endregion

		#region Private Methods

        private static int CompareCandidatePeersWhileDownloading(PeerIdInternal P1, PeerIdInternal P2)
		{
			//Comparer for candidate peers for use when the torrent is downloading
			//First sort Am interested before !AmInterested
			if (P1.Connection.AmInterested && !P2.Connection.AmInterested)
				return -1;
			else if (!P1.Connection.AmInterested && P2.Connection.AmInterested)
				return 1;

			//Both have the same AmInterested status, sort by download rate highest first
            return P2.Connection.LastReviewDownloadRate.CompareTo(P1.Connection.LastReviewDownloadRate);
		}

        private static int CompareCandidatePeersWhileSeeding(PeerIdInternal P1, PeerIdInternal P2)
		{
			//Comparer for candidate peers for use when the torrent is seeding
			//Sort by upload rate, largest first
            return P2.Connection.LastReviewUploadRate.CompareTo(P1.Connection.LastReviewUploadRate);
		}

        private static int CompareNascentPeers(PeerIdInternal P1, PeerIdInternal P2)
		{
			//Comparer for nascent peers
			//Sort most recent first
            if (P1.Connection.LastUnchoked > P2.Connection.LastUnchoked)
				return -1;
            else if (P1.Connection.LastUnchoked < P2.Connection.LastUnchoked)
				return 1;
			else
				return 0;
		}

        private static int CompareOptimisticUnchokeCandidatesWhileDownloading(PeerIdInternal P1, PeerIdInternal P2)
		{
			//Comparer for optimistic unchoke candidates

			//Start by sorting peers that have given us most data before to the top
			if (P1.Connection.Monitor.DataBytesDownloaded > P2.Connection.Monitor.DataBytesDownloaded)
				return -1;
			else if (P1.Connection.Monitor.DataBytesDownloaded < P2.Connection.Monitor.DataBytesDownloaded)
				return 1;

			//Amount of data sent is equal (and probably 0), sort untried before tried
            if (!P1.Connection.LastUnchoked.HasValue && P2.Connection.LastUnchoked.HasValue)
				return -1;
            else if (P1.Connection.LastUnchoked.HasValue && !P2.Connection.LastUnchoked.HasValue)
				return 1;
            else if (!P1.Connection.LastUnchoked.HasValue && !P2.Connection.LastUnchoked.HasValue)
				//Both untried, nothing to choose between them
				return 0;

			//Both peers have been unchoked
			//Sort into descending order (most recent first)
            if (P1.Connection.LastUnchoked > P2.Connection.LastUnchoked)
				return -1;
            else if (P1.Connection.LastUnchoked < P2.Connection.LastUnchoked)
				return 1;
			else
				return 0;
		}

        private static int CompareOptimisticUnchokeCandidatesWhileSeeding(PeerIdInternal P1, PeerIdInternal P2)
		{
			//Comparer for optimistic unchoke candidates

			//Start by sorting peers that we have sent most data to before to the top
			if (P1.Connection.Monitor.DataBytesUploaded > P2.Connection.Monitor.DataBytesUploaded)
				return -1;
			else if (P1.Connection.Monitor.DataBytesUploaded < P2.Connection.Monitor.DataBytesUploaded)
				return 1;

			//Amount of data sent is equal (and probably 0), sort untried before tried
            if (!P1.Connection.LastUnchoked.HasValue && P2.Connection.LastUnchoked.HasValue)
				return -1;
            else if (P1.Connection.LastUnchoked.HasValue && !P2.Connection.LastUnchoked.HasValue)
				return 1;
            else if (!P1.Connection.LastUnchoked.HasValue && P2.Connection.LastUnchoked.HasValue)
				//Both untried, nothing to choose between them
				return 0;

			//Both peers have been unchoked
			//Sort into descending order (most recent first)
            if (P1.Connection.LastUnchoked > P2.Connection.LastUnchoked)
				return -1;
            else if (P1.Connection.LastUnchoked < P2.Connection.LastUnchoked)
				return 1;
			else
				return 0;
		}

		private static double SecondsBetween(DateTime FirstTime, DateTime SecondTime)
		{
			//Calculate the number of seconds and fractions of a second that have elapsed between the first time and the second
			return SecondTime.Subtract(FirstTime).TotalMilliseconds / 1000;
		}

		#endregion
	}
}
