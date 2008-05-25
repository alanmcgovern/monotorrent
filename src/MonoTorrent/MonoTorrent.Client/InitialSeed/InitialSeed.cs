using System;
using System.Collections.Generic;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	internal class InitialSeed
	{

		public InitialSeed (TorrentManager torrentManager)
		{
			this.torrentManager = torrentManager;
			pieceToPeer = new Dictionary<int, PeerIdInternal> (torrentManager.Bitfield.Length);
			bitfield = torrentManager.Bitfield.Clone();//all true
		}

		private BitField bitfield;
		private TorrentManager torrentManager;
		private IDictionary<int, PeerIdInternal> pieceToPeer;

		public int GetNextPieceForPeer (PeerIdInternal id)
		{
			int piece = bitfield.FirstTrue();
			if (piece == -1) { // unactive superseed mode
				torrentManager.Settings.InitialSeedingEnabled = false;
				return -1;
			}
			// if piece +1 = bitfield.Length FirstTrue will return -1 so OK
			while (piece != -1 && pieceToPeer.ContainsKey(piece)) {
				piece = bitfield.FirstTrue(piece + 1, bitfield.Length);
			}
			
			//if all attributed to peer but not received
			if (piece == -1) {
				piece = bitfield.FirstTrue();//we resend attributed but not received
			}
			pieceToPeer [piece] = id;
		        return piece;
		}
		
		public void OnNotInitialPeerHaveMessage(int piece)
		{
			bitfield [piece] = false;//the piece is uploaded to another peer
		}

		public PeerIdInternal GetOriginPeer (int piece)
		{
			return ((pieceToPeer.ContainsKey (piece)) ? (pieceToPeer [piece]) : null);
		}
	}
}


