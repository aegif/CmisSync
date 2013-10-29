ASSEMBLY = CmisSync.Lib
TARGET = library

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSON_LIBS) $(DOTCMISLIBS) $(WEBLIBS) $(MONOPOSIX)

SOURCES =  \
	ActivityListenerAggregator.cs \
	Backend.cs \
	ChunkedStream.cs \
	Cmis/CmisPermissionDeniedException.cs \
	Cmis/CmisServerNotFoundException.cs \
	Cmis/CmisUtils.cs \
	Cmis/Crypto.cs \
	Cmis/Database.cs \
	Cmis/MimeType.cs \
	Config.cs \
	ConfigManager.cs \
	ConfigMigration.cs \
	Events/ISyncEvent.cs \
	Events/FSEvent.cs \
	Events/ISyncEventHandler.cs \
	Events/DebugLoggingHandler.cs \
	Events/SyncEventManager.cs \
	Events/SyncEventQueue.cs \
	Fetcher.cs \
	IActivityListener.cs \
	LoggingStream.cs \
	RepoBase.cs \
	RepoInfo.cs \
	Sync/ChangeLogStrategy.cs \
	Sync/CmisRepo.cs \
	Sync/CrawlStrategy.cs \
	Sync/SynchronizedFolder.cs \
	Sync/WatcherStrategy.cs \
	Utils.cs \
	Watcher.cs

EXTRA_DIST = CmisSync.Lib.csproj StaticDefines.cs

include $(top_srcdir)/build/build.mk
