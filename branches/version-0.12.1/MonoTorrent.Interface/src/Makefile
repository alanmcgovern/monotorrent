TARGET=-target:winexe
EXTRADIR=../
OUT=../../bin/MonoTorrent.Interface.exe
SOURCES=MonoTorrent.Interface/Program.cs \
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
	MonoTorrent.Interface/Settings/Configuration.cs \
	MonoTorrent.Interface/Settings/BEncodedSettingsStorage.cs \
	AssemblyInfo.cs

PKGS=-pkg:gtk-sharp-2.0 -pkg:gconf-sharp-2.0

REFERENCES=\
	-r:../../bin/MonoTorrent.Client.dll \
	-r:../../bin/MonoTorrent.Common.dll \
	-r:../../bin/MonoTorrent.BEncoding.dll \
	-r:Mono.Posix

RESOURCES=../res/ui.xml \
	../COPYING

RESOURCE_B=$(addprefix -resource:, $(RESOURCES))

all: $(OUT)

include ../../rules.make
