TARGET=-target:exe
OUT=../bin/MonoTorrent.exe

REFERENCES=\
	-r:../bin/MonoTorrent.Common.dll	\
	-r:../bin/MonoTorrent.Client.dll	\
	-r:../bin/MonoTorrent.BEncoding.dll	\
	-r:../Libs/Upnp.dll

SOURCES=main.cs \
	Top10Listener.cs

all: $(OUT)

include ../rules.make
TARGET=-target:exe