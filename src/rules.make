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
	cp $(OUT) $(prefix)/lib/bitsharp

distlocal:
	cp Makefile $(EXTRA_DIST) $(DESTDIR)
	for f in $(SOURCES); do d=`dirname $$f`; mkdir -p $(DESTDIR)/$$d || true; cp $$f $(DESTDIR)/$$d; done