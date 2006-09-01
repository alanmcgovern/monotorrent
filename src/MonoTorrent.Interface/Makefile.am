SUBDIRS = res src po
bin_SCRIPTS = monotorrent

REWRITE = \
	sed -e "s|\@prefix\@|$(prefix)|g" \
	    -e "s|\@pkglibdir\@|$(pkglibdir)|g" \
	    -e "s|\@bindir\@|$(bindir)|g" \
	    -e "s|\@MONO\@|$(MONO)|g" \
	    -e "s|\@PACKAGE\@|$(PACKAGE)|g"

$(bin_SCRIPTS): $(srcdir)/$(PACKAGE).in Makefile
	$(REWRITE) $(srcdir)/$(PACKAGE).in >$@.tmp
	mv $@.tmp $@

desktopdir = $(datadir)/applications
dist_desktop_DATA = monotorrent.desktop

EXTRA_DIST = \
	$(PACKAGE).in \
	intltool-extract.in \
	intltool-merge.in \
	intltool-update.in

CLEANFILES = \
	$(PACKAGE) \
	intltool-extract \
	intltool-merge \
	intltool-update

DISTCLEANFILES = \
	aclocal.m4 \
	config.guess \
	config.sub \
	configure \
	install-sh \
	Makefile.in \
	missing \
	intltool-extract.in \
	intltool-merge.in \
	intltool-update.in \
	mkinstalldirs \
	po/Makefile.in.in

distclean-local:
	rm -rf autom4te.cache
