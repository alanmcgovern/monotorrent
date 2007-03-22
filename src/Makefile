SUBDIRS = MonoTorrent.BEncoding  MonoTorrent.Common MonoTorrent.Client MonoTorrent.Tracker  MonoTorrent.TrackerApp MonoTorrent MonoTorrent.Interface/src

all: 
	for dir in $(SUBDIRS); do \
		(cd $$dir && make) \
	done

clean:
	for dir in $(SUBDIRS); do \
		(cd $$dir && make clean)
	done