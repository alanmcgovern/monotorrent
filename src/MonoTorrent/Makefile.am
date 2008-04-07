
EXTRA_DIST =  

# Warning: This is an automatically generated file, do not edit!

if ENABLE_DEBUG_ANY_CPU
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -unsafe -warn:4 -optimize- -debug "-define:DEBUG;TRACE"

ASSEMBLY = bin/Debug/MonoTorrent.dll
ASSEMBLY_MDB = $(ASSEMBLY).mdb
COMPILE_TARGET = library
PROJECT_REFERENCES = 
BUILD_DIR = bin/Debug/


endif

if ENABLE_RELEASE_ANY_CPU
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -unsafe -warn:4 -optimize+ "-define:TRACE"

ASSEMBLY = bin/Release/MonoTorrent.dll
ASSEMBLY_MDB = 
COMPILE_TARGET = library
PROJECT_REFERENCES = 
BUILD_DIR = bin/Release/


endif

AL=al2
SATELLITE_ASSEMBLY_NAME=MonoTorrent.resources.dll

LINUX_PKGCONFIG = \
	$(MONOTORRENT_PC)  


RESGEN=resgen2
	
all: $(ASSEMBLY) $(LINUX_PKGCONFIG) 

FILES =  \
	AssemblyInfo.cs \
	MonoTorrent.BEncoding/BEncodedDictionary.cs \
	MonoTorrent.BEncoding/BEncodedList.cs \
	MonoTorrent.BEncoding/BEncodedNumber.cs \
	MonoTorrent.BEncoding/BEncodedString.cs \
	MonoTorrent.BEncoding/BEncodingException.cs \
	MonoTorrent.BEncoding/IBEncodedValue.cs \
	MonoTorrent.Client/AllowedFastAlgorithm.cs \
	MonoTorrent.Client/Block.cs \
	MonoTorrent.Client/BufferedFileRead.cs \
	MonoTorrent.Client/ClientEngine.cs \
	MonoTorrent.Client/ConnectionListeners/ConnectionListenerBase.cs \
	MonoTorrent.Client/ConnectionListeners/SocketListener.cs \
	MonoTorrent.Client/Encryption/BigInteger/BigInteger.cs \
	MonoTorrent.Client/Encryption/BigInteger/ConfidenceFactor.cs \
	MonoTorrent.Client/Encryption/BigInteger/NextPrimeFinder.cs \
	MonoTorrent.Client/Encryption/BigInteger/PrimalityTests.cs \
	MonoTorrent.Client/Encryption/BigInteger/PrimeGeneratorBase.cs \
	MonoTorrent.Client/Encryption/BigInteger/SequentialSearch.cs \
	MonoTorrent.Client/Encryption/EncryptedSocket.cs \
	MonoTorrent.Client/Encryption/IEncryption.cs \
	MonoTorrent.Client/Encryption/IEncryptor.cs \
	MonoTorrent.Client/Encryption/ModuloCalculator.cs \
	MonoTorrent.Client/Encryption/NoEncryption.cs \
	MonoTorrent.Client/Encryption/NullEncryption.cs \
	MonoTorrent.Client/Encryption/PeerAEncryption.cs \
	MonoTorrent.Client/Encryption/PeerBEncryption.cs \
	MonoTorrent.Client/Encryption/RC4.cs \
	MonoTorrent.Client/EventArgs/AnnounceResponseEventArgs.cs \
	MonoTorrent.Client/EventArgs/BlockEventArgs.cs \
	MonoTorrent.Client/EventArgs/CriticalExceptionEventArgs.cs \
	MonoTorrent.Client/EventArgs/MessageEventArgs.cs \
	MonoTorrent.Client/EventArgs/NewConnectionEventArgs.cs \
	MonoTorrent.Client/EventArgs/PeerConnectionEventArgs.cs \
	MonoTorrent.Client/EventArgs/PeersAddedEventArgs.cs \
	MonoTorrent.Client/EventArgs/PieceEventArgs.cs \
	MonoTorrent.Client/EventArgs/PieceHashedEventArgs.cs \
	MonoTorrent.Client/EventArgs/ScrapeResponseEventArgs.cs \
	MonoTorrent.Client/EventArgs/StateUpdateEventArgs.cs \
	MonoTorrent.Client/EventArgs/TorrentEventArgs.cs \
	MonoTorrent.Client/EventArgs/TorrentStateChangedEventArgs.cs \
	MonoTorrent.Client/EventArgs/TrackerRequestEventArgs.cs \
	MonoTorrent.Client/EventArgs/TrackerStateChangedEventArgs.cs \
	MonoTorrent.Client/Exceptions/ConnectionException.cs \
	MonoTorrent.Client/Exceptions/ListenerException.cs \
	MonoTorrent.Client/Exceptions/MessageException.cs \
	MonoTorrent.Client/Exceptions/ProtocolException.cs \
	MonoTorrent.Client/Exceptions/TorrentLoadException.cs \
	MonoTorrent.Client/FastResume/FastResume.cs \
	MonoTorrent.Client/FileStreamBuffer.cs \
	MonoTorrent.Client/Logger.cs \
	MonoTorrent.Client/Managers/BufferManager.cs \
	MonoTorrent.Client/Managers/ChokeUnchokeManager.cs \
	MonoTorrent.Client/Managers/ConnectionManager.cs \
	MonoTorrent.Client/Managers/ConnectionMonitor.cs \
	MonoTorrent.Client/Managers/DiskManager.cs \
	MonoTorrent.Client/Managers/FileManager.cs \
	MonoTorrent.Client/Managers/ListenManager.cs \
	MonoTorrent.Client/Managers/PeerManager.cs \
	MonoTorrent.Client/Managers/PieceManager.cs \
	MonoTorrent.Client/Managers/TorrentManager.cs \
	MonoTorrent.Client/Managers/TrackerManager.cs \
	MonoTorrent.Client/ManagerWaitHandle.cs \
	MonoTorrent.Client/MessageHandler.cs \
	MonoTorrent.Client/Messages/FastPeerExtensions/AllowedFastMessage.cs \
	MonoTorrent.Client/Messages/FastPeerExtensions/HaveAllMessage.cs \
	MonoTorrent.Client/Messages/FastPeerExtensions/HaveNoneMessage.cs \
	MonoTorrent.Client/Messages/FastPeerExtensions/RejectRequestMessage.cs \
	MonoTorrent.Client/Messages/FastPeerExtensions/SuggestPieceMessage.cs \
	MonoTorrent.Client/Messages/IMessage.cs \
	MonoTorrent.Client/Messages/LibtorrentMessages/ExtendedHandshakeMessage.cs \
	MonoTorrent.Client/Messages/LibtorrentMessages/LibtorrentMessage.cs \
	MonoTorrent.Client/Messages/LibtorrentMessages/LTChat.cs \
	MonoTorrent.Client/Messages/LibtorrentMessages/LTMetadata.cs \
	MonoTorrent.Client/Messages/LibtorrentMessages/LTSupport.cs \
	MonoTorrent.Client/Messages/Message.cs \
	MonoTorrent.Client/Messages/MessageBundle.cs \
	MonoTorrent.Client/Messages/PeerMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/BitfieldMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/CancelMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/ChokeMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/HandshakeMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/HaveMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/InterestedMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/KeepAliveMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/NotInterestedMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/PieceMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/PortMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/RequestMessage.cs \
	MonoTorrent.Client/Messages/StandardMessages/UnchokeMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/AnnounceMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/AnnounceResponseMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/ConnectMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/ConnectResponseMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/ErrorMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/Extensions/AuthenticationMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/PeerDetailsMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/ScrapeDetails.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/ScrapeMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/ScrapeResponseMessage.cs \
	MonoTorrent.Client/Messages/UdpTrackerMessages/UdpTrackerMessage.cs \
	MonoTorrent.Client/Messages/UnknownMessage.cs \
	MonoTorrent.Client/PeerConnections/ConnectionFactory.cs \
	MonoTorrent.Client/PeerConnections/HTTPConnection.cs \
	MonoTorrent.Client/PeerConnections/IConnection.cs \
	MonoTorrent.Client/PeerConnections/IPV6Connection.cs \
	MonoTorrent.Client/PeerConnections/PeerConnectionBase.cs \
	MonoTorrent.Client/PeerConnections/PeerId.cs \
	MonoTorrent.Client/PeerConnections/PeerIdInternal.cs \
	MonoTorrent.Client/PeerConnections/TCPConnection.cs \
	MonoTorrent.Client/Peers/Peer.cs \
	MonoTorrent.Client/Peers/PeerList.cs \
	MonoTorrent.Client/Piece.cs \
	MonoTorrent.Client/PiecePicking/EndGamePicker.cs \
	MonoTorrent.Client/PiecePicking/PiecePickerBase.cs \
	MonoTorrent.Client/PiecePicking/StandardPicker.cs \
	MonoTorrent.Client/PieceWriter/DiskWriter.cs \
	MonoTorrent.Client/PieceWriter/IPieceWriter.cs \
	MonoTorrent.Client/PieceWriter/MemoryWriter.cs \
	MonoTorrent.Client/PieceWriter/PieceData.cs \
	MonoTorrent.Client/PieceWriter/Pressure.cs \
	MonoTorrent.Client/RateLimiter.cs \
	MonoTorrent.Client/ReaderLock.cs \
	MonoTorrent.Client/Settings/EngineSettings.cs \
	MonoTorrent.Client/Settings/TorrentSettings.cs \
	MonoTorrent.Client/TorrentFileStream.cs \
	MonoTorrent.Client/Tracker/AnnounceParameters.cs \
	MonoTorrent.Client/Tracker/HTTPTracker.cs \
	MonoTorrent.Client/Tracker/ScrapeParameters.cs \
	MonoTorrent.Client/Tracker/Tracker.cs \
	MonoTorrent.Client/Tracker/TrackerConnectionId.cs \
	MonoTorrent.Client/Tracker/TrackerFactory.cs \
	MonoTorrent.Client/Tracker/TrackerTier.cs \
	MonoTorrent.Client/Tracker/UdpTracker.cs \
	MonoTorrent.Client/TransferType.cs \
	MonoTorrent.Client/WriterLock.cs \
	MonoTorrent.Common/BitField.cs \
	MonoTorrent.Common/Enums.cs \
	MonoTorrent.Common/EventArgs/TorrentCreatorEventArgs.cs \
	MonoTorrent.Common/Hashes.cs \
	MonoTorrent.Common/MonoTorrentCollectionBase.cs \
	MonoTorrent.Common/Peer.cs \
	MonoTorrent.Common/PeerID.cs \
	MonoTorrent.Common/SpeedMonitor.cs \
	MonoTorrent.Common/ToolBox.cs \
	MonoTorrent.Common/Torrent.cs \
	MonoTorrent.Common/TorrentCreator.cs \
	MonoTorrent.Common/TorrentCreatorAsyncResult.cs \
	MonoTorrent.Common/TorrentException.cs \
	MonoTorrent.Common/TorrentFile.cs \
	MonoTorrent.Common/VersionInfo.cs \
	MonoTorrent.TorrentWatchers/ITorrentWatcher.cs \
	MonoTorrent.TorrentWatchers/TorrentFolderWatcher.cs \
	MonoTorrent.TorrentWatchers/TorrentWatcherEventArgs.cs \
	MonoTorrent.TorrentWatchers/TorrentWatchers.cs \
	MonoTorrent.Tracker/ByteComparer.cs \
	MonoTorrent.Tracker/Exceptions/TrackerException.cs \
	MonoTorrent.Tracker/Frontend/AnnounceParameters.cs \
	MonoTorrent.Tracker/Frontend/ScrapeParameters.cs \
	MonoTorrent.Tracker/IIntervalAlgorithm.cs \
	MonoTorrent.Tracker/InfoHashTrackable.cs \
	MonoTorrent.Tracker/IPAddressComparer.cs \
	MonoTorrent.Tracker/ITrackable.cs \
	MonoTorrent.Tracker/Listeners/HttpListener.cs \
	MonoTorrent.Tracker/Listeners/ListenerBase.cs \
	MonoTorrent.Tracker/Listeners/ManualListener.cs \
	MonoTorrent.Tracker/Peer.cs \
	MonoTorrent.Tracker/RequestMonitor.cs \
	MonoTorrent.Tracker/RequestParameters.cs \
	MonoTorrent.Tracker/SimpleTorrentManager.cs \
	MonoTorrent.Tracker/StaticIntervalAlgorithm.cs \
	MonoTorrent.Tracker/Tracker.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = \
	monotorrent.pc.in 

REFERENCES =  \
	System \
	System.Data \
	System.Web \
	System.Xml

DLL_REFERENCES = 

CLEANFILES = $(LINUX_PKGCONFIG) 

include $(top_srcdir)/Makefile.include

MONOTORRENT_PC = $(BUILD_DIR)/monotorrent.pc

$(eval $(call emit-deploy-wrapper,MONOTORRENT_PC,monotorrent.pc))


$(eval $(call emit_resgen_targets))
$(build_xamlg_list): %.xaml.g.cs: %.xaml
	xamlg '$<'

$(ASSEMBLY) $(ASSEMBLY_MDB): $(build_sources) $(build_resources) $(build_datafiles) $(DLL_REFERENCES) $(PROJECT_REFERENCES) $(build_xamlg_list) $(build_satellite_assembly_list)
	mkdir -p $(shell dirname $(ASSEMBLY))
	$(ASSEMBLY_COMPILER_COMMAND) $(ASSEMBLY_COMPILER_FLAGS) -out:$(ASSEMBLY) -target:$(COMPILE_TARGET) $(build_sources_embed) $(build_resources_embed) $(build_references_ref)
