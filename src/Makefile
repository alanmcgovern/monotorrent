all: bencoding common client tracker trackerapp testclient

bencoding:
	cd MonoTorrent.BEncoding && $(MAKE)

common:
	cd MonoTorrent.Common && $(MAKE)

client: 
	cd MonoTorrent.Client && $(MAKE)
	
tracker:
	cd MonoTorrent.Tracker && $(MAKE)

trackerapp:
	cd MonoTorrent.TrackerApp && $(MAKE)

testclient:
	cd MonoTorrent && $(MAKE)

interface:
	cd MonoTorrent.Interface/src && $(MAKE)
