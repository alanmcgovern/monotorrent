CSC=gmcs 
OUT=../TrackerApp.exe
REFERENCES=../bin/MonoTorrent.Common.dll,../bin/MonoTorrent.Tracker.dll
SOURCES=Main.cs AssemblyInfo.cs

all: trackerapp

trackerapp: $(SOURCES)
	mkdir -p ../bin
	$(CSC) $(SOURCES) -target:exe -out:$(OUT) -r:$(REFERENCES)
