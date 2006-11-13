CSC=gmcs
OUT=../../MonoTorrent.Interface.exe
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
	AssemblyInfo.cs
REFERENCES=../../Client.dll \
	../../Common.dll
RESOURCES=../res/ui.xml \
	../COPYING

REFERENCES_B=$(addprefix -reference:, $(REFERENCES))
RESOURCE_B=$(addprefix -resource:, $(RESOURCES))

all: interface

interface: $(SOURCES)
	$(CSC) $(SOURCES) $(RESOURCE_B) $(REFERENCES_B) -target:winexe -d:DEBUG -out:$(OUT)