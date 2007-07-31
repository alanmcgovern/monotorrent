SUBDIRS = MonoTorrent TrackerApp SampleClient

all: config.make monotorrent.pc
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

monotorrent.pc: monotorrent.pc.in Makefile
	sed -e "s,@PREFIX@,$(prefix)," -e "s/@VERSION@/$(VERSION)/" < monotorrent.pc.in > monotorrent.pc

install-setup: monotorrent.pc
	-mkdir -p $(prefix)/lib/bitsharp
	-mkdir -p $(prefix)/bin
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/SampleClient.exe/' < script.in > monotorrent.x
	chmod +x monotorrent.x
	mv monotorrent.x $(prefix)/bin/monotorrent.x
	sed -e "s,@prefix@,$(prefix)," -e 's/@cmd@/MonoTorrent.Interface.exe/' < script.in > monotorrent-gtk
	chmod +x monotorrent-gtk
	mv monotorrent-gtk $(prefix)/bin/monotorrent-gtk
	-mkdir $(prefix)/lib/pkgconfig
	cp monotorrent.pc $(prefix)/lib/pkgconfig/

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
	cp README rules.make TODO script.in Makefile configure monotorrent.pc.in $(DESTDIR)/

distcheck: dist
	(mkdir test; cd test;  \
	 tar xzvf ../monotorrent-$(VERSION).tar.gz; cd monotorrent-$(VERSION); \
	 ./configure --prefix=$$(cd `pwd`/..; pwd); \
	 make && make install && make dist);
	rm -rf test
