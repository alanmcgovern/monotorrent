
XBUILD=msbuild
XBUILD_ARGS=/nologo /restore

all:
	@echo Building
	$(XBUILD) $(XBUILD_ARGS)

clean:
	@echo Cleaning $(MAIN_SLN)
	$(XBUILD) $(XBUILD_ARGS) /t:Clean

.PHONY: all clean
