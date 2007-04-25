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
	-mkdir -p $(prefix)/lib/bitsharp
	-mkdir -p $(prefix)/bin
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/MonoTorrent.exe/' < script.in > monotorrent.x
	chmod +x monotorrent.x
	mv monotorrent.x $(prefix)/bin/monotorrent.x
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/MonoTorrent.Interface.exe/' < script.in > monotorrent-gtk
	chmod +x monotorrent-gtk
	mv monotorrent-gtk $(prefix)/bin/monotorrent-gtk
	cp Libs/Upnp.dll $(prefix)/lib/bitsharp/

dist:
	rm -rf monotorrent-$(VERSION)
	mkdir monotorrent-$(VERSION)
	(make distlocal DESTDIR=monotorrent-$(VERSION))
	for dir in $(SUBDIRS); do \
		mkdir monotorrent-$(VERSION)/$$dir || true; \
		(cd $$dir; make distlocal DESTDIR=../monotorrent-$(VERSION)/$$dir);  \
	done
	tar czvf monotorrent-$(VERSION).tar.gz monotorrent-$(VERSION)
	rm -rf monotorrent-$(VERSION)
	echo monotorrent-$(VERSION) has been packaged

distlocal:
	mkdir $(DESTDIR)/Libs
	cp Libs/Upnp.dll Libs/Upnp.xml $(DESTDIR)/Libs
	cp README rules.make TODO script.in Makefile configure $(DESTDIR)/

distcheck: dist
	(mkdir test; cd test;  \
	 tar xzvf ../monotorrent-$(VERSION).tar.gz; cd monotorrent-$(VERSION); \
	 ./configure --prefix=$$(cd `pwd`/..; pwd); \
	 make && make install && make dist);
	rm -rf test
