CSC=gmcs 
OUT=../bin/MonoTorrent.BEncoding.dll
REFERENCES=System
SOURCES=AssemblyInfo.cs \
	BEncode.cs \
	BEncodedDictionary.cs \
	BEncodedList.cs \
	BEncodedNumber.cs \
	BEncodedString.cs \
	BEncodingException.cs \
	IBEncodedValue.cs


all: bencoding

bencoding: $(SOURCES)
	mkdir -p ../bin
	$(CSC) -r:$(REFERENCES) -target:library -d:DEBUG -out:$(OUT) $(SOURCES)
