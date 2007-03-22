CSC=gmcs
TARGET=-target:library
rootdir=$(EXTRADIR)..
bindir=$(rootdir)/bin
include $(rootdir)/config.make

$(OUT): $(SOURCES)
	mkdir -p $(bindir)
	$(CSC) $(REFERENCES) $(RESOURCES_B) $(TARGET) $(PKGS) -d:DEBUG -out:$(OUT) $(SOURCES)

all: $(OUT) 

clean:
	-rm $(OUT)

install: 
	mkdir $(prefix)/lib/bitsharp
	mkdir $(prefix)/bin
	cp $(OUT) $(prefix)/lib/bitsharp
	