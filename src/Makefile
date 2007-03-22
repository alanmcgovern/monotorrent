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

install: install-setup 
	for dir in $(SUBDIRS); do \
		(cd $$dir && make install) \
	done

include config.make

install-setup: 
	-mkdir $(prefix)/lib/bitsharp
	-mkdir $(prefix)/bin
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/MonoTorrent.exe/' < script.in > monotorrent
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/MonoTorrent.Interface.exe/' < script.in > monotorrent-gtk
	chmod +x monotorrent-gtk monotorrent
	cp monotorrent monotorrent-gtk $(prefix)/bin