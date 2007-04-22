SUBDIRS = MonoTorrent TrackerApp SampleClient

all: config.make
	for dir in $(SUBDIRS); do \
		(cd $$dir && make) \
	done

clean:
	-for dir in $(SUBDIRS); do \
		(cd $$dir && make clean) \
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
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/MonoTorrent.exe/' < script.in > monotorrent.x
	chmod +x monotorrent.x
	mv monotorrent.x $(prefix)/bin/monotorrent
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/MonoTorrent.Interface.exe/' < script.in > monotorrent-gtk
	chmod +x monotorrent-gtk
	mv monotorrent-gtk $(prefix)/bin/monotorrent-gtk
	cp Libs/Upnp.dll $(prefix)/lib/bitsharp/
