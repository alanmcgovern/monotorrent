CSC=gmcs 
OUT=../Client.dll
REFERENCES=../Common.dll,System.Web,../Mono.Security.dll
SOURCES=BitField.cs \
Block.cs \
ClientEngine.cs \
ConnectionListener.cs \
IClientEngine.cs \
IEngineSettings.cs \
IPieceManager.cs \
ITorrentManager.cs \
Encryption/IEncryptor.cs \
Encryption/NoEncryption.cs \
Encryption/RC4FullEncryption.cs \
Encryption/RC4HeaderEncryption.cs \
EventArgs/MessageEventArgs.cs \
EventArgs/PeerConnectionEventArgs.cs \
EventArgs/PeersAddedEventArgs.cs \
EventArgs/PieceEventArgs.cs \
EventArgs/PieceHashedEventArgs.cs \
EventArgs/TorrentStateChangedEventArgs.cs \
EventArgs/TrackerEventArgs.cs \
Peers/Peer.cs \
Peers/Peers.cs \
PeerConnections/PeerConnectionId.cs \
PeerConnections/IPeerConnectionID.cs \
PeerConnections/PeerConnectionBase.cs \
PeerConnections/TCPConnection.cs \
Managers/ConnectionManager.cs \
Managers/ConnectionMonitor.cs \
Managers/FileManager.cs \
Managers/PieceManager.cs \
Managers/TorrentManager.cs \
Managers/TrackerManager.cs \
PeerMessages/BitfieldMessage.cs \
PeerMessages/CancelMessage.cs \
PeerMessages/ChokeMessage.cs \
PeerMessages/HandshakeMessage.cs \
PeerMessages/HaveMessage.cs \
PeerMessages/InterestedMessage.cs \
PeerMessages/IPeerMessage.cs \
PeerMessages/KeepAliveMessage.cs \
PeerMessages/NotInterestedMessage.cs \
PeerMessages/PeerwireEncoder.cs \
PeerMessages/PieceMessage.cs \
PeerMessages/PortMessage.cs \
PeerMessages/RequestMessage.cs \
PeerMessages/UnchokeMessage.cs \
PeerMessages/ExtendedMessages/ExtendedListMessage.cs \
PeerMessages/FastPeerExtensions/AllowedFastMessage.cs \
PeerMessages/FastPeerExtensions/HaveAllMessage.cs \
PeerMessages/FastPeerExtensions/HaveNoneMessage.cs \
PeerMessages/FastPeerExtensions/RejectRequestMessage.cs \
PeerMessages/FastPeerExtensions/SuggestPieceMessage.cs \
Settings/EngineSettings.cs \
Settings/TorrentSettings.cs \
Piece.cs \
TrackerConnectionId.cs \
AllowedFastAlgorithm.cs \
Tracker.cs

#Tests/BitFieldTest.cs \
#Tests/FileManager.cs \
#Tests/PeerMessagesTest.cs \
#Tests/AllowedFastAlgorithmTest.cs \


all: client

client: $(SOURCES)
	$(CSC) -r:$(REFERENCES) -target:library -out:$(OUT) $(SOURCES)
