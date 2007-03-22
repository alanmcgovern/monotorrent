CSC=gmcs 
OUT=../bin/MonoTorrent.exe
REFERENCES=../bin/MonoTorrent.Common.dll,../bin/MonoTorrent.Client.dll,../bin/MonoTorrent.BEncoding.dll,../Libs/Upnp.dll
SOURCES=main.cs \
	Top10Listener.cs

all: testclient

testclient: $(SOURCES)
	mkdir -p ../bin
	$(CSC) $(SOURCES) -target:exe -d:DEBUG -out:$(OUT) -r:$(REFERENCES)
	cp ../Libs/Upnp.dll ../bin/Upnp.dll