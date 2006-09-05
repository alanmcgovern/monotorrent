CSC=gmcs 
OUT=../Tracker.dll
REFERENCES=../Common.dll,System.Web
SOURCES=Frontend/AnnounceHandler.cs \
	Frontend/AnnounceParameterParser.cs \
	Frontend/AnnounceParameters.cs \
	Frontend/global.asax.cs \
	Frontend/InternalHttpServer.cs \
	Frontend/ParameterParser.cs \
	Frontend/ScrapeHandler.cs \
	Frontend/ScrapeParameterParser.cs \
	Frontend/ScrapeParameters.cs \
	Frontend/TestHandler.cs \
	IIntervalAlgorithm.cs \
	ITorrentManager.cs \
	Peer.cs \
	PeerManager.cs \
	SimpleTorrentManager.cs \
	StaticIntervalAlgorithm.cs \
	Tracker.cs \
	TrackerEngine.cs 


all: tracker

tracker: $(SOURCES)
	$(CSC) $(SOURCES) -target:library -out:$(OUT) -r:$(REFERENCES)
