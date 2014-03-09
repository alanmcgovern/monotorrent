CONFIG=config.make

XBUILD=xbuild
XBUILD_ARGS=/verbosity:quiet /nologo /property:Configuration=$(MONOTORRENT_PROFILE)
MAIN_SLN=src/MonoTorrent.sln
DIST_FILE=monotorrent-$(MONOTORRENT_VERSION).tar.gz

all:
	@echo Building $(MAIN_SLN)
	@$(XBUILD) $(XBUILD_ARGS) $(MAIN_SLN)

clean:
	@echo Cleaning $(MAIN_SLN)
	@$(XBUILD) $(XBUILD_ARGS) $(MAIN_SLN) /t:Clean

dist:
	git archive --format=tar HEAD | gzip > $(DIST_FILE)

dist-clean:
	rm -f $(DIST_FILE)

dist-check:

install: $(CONFIG)
	@echo Installing MonoTorrent libraries
	mkdir -p $(DESTDIR)$(MONOTORRENT_INSTALL_DIR)
	cp -R build/MonoTorrent/$(MONOTORRENT_PROFILE)/* $(DESTDIR)$(MONOTORRENT_INSTALL_DIR)/

	@echo Installing pc files
	mkdir -p $(DESTDIR)$(pkgconfigdir)
	cp src/MonoTorrent/monotorrent.pc $(DESTDIR)$(pkgconfigdir)/
	cp src/MonoTorrent.Dht/monotorrent.dht.pc $(DESTDIR)$(pkgconfigdir)/

uninstall: $(CONFIG)
	@echo Removing MonoTorrent libraries
	rm -rf $(DESTDIR)/$(libdir)/monotorrent
	@echo Removing MonoTorrent pc files
	rm -f $(DESTDIR)$(pkgconfigdir)/monotorrent.pc
	rm -f $(DESTDIR)$(pkgconfigdir)/monotorrent.dht.pc

$(CONFIG):
	@if ! test -e "$(CONFIG)"; then \
	echo "You must run configure first" && exit 1; \
	fi


include $(CONFIG)

.PHONY: all clean dist dist-clean dist-check install uninstall
