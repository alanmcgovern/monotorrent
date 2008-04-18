
EXTRA_DIST =  

# Warning: This is an automatically generated file, do not edit!

if ENABLE_DEBUG_ANY_CPU
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -warn:4 -optimize- -debug "-define:DEBUG;TRACE"

ASSEMBLY = bin/Debug/SampleClient.exe
ASSEMBLY_MDB = $(ASSEMBLY).mdb
COMPILE_TARGET = exe
PROJECT_REFERENCES =  \
	../MonoTorrent/bin/Debug/MonoTorrent.dll
BUILD_DIR = bin/Debug/

MONOTORRENT_DLL_MDB_SOURCE=../MonoTorrent/bin/Debug/MonoTorrent.dll.mdb
MONOTORRENT_DLL_MDB=$(BUILD_DIR)/MonoTorrent.dll.mdb
MONOTORRENT_DLL_SOURCE=../MonoTorrent/bin/Debug/MonoTorrent.dll

endif

if ENABLE_RELEASE_ANY_CPU
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -warn:4 -optimize+ "-define:TRACE"

ASSEMBLY = bin/Release/SampleClient.exe
ASSEMBLY_MDB = 
COMPILE_TARGET = exe
PROJECT_REFERENCES =  \
	../MonoTorrent/bin/Release/MonoTorrent.dll
BUILD_DIR = bin/Release/

MONOTORRENT_DLL_MDB=
MONOTORRENT_DLL_SOURCE=../MonoTorrent/bin/Release/MonoTorrent.dll

endif

AL=al2
SATELLITE_ASSEMBLY_NAME=SampleClient.resources.dll

PROGRAMFILES = \
	$(MONOTORRENT_DLL_MDB) \
	$(MONOTORRENT_DLL)  

BINARIES = \
	$(SAMPLECLIENT)  


RESGEN=resgen2
	
all: $(ASSEMBLY) $(PROGRAMFILES) $(BINARIES) 

FILES = \
	main.cs \
	TestManualConnection.cs \
	Top10Listener.cs \
	Properties/AssemblyInfo.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = \
	sampleclient.in 

REFERENCES =  \
	System \
	System.Web \
	System.Xml

DLL_REFERENCES = 

CLEANFILES = $(PROGRAMFILES) $(BINARIES) 

include $(top_srcdir)/Makefile.include

SAMPLECLIENT = $(BUILD_DIR)/sampleclient
MONOTORRENT_DLL = $(BUILD_DIR)/MonoTorrent.dll

$(eval $(call emit-deploy-wrapper,SAMPLECLIENT,sampleclient,x))
$(eval $(call emit-deploy-target,MONOTORRENT_DLL_MDB))
$(eval $(call emit-deploy-target,MONOTORRENT_DLL))


$(eval $(call emit_resgen_targets))
$(build_xamlg_list): %.xaml.g.cs: %.xaml
	xamlg '$<'

$(ASSEMBLY) $(ASSEMBLY_MDB): $(build_sources) $(build_resources) $(build_datafiles) $(DLL_REFERENCES) $(PROJECT_REFERENCES) $(build_xamlg_list) $(build_satellite_assembly_list)
	mkdir -p $(shell dirname $(ASSEMBLY))
	$(ASSEMBLY_COMPILER_COMMAND) $(ASSEMBLY_COMPILER_FLAGS) -out:$(ASSEMBLY) -target:$(COMPILE_TARGET) $(build_sources_embed) $(build_resources_embed) $(build_references_ref)
