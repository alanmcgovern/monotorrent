using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	class PeerList
	{
		#region Private Fields

		private List<PeerId> peers; //Peers held
		private PeerListType listType; //The type of list this represents
		private int scanIndex = 0; //Position in the list when scanning peers

		#endregion Private Fields

		#region Constructors

		public PeerList(PeerListType ListType)
		{
			peers = new List<PeerId>();
			listType = ListType;
		}

		#endregion

		#region Public Properties

		public int Count
		{
			get { return peers.Count; }
		}

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

		public int UnchokedPeers
		{
			get
			{
				int peersCount = 0;
				foreach (PeerId peer in peers)
					if (!peer.AmChoking)
						peersCount++;
				return peersCount;
			}
		}

		#endregion

		#region Public Methods

        public void Add(PeerId peer)
		{
			peers.Add(peer);
		}

		public void Clear()
		{
			peers.Clear();
			scanIndex = 0;
		}

        public PeerId GetNextPeer()
		{
			if (scanIndex < peers.Count)
			{
				scanIndex++;
				return peers[scanIndex - 1];
			}
			else
				return null;
		}

        public PeerId GetFirstInterestedChokedPeer()
		{
			//Look for a choked peer
            foreach (PeerId peer in peers)
				if (peer.Connection != null)
					if (peer.IsInterested && peer.AmChoking)
						return peer;
			//None found, return null
			return null;
		}

        public PeerId GetOUPeer()
		{
			//Look for an untried peer that we haven't unchoked, or else return the choked peer with the longest unchoke interval
            PeerId longestIntervalPeer = null;
			double longestIntervalPeerTime = 0;
            foreach (PeerId peer in peers)
				if (peer.Connection != null)
					if (peer.AmChoking)
					{
                        if (!peer.LastUnchoked.HasValue)
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
                                double newInterval = SecondsBetween(peer.LastUnchoked.Value, DateTime.Now);
								if (peer.Monitor.DataBytesDownloaded == 0)
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

        public bool Includes(PeerId peer)
		{
			//Return false if the supplied peer is null
			if (peer == null)
				return false;
			return peers.Contains(peer);
		}

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

		public void StartScan()
		{
			scanIndex = 0;
		}

		#endregion

		#region Private Methods

        private static int CompareCandidatePeersWhileDownloading(PeerId P1, PeerId P2)
		{
			//Comparer for candidate peers for use when the torrent is downloading
			//First sort Am interested before !AmInterested
			if (P1.AmInterested && !P2.AmInterested)
				return -1;
			else if (!P1.AmInterested && P2.AmInterested)
				return 1;

			//Both have the same AmInterested status, sort by download rate highest first
            return P2.LastReviewDownloadRate.CompareTo(P1.LastReviewDownloadRate);
		}

        private static int CompareCandidatePeersWhileSeeding(PeerId P1, PeerId P2)
		{
			//Comparer for candidate peers for use when the torrent is seeding
			//Sort by upload rate, largest first
            return P2.LastReviewUploadRate.CompareTo(P1.LastReviewUploadRate);
		}

        private static int CompareNascentPeers(PeerId P1, PeerId P2)
		{
			//Comparer for nascent peers
			//Sort most recent first
            if (P1.LastUnchoked > P2.LastUnchoked)
				return -1;
            else if (P1.LastUnchoked < P2.LastUnchoked)
				return 1;
			else
				return 0;
		}

        private static int CompareOptimisticUnchokeCandidatesWhileDownloading(PeerId P1, PeerId P2)
		{
			//Comparer for optimistic unchoke candidates

			//Start by sorting peers that have given us most data before to the top
			if (P1.Monitor.DataBytesDownloaded > P2.Monitor.DataBytesDownloaded)
				return -1;
			else if (P1.Monitor.DataBytesDownloaded < P2.Monitor.DataBytesDownloaded)
				return 1;

			//Amount of data sent is equal (and probably 0), sort untried before tried
            if (!P1.LastUnchoked.HasValue && P2.LastUnchoked.HasValue)
				return -1;
            else if (P1.LastUnchoked.HasValue && !P2.LastUnchoked.HasValue)
				return 1;
            else if (!P1.LastUnchoked.HasValue && !P2.LastUnchoked.HasValue)
				//Both untried, nothing to choose between them
				return 0;

			//Both peers have been unchoked
			//Sort into descending order (most recent first)
            if (P1.LastUnchoked > P2.LastUnchoked)
				return -1;
            else if (P1.LastUnchoked < P2.LastUnchoked)
				return 1;
			else
				return 0;
		}

        private static int CompareOptimisticUnchokeCandidatesWhileSeeding(PeerId P1, PeerId P2)
		{
			//Comparer for optimistic unchoke candidates

			//Start by sorting peers that we have sent most data to before to the top
			if (P1.Monitor.DataBytesUploaded > P2.Monitor.DataBytesUploaded)
				return -1;
			else if (P1.Monitor.DataBytesUploaded < P2.Monitor.DataBytesUploaded)
				return 1;

			//Amount of data sent is equal (and probably 0), sort untried before tried
            if (!P1.LastUnchoked.HasValue && P2.LastUnchoked.HasValue)
				return -1;
            else if (P1.LastUnchoked.HasValue && !P2.LastUnchoked.HasValue)
				return 1;
            else if (!P1.LastUnchoked.HasValue && P2.LastUnchoked.HasValue)
				//Both untried, nothing to choose between them
				return 0;

			//Both peers have been unchoked
			//Sort into descending order (most recent first)
            if (P1.LastUnchoked > P2.LastUnchoked)
				return -1;
            else if (P1.LastUnchoked < P2.LastUnchoked)
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
