
EXTRA_DIST =  

# Warning: This is an automatically generated file, do not edit!

if ENABLE_DEBUG
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -unsafe -warn:4 -optimize- -debug "-define:TRACE;DEBUG"
ASSEMBLY = bin/Debug/MonoTorrent.Dht.dll
ASSEMBLY_MDB = $(ASSEMBLY).mdb
COMPILE_TARGET = library
PROJECT_REFERENCES =  \
	../MonoTorrent/bin/Debug/MonoTorrent.dll
BUILD_DIR = bin/Debug/

MONOTORRENT_DLL_SOURCE=../MonoTorrent/bin/Debug/MonoTorrent.dll
MONOTORRENT_DLL_MDB_SOURCE=../MonoTorrent/bin/Debug/MonoTorrent.dll.mdb
MONOTORRENT_DLL_MDB=$(BUILD_DIR)/MonoTorrent.dll.mdb
MONOTORRENT_DHT_DLL_MDB_SOURCE=bin/Debug/MonoTorrent.Dht.dll.mdb

endif

if ENABLE_RELEASE
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -unsafe -warn:4 -optimize+ -debug -define:DEBUG "-define:TRACE"
ASSEMBLY = bin/Release/MonoTorrent.Dht.dll
ASSEMBLY_MDB = $(ASSEMBLY).mdb
COMPILE_TARGET = library
PROJECT_REFERENCES =  \
	../MonoTorrent/bin/Release/MonoTorrent.dll
BUILD_DIR = bin/Release/

MONOTORRENT_DLL_SOURCE=../MonoTorrent/bin/Release/MonoTorrent.dll
MONOTORRENT_DLL_MDB=
MONOTORRENT_DHT_DLL_MDB_SOURCE=bin/Release/MonoTorrent.Dht.dll.mdb

endif

AL=al2
SATELLITE_ASSEMBLY_NAME=$(notdir $(basename $(ASSEMBLY))).resources.dll

PROGRAMFILES = \
	$(MONOTORRENT_DLL) \
	$(MONOTORRENT_DLL_MDB) \
	$(MONOTORRENT_DHT_DLL_MDB)  

LINUX_PKGCONFIG = \
	$(MONOTORRENT_DHT_PC)  


RESGEN=resgen2
	
all: $(ASSEMBLY) $(PROGRAMFILES) $(LINUX_PKGCONFIG) 

FILES = \
	BigInteger.cs \
	DhtEngine.cs \
	EventArgs/NodeAddedEventArgs.cs \
	EventArgs/SendMessageEventArgs.cs \
	EventArgs/TaskCompleteEventArgs.cs \
	Listeners/DhtListener.cs \
	Message.cs \
	MessageException.cs \
	MessageFactory.cs \
	MessageLoop.cs \
	Messages/Errors/ErrorMessage.cs \
	Messages/Queries/AnnouncePeer.cs \
	Messages/Queries/FindNode.cs \
	Messages/Queries/GetPeers.cs \
	Messages/Queries/Ping.cs \
	Messages/Queries/QueryMessage.cs \
	Messages/Responses/AnnouncePeerResponse.cs \
	Messages/Responses/FindNodeResponse.cs \
	Messages/Responses/GetPeersResponse.cs \
	Messages/Responses/PingResponse.cs \
	Messages/Responses/ResponseMessage.cs \
	Nodes/NodeId.cs \
	RoutingTable/Bucket.cs \
	Nodes/NodeState.cs \
	Nodes/Node.cs \
	RoutingTable/RoutingTable.cs \
	Tasks/AnnounceTask.cs \
	Tasks/GetPeersTask.cs \
	Tasks/InitialiseTask.cs \
	Tasks/ITask.cs \
	Tasks/RefreshBucketTask.cs \
	Tasks/ReplaceNodeTask.cs \
	Tasks/SendMessageTask.cs \
	Tasks/Task.cs \
	TransactionId.cs \
	Nodes/TokenManager.cs \
	EventArgs/NodeFoundEventArgs.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = \
	monotorrent.dht.pc.in 

REFERENCES =  \
	System \
	System.Data \
	System.Xml

DLL_REFERENCES = 

CLEANFILES = $(PROGRAMFILES) $(LINUX_PKGCONFIG) 

include $(top_srcdir)/Makefile.include

MONOTORRENT_DLL = $(BUILD_DIR)/MonoTorrent.dll
MONOTORRENT_DHT_DLL_MDB = $(BUILD_DIR)/MonoTorrent.Dht.dll.mdb
MONOTORRENT_DHT_PC = $(BUILD_DIR)/monotorrent.dht.pc

$(eval $(call emit-deploy-target,MONOTORRENT_DLL))
$(eval $(call emit-deploy-target,MONOTORRENT_DLL_MDB))
$(eval $(call emit-deploy-wrapper,MONOTORRENT_DHT_PC,monotorrent.dht.pc))


$(eval $(call emit_resgen_targets))
$(build_xamlg_list): %.xaml.g.cs: %.xaml
	xamlg '$<'

$(ASSEMBLY) $(ASSEMBLY_MDB): $(build_sources) $(build_resources) $(build_datafiles) $(DLL_REFERENCES) $(PROJECT_REFERENCES) $(build_xamlg_list) $(build_satellite_assembly_list)
	mkdir -p $(shell dirname $(ASSEMBLY))
	$(ASSEMBLY_COMPILER_COMMAND) $(ASSEMBLY_COMPILER_FLAGS) -out:$(ASSEMBLY) -target:$(COMPILE_TARGET) $(build_sources_embed) $(build_resources_embed) $(build_references_ref)
