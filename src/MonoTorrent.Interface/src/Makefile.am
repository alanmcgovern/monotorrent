monotorrentdir = $(pkglibdir)
monotorrent_SCRIPTS = $(PACKAGE).exe $(PACKAGE).exe.mdb
dist_monotorrent_DATA = $(monotorrent_libs) $(monotorrent_libs_debug)

EXTRA_DIST = $(monotorrent_sources) $(monotorrent_sources_in)

CLEANFILES = $(monotorrent_SCRIPTS)
DISTCLEANFILES = Makefile.in

monotorrent_sources = \
	MonoTorrent.Interface/Program.cs \
	MonoTorrent.Interface/PathConstants.cs \
	MonoTorrent.Interface/Model/TorrentsList.cs \
	MonoTorrent.Interface/Model/TorrentFilesList.cs \
	MonoTorrent.Interface/Model/TorrentsRepository.cs \
	MonoTorrent.Interface/View/TreeViewWithPopup.cs \
	MonoTorrent.Interface/View/DialogWithTitle.cs \
	MonoTorrent.Interface/View/MainWindow.cs \
	MonoTorrent.Interface/View/NewTorrentDialog.cs \
	MonoTorrent.Interface/View/TorrentsView.cs \
	MonoTorrent.Interface/View/TorrentView.cs \
	MonoTorrent.Interface/View/TorrentFilesView.cs \
	MonoTorrent.Interface/View/TorrentChooserDialog.cs \
	MonoTorrent.Interface/View/UIBuilder.cs \
	MonoTorrent.Interface/View/ActionConstants.cs \
	MonoTorrent.Interface/View/PreferencesDialog.cs \
	MonoTorrent.Interface/View/MonoTorrentStatusbar.cs \
	MonoTorrent.Interface/Controller/WindowController.cs \
	MonoTorrent.Interface/Controller/TorrentsController.cs \
	MonoTorrent.Interface/Controller/PreferencesController.cs \
	MonoTorrent.Interface/Helpers/Formatter.cs \
	MonoTorrent.Interface/Helpers/AssemblyInfo.cs \
	MonoTorrent.Interface/Settings/ISettingsStorage.cs \
	MonoTorrent.Interface/Settings/SettingsStorage.cs \
	MonoTorrent.Interface/Settings/GConfSettingsStorage.cs \
	MonoTorrent.Interface/Settings/Configuration.cs

monotorrent_sources_in = \
	AssemblyInfo.cs.in
monotorrent_generated_sources = $(monotorrent_sources_in:.in=)

monotorrent_build_sources = $(addprefix $(srcdir)/, $(monotorrent_sources))
monotorrent_build_sources += $(monotorrent_generated_sources)

monotorrent_resources = \
	$(srcdir)/../COPYING \
	$(srcdir)/../res/ui.xml
monotorrent_resources_build = $(addprefix -resource:, $(monotorrent_resources))

monotorrent_libs = \
	MonoTorrent.Common.dll \
	MonoTorrent.Client.dll
monotorrent_libs_build = $(addprefix -r:, $(monotorrent_libs))
monotorrent_libs_debug = $(addsuffix .mdb, $(monotorrent_libs))

$(PACKAGE).exe: $(monotorrent_build_sources) $(monotorrent_resources) $(monotorrent_libs)
	$(MCS) -debug $(GTKSHARP_LIBS) $(GCONFSHARP_LIBS) $(monotorrent_libs_build) -r:Mono.Posix $(monotorrent_resources_build) -out:$@ $(monotorrent_build_sources)

install-data-hook:
	for ASM in $(INSTALLED_ASSEMBLIES); do \
		$(INSTALL) -c -m 0755 $$ASM $(DESTDIR)$(pkglibdir); \
	done;

uninstall-hook:
	for ASM in $(INSTALLED_ASSEMBLIES); do \
		rm -f $(DESTDIR)$(pkglibdir)/`basename $$ASM`; \
	done;
