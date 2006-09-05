CSC=gmcs 
OUT=../Client.dll
REFERENCES=../Common.dll,System.Web
SOURCES=BitField.cs \
Block.cs \
ClientEngine.cs \
ClientPeer.cs \
ConnectionListener.cs \
Encryption/RC4FullConnection.cs \
Encryption/RC4HeaderConnection.cs \
Encryption/StandardConnection.cs \
EventArgs/MessageEventArgs.cs \
EventArgs/PeerConnectionEventArgs.cs \
EventArgs/PeersAddedEventArgs.cs \
EventArgs/PieceEventArgs.cs \
EventArgs/PieceHashedEventArgs.cs \
EventArgs/TorrentStateChangedEventArgs.cs \
EventArgs/TrackerEventArgs.cs \
IClientEngine.cs \
IEngineSettings.cs \
IPeerConnectionID.cs \
IPieceManager.cs \
ITorrentManager.cs \
Managers/ConnectionManager.cs \
Managers/ConnectionMonitor.cs \
Managers/FileManager.cs \
Managers/PieceManager.cs \
Managers/TorrentManager.cs \
Managers/TrackerManager.cs \
PeerBase.cs \
PeerConnectionBase.cs \
PeerConnection.cs \
PeerConnectionId.cs \
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
Peers.cs \
Piece.cs \
Settings/EngineSettings.cs \
Settings/TorrentSettings.cs \
TrackerConnectionId.cs \
Tracker.cs
#Tests/BitFieldTest.cs \
#Tests/FileManager.cs \
#Tests/PeerMessagesTest.cs \


all: client

client: $(SOURCES)
	$(CSC) -r:$(REFERENCES) -target:library -out:$(OUT) $(SOURCES)
