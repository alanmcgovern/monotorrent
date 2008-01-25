OUT=../bin/MonoTorrent.dll

REFERENCES=	\
	-r:System.Web,Mono.Security.dll

EXTRA_DIST = AssemblyInfo.cs.in

SOURCES=	\
	Collections/MonoTorrentCollectionBase.cs \
	\
	\
	Common/BitField.cs \
	Common/Enums.cs \
	Common/Hashes.cs \
	Common/Peer.cs \
	Common/PeerID.cs \
	Common/ToolBox.cs \
	Common/TorrentCreator.cs \
	Common/Torrent.cs \
	Common/TorrentFile.cs \
	Common/TorrentException.cs \
	Common/TorrentCreatorAsyncResult.cs \
	Common/VersionInfo.cs \
	Common/EventArgs/TorrentCreatorEventArgs.cs \
	Common/TorrentWatchers/ITorrentWatcher.cs \
	Common/TorrentWatchers/TorrentFolderWatcher.cs \
	Common/TorrentWatchers/TorrentWatcherEventArgs.cs \
	Common/TorrentWatchers/TorrentWatchers.cs \
	\
	\
	Client/AllowedFastAlgorithm.cs \
	Client/Block.cs \
	Client/BufferedFileRead.cs \
	Client/BufferedFileWrite.cs \
	Client/ClientEngine.cs \
	Client/FileStreamBuffer.cs \
	Client/Logger.cs \
	Client/MessageHandler.cs \
	Client/Piece.cs \
	Client/RateLimiter.cs \
	Client/ReaderLock.cs \
	Client/TorrentFileStream.cs \
	Client/TransferType.cs \
	Client/WriterLock.cs \
	\
	\
	Client/ConnectionListeners/IConnectionListener.cs \
	Client/ConnectionListeners/SocketListener.cs \
	\
	\
	Client/Encryption/EncryptedSocket.cs \
	Client/Encryption/IEncryption.cs \
	Client/Encryption/IEncryptor.cs \
	Client/Encryption/ModuloCalculator.cs \
	Client/Encryption/NoEncryption.cs \
	Client/Encryption/NullEncryption.cs \
	Client/Encryption/PeerAEncryption.cs \
	Client/Encryption/PeerBEncryption.cs \
	Client/Encryption/RC4.cs \
	\
	\
	Client/EventArgs/AnnounceResponseEventArgs.cs \
	Client/EventArgs/BlockEventArgs.cs \
	Client/EventArgs/MessageEventArgs.cs \
	Client/EventArgs/NewConnectionEventArgs.cs \
	Client/EventArgs/PeerConnectionEventArgs.cs \
	Client/EventArgs/PeersAddedEventArgs.cs \
	Client/EventArgs/PieceEventArgs.cs \
	Client/EventArgs/PieceHashedEventArgs.cs \
	Client/EventArgs/ScrapeResponseEventArgs.cs \
	Client/EventArgs/StateUpdateEventArgs.cs \
	Client/EventArgs/TorrentEventArgs.cs \
	Client/EventArgs/TorrentStateChangedEventArgs.cs \
	Client/EventArgs/TrackerRequestEventArgs.cs \
	Client/EventArgs/TrackerStateChangedEventArgs.cs \
	\
	\
	Client/Exceptions/ConnectionException.cs \
	Client/Exceptions/MessageException.cs \
	Client/Exceptions/ListenerException.cs \
	Client/Exceptions/ProtocolException.cs \
	Client/Exceptions/TorrentLoadException.cs \
	\
	\
	Client/Managers/BufferManager.cs \
	Client/Managers/ChokeUnchokeManager.cs \
	Client/Managers/ConnectionManager.cs \
	Client/Managers/ConnectionMonitor.cs \
	Client/Managers/DiskManager.cs \
	Client/Managers/FileManager.cs \
	Client/Managers/ListenManager.cs \
	Client/Managers/PeerManager.cs \
	Client/Managers/PieceManager.cs \
	Client/Managers/TorrentManager.cs \
	Client/Managers/TrackerManager.cs \
	\
	\
	Client/PeerConnections/ConnectionFactory.cs  \
	Client/PeerConnections/HTTPConnection.cs  \
	Client/PeerConnections/IConnection.cs  \
	Client/PeerConnections/PeerConnectionBase.cs \
	Client/PeerConnections/PeerConnectionId.cs \
	Client/PeerConnections/PeerId.cs \
	Client/PeerConnections/TCPConnection.cs \
	\
	\
	Client/PeerMessages/BitfieldMessage.cs \
	Client/PeerMessages/CancelMessage.cs \
	Client/PeerMessages/ChokeMessage.cs \
	Client/PeerMessages/HandshakeMessage.cs \
	Client/PeerMessages/HaveMessage.cs \
	Client/PeerMessages/InterestedMessage.cs \
	Client/PeerMessages/IPeerMessage.cs \
	Client/PeerMessages/IPeerMessageInternal.cs \
	Client/PeerMessages/KeepAliveMessage.cs \
	Client/PeerMessages/NotInterestedMessage.cs \
	Client/PeerMessages/PeerwireEncoder.cs \
	Client/PeerMessages/PieceMessage.cs \
	Client/PeerMessages/PortMessage.cs \
	Client/PeerMessages/RequestMessage.cs \
	Client/PeerMessages/UnchokeMessage.cs \
	Client/PeerMessages/UnknownMessage.cs \
	Client/PeerMessages/ExtendedMessages/ExtendedListMessage.cs \
	Client/PeerMessages/FastPeerExtensions/AllowedFastMessage.cs \
	Client/PeerMessages/FastPeerExtensions/HaveAllMessage.cs \
	Client/PeerMessages/FastPeerExtensions/HaveNoneMessage.cs \
	Client/PeerMessages/FastPeerExtensions/RejectRequestMessage.cs \
	Client/PeerMessages/FastPeerExtensions/SuggestPieceMessage.cs \
	\
	\
	Client/Peers/Peer.cs \
	Client/Peers/PeerList.cs \
	\
	\
	Client/PiecePicking/EndGamePicker.cs \
	Client/PiecePicking/PiecePickerBase.cs \
	Client/PiecePicking/StandardPicker.cs \
	\
	\
	Client/Settings/EngineSettings.cs \
	Client/Settings/TorrentSettings.cs \
	\
	\
	Client/Tracker/AnnounceParameters.cs \
	Client/Tracker/HTTPTracker.cs \
	Client/Tracker/TrackerConnectionId.cs \
	Client/Tracker/Tracker.cs \
	Client/Tracker/TrackerFactory.cs \
	Client/Tracker/TrackerTier.cs \
	\
	\
	BEncoding/BEncodedDictionary.cs \
	BEncoding/BEncodedList.cs \
	BEncoding/BEncodedNumber.cs \
	BEncoding/BEncodedString.cs \
	BEncoding/BEncodingException.cs \
	BEncoding/IBEncodedValue.cs	\
	Tracker/ByteComparer.cs \
	Tracker/IIntervalAlgorithm.cs \
	Tracker/ITrackable.cs \
	Tracker/Peer.cs \
	Tracker/RequestParameters.cs \
	Tracker/SimpleTorrentManager.cs \
	Tracker/StaticIntervalAlgorithm.cs \
	Tracker/Tracker.cs \
	Tracker/Exceptions/TrackerException.cs \
	Tracker/Frontend/AnnounceParameters.cs \
	Tracker/Frontend/ScrapeParameters.cs \
	Tracker/Listeners/HttpListener.cs \
	Tracker/Listeners/ListenerBase.cs \
	AssemblyInfo.cs

all: $(OUT)

AssemblyInfo.cs: AssemblyInfo.cs.in ../config.make
	sed "s/@VERSION@/$(ASSEMBLY_VERSION)/" < AssemblyInfo.cs.in > AssemblyInfo.cs

include ../rules.make
