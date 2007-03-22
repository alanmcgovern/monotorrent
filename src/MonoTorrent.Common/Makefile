CSC=gmcs 
OUT=../bin/MonoTorrent.Common.dll
REFERENCES=../bin/MonoTorrent.BEncoding.dll
SOURCES=Enums.cs \
	Hashes.cs \
	Peer.cs \
	PeerID.cs \
	ToolBox.cs \
	TorrentCreator.cs \
	Torrent.cs \
	TorrentFile.cs \
	TorrentException.cs \
	VersionInfo.cs \
	TorrentWatchers/ITorrentWatcher.cs \
	TorrentWatchers/TorrentFolderWatcher.cs \
	TorrentWatchers/TorrentWatcherEventArgs.cs \
	TorrentWatchers/TorrentWatchers.cs \
#	Test/BEncodingTest.cs \
#	Test/CatStreamReaderTest.cs \
#	Test/TorrentCreatorSingleFileTest.cs \
#	Test/TorrentCreatorTest.cs \
#	Test/TorrentTest.cs



all: $(OUT)

$(OUT): $(SOURCES)
	mkdir -p ../bin
	$(CSC) $(SOURCES) -target:library -d:DEBUG -out:$(OUT) -r:$(REFERENCES)

