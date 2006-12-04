CSC=gmcs 
OUT=../Common.dll
SOURCES=BEncode/BEncode.cs \
	BEncode/BEncodedDictionary.cs \
	BEncode/BEncodedList.cs \
	BEncode/BEncodedNumber.cs \
	BEncode/BEncodedString.cs \
	BEncode/BEncodingException.cs \
	BEncode/IBEncodedValue.cs \
	Enums.cs \
	Peer.cs \
	PeerID.cs \
	ToolBox.cs \
	TorrentCreator.cs \
	Torrent.cs \
	TorrentFile.cs \
	TorrentException.cs \
	VersionInfo.cs \
#	Test/BEncodingTest.cs \
#	Test/CatStreamReaderTest.cs \
#	Test/TorrentCreatorSingleFileTest.cs \
#	Test/TorrentCreatorTest.cs \
#	Test/TorrentTest.cs



all: common

common: $(SOURCES)
	$(CSC) $(SOURCES) -target:library -d:DEBUG -out:$(OUT)
	
