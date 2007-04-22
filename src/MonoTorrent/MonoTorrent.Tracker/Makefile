OUT=../bin/MonoTorrent.Tracker.dll
REFERENCES=	\
	-r:../bin/MonoTorrent.Common.dll	\
	-r:System.Web				\
	-r:../bin/MonoTorrent.BEncoding.dll

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

all: $(OUT)

include ../rules.make