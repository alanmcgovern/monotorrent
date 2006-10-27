CSC=gmcs 
OUT=../TestClient.exe
REFERENCES=../Common.dll,../Client.dll
SOURCES=main.cs 

all: testclient

testclient: $(SOURCES)
	$(CSC) $(SOURCES) -target:exe -d:DEBUG -out:$(OUT) -r:$(REFERENCES)
