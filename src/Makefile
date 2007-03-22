SUBDIRS = MonoTorrent.BEncoding  MonoTorrent.Common MonoTorrent.Client MonoTorrent.Tracker  MonoTorrent.TrackerApp MonoTorrent MonoTorrent.Interface/src

all: config.make
	for dir in $(SUBDIRS); do \
		(cd $$dir && make) \
	done

clean:
	for dir in $(SUBDIRS); do \
		(cd $$dir && make clean)
	done

config.make:
	echo You must run configure first
	exit 1
