#
# Simple Makefile
#

CSC = gmcs
OPTS = -debug
REFS = -r:System.Web
LIBS = $(CLIENT_TARGET) $(TRACKER_TARGET) $(COMMON_TARGET)
DEBUG = $(CLIENT_TARGET).mdb $(TRACKER_TARGET).mdb $(COMMON_TARGET).mdb

COMMON_TARGET = MonoTorrent.Common.dll
COMMON_DIR = MonoTorrent.Common
COMMON_SOURCES = \
		 $(COMMON_DIR)/BEncode/BEncode.cs \
		 $(COMMON_DIR)/BEncode/BEncodedDictionary.cs \
		 $(COMMON_DIR)/BEncode/BEncodedList.cs \
		 $(COMMON_DIR)/BEncode/BEncodedNumber.cs \
		 $(COMMON_DIR)/BEncode/BEncodedString.cs \
		 $(COMMON_DIR)/BEncode/BEncodingException.cs \
		 $(COMMON_DIR)/BEncode/IBEncodedValue.cs \
		 $(COMMON_DIR)/Enums.cs \
		 $(COMMON_DIR)/IPeer.cs \
		 $(COMMON_DIR)/ITorrent.cs \
		 $(COMMON_DIR)/ITorrentFile.cs \
		 $(COMMON_DIR)/OrderedDictionary.cs \
		 $(COMMON_DIR)/Peer.cs \
		 $(COMMON_DIR)/PeerID.cs \
		 $(COMMON_DIR)/ToolBox.cs \
		 $(COMMON_DIR)/TorrentCreator.cs \
		 $(COMMON_DIR)/Torrent.cs \
		 $(COMMON_DIR)/TorrentFile.cs \
		 $(COMMON_DIR)/TorrentReader.cs \
		 $(COMMON_DIR)/VersionInfo.cs

CLIENT_TARGET = MonoTorrent.Client.dll
CLIENT_DIR = MonoTorrent.Client
CLIENT_SOURCES = \
		 $(CLIENT_DIR)/IClientEngine.cs \
		 $(CLIENT_DIR)/IEngineSettings.cs \
		 $(CLIENT_DIR)/IPieceManager.cs \
		 $(CLIENT_DIR)/ITorrentManager.cs \
		 $(CLIENT_DIR)/IPeerConnectionID.cs \
		 $(CLIENT_DIR)/BitField.cs \
		 $(CLIENT_DIR)/Block.cs \
		 $(CLIENT_DIR)/ClientEngine.cs \
		 $(CLIENT_DIR)/ClientPeer.cs \
		 $(CLIENT_DIR)/ConnectionListener.cs \
		 $(CLIENT_DIR)/PeerConnection.cs \
		 $(CLIENT_DIR)/PeerConnectionId.cs \
		 $(CLIENT_DIR)/Peers.cs \
		 $(CLIENT_DIR)/Piece.cs \
		 $(CLIENT_DIR)/TrackerConnection.cs \
		 $(CLIENT_DIR)/TrackerConnectionId.cs \
		 $(CLIENT_DIR)/EventArgs/MessageEventArgs.cs \
		 $(CLIENT_DIR)/EventArgs/PeerConnectionEventArgs.cs \
		 $(CLIENT_DIR)/EventArgs/PeersAddedEventArgs.cs \
		 $(CLIENT_DIR)/EventArgs/PieceEventArgs.cs \
		 $(CLIENT_DIR)/EventArgs/PieceHashedEventArgs.cs \
		 $(CLIENT_DIR)/EventArgs/TrackerEventArgs.cs \
		 $(CLIENT_DIR)/EventArgs/TorrentStateChangedEventArgs.cs \
		 $(CLIENT_DIR)/Managers/ConnectionManager.cs \
		 $(CLIENT_DIR)/Managers/ConnectionMonitor.cs \
		 $(CLIENT_DIR)/Managers/FileManager.cs \
		 $(CLIENT_DIR)/Managers/PieceManager.cs \
		 $(CLIENT_DIR)/Managers/TorrentManager.cs \
		 $(CLIENT_DIR)/PeerMessages/BitfieldMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/CancelMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/ChokeMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/HandshakeMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/HaveMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/InterestedMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/IPeerMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/KeepAliveMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/NotInterestedMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/PeerwireEncoder.cs \
		 $(CLIENT_DIR)/PeerMessages/PieceMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/PortMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/RequestMessage.cs \
		 $(CLIENT_DIR)/PeerMessages/UnchokeMessage.cs \
		 $(CLIENT_DIR)/Settings/EngineSettings.cs \
		 $(CLIENT_DIR)/Settings/TorrentSettings.cs

TRACKER_TARGET = MonoTorrent.Tracker.dll
TRACKER_DIR = MonoTorrent.Tracker
TRACKER_SOURCES = \
		 $(TRACKER_DIR)/IIntervalAlgorithm.cs \
		 $(TRACKER_DIR)/ITorrentManager.cs \
		 $(TRACKER_DIR)/Peer.cs \
		 $(TRACKER_DIR)/PeerManager.cs \
		 $(TRACKER_DIR)/SimpleTorrentManager.cs \
		 $(TRACKER_DIR)/StaticIntervalAlgorithm.cs \
		 $(TRACKER_DIR)/Tracker.cs \
		 $(TRACKER_DIR)/TrackerEngine.cs \
		 $(TRACKER_DIR)/Frontend/AnnounceParameters.cs \
		 $(TRACKER_DIR)/Frontend/AnnounceParameterParser.cs \
		 $(TRACKER_DIR)/Frontend/ParameterParser.cs \
		 $(TRACKER_DIR)/Frontend/ScrapeParameters.cs \
		 $(TRACKER_DIR)/Frontend/ScrapeParameterParser.cs \
		 $(TRACKER_DIR)/Frontend/InternalHttpServer.cs

all: $(LIBS)

$(CLIENT_TARGET): $(COMMON_TARGET) $(CLIENT_SOURCES)
	$(CSC) $(OPTS) $(REFS) -r:$< -target:library -out:$@ $(CLIENT_SOURCES)

$(TRACKER_TARGET): $(COMMON_TARGET) $(TRACKER_SOURCES)
	$(CSC) $(OPTS) $(REFS) -r:$< -target:library -out:$@ $(TRACKER_SOURCES)

$(COMMON_TARGET): $(COMMON_SOURCES)
	$(CSC) $(OPTS) $(REFS) -target:library -out:$@ $(COMMON_SOURCES)

clean:
	rm -f $(LIBS) $(DEBUG)
