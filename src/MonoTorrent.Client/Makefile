OUT=../bin/MonoTorrent.Client.dll

REFERENCES=	\
	-r:../bin/MonoTorrent.Common.dll	\
	-r:System.Web,Mono.Security.dll		\
	-r:../bin/MonoTorrent.BEncoding.dll	\
	-r:../Libs/Upnp.dll

SOURCES=BitField.cs \
	Block.cs \
	BufferedFileRead.cs \
	BufferedFileWrite.cs \
	ClientEngine.cs \
	ConnectionListener.cs \
	Logger.cs \
	PeerList.cs \
	PortMapper.cs \
	RateLimiter.cs \
	MessageHandler.cs \
	TransferType.cs \
	TrackerTier.cs \
	Encryption/EncryptedSocket.cs \
	Encryption/IEncryption.cs \
	Encryption/IEncryptor.cs \
	Encryption/ModuloCalculator.cs \
	Encryption/NoEncryption.cs \
	Encryption/NullEncryption.cs \
	Encryption/PeerAEncryption.cs \
	Encryption/PeerBEncryption.cs \
	Encryption/RC4.cs \
	EventArgs/MessageEventArgs.cs \
	EventArgs/PeerConnectionEventArgs.cs \
	EventArgs/PeersAddedEventArgs.cs \
	EventArgs/PieceEventArgs.cs \
	EventArgs/PieceHashedEventArgs.cs \
	EventArgs/TorrentStateChangedEventArgs.cs \
	EventArgs/TrackerStateChangedEventArgs.cs \
	Exceptions/ConnectionException.cs \
	Exceptions/MessageException.cs \
	Exceptions/ListenerException.cs \
	Exceptions/ProtocolException.cs \
	Exceptions/TorrentLoadException.cs \
	Peers/Peer.cs \
	PeerConnections/PeerConnectionId.cs \
	PeerConnections/PeerConnectionBase.cs \
	PeerConnections/TCPConnection.cs \
	PiecePicking/EndGamePicker.cs \
	PiecePicking/PiecePickerBase.cs \
	PiecePicking/StandardPicker.cs \
	Managers/BufferManager.cs \
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
	PeerMessages/IPeerMessageInternal.cs \
	PeerMessages/KeepAliveMessage.cs \
	PeerMessages/NotInterestedMessage.cs \
	PeerMessages/PeerwireEncoder.cs \
	PeerMessages/PieceMessage.cs \
	PeerMessages/PortMessage.cs \
	PeerMessages/RequestMessage.cs \
	PeerMessages/UnchokeMessage.cs \
	PeerMessages/UnknownMessage.cs \
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


all: $(OUT)

include ../rules.make