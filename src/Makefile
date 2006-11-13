all: common client tracker trackerapp testclient interface


common:
	cd MonoTorrent.Common && $(MAKE)

client: 
	cd MonoTorrent.Client && $(MAKE)
	
tracker:
	cd MonoTorrent.Tracker && $(MAKE)

trackerapp:
	cd MonoTorrent.TrackerApp && $(MAKE)

testclient:
	cd TestClient && $(MAKE)

interface:
	cd MonoTorrent.Interface/src && $(MAKE)
