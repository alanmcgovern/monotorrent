CSC=gmcs 
OUT=../TestClient.exe
REFERENCES=../Common.dll,../Client.dll
SOURCES=main.cs 

all: testclient

testclient: $(SOURCES)
	$(CSC) $(SOURCES) -target:exe -out:$(OUT) -r:$(REFERENCES)
