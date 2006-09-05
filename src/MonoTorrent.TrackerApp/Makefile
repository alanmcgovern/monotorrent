CSC=gmcs 
OUT=../TrackerApp.exe
REFERENCES=../Common.dll,../Tracker.dll
SOURCES=Main.cs AssemblyInfo.cs

all: trackerapp

trackerapp: $(SOURCES)
	$(CSC) $(SOURCES) -target:exe -out:$(OUT) -r:$(REFERENCES)
