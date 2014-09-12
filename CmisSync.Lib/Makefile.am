ASSEMBLY = CmisSync.Lib
TARGET = library

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSON_LIBS) $(DOTCMISLIBS) $(WEBLIBS) $(MONOPOSIX) $(LIB_CMISAUTH)

SOURCES =  \
	ActivityListenerAggregator.cs \
	Backend.cs \
	ChunkedStream.cs \
	Cmis/BaseException.cs \
	Cmis/PermissionDeniedException.cs \
	Cmis/CmisUtils.cs \
	Cmis/Database.cs \
	Cmis/ServerNotFoundException.cs \
	Cmis/MimeType.cs \
	Config.cs \
	ConfigManager.cs \
	ConfigMigration.cs \
	DatabaseMigration.cs \
	Events/ActiveActivitiesManager.cs \
	Events/ConfigChangedEvent.cs \
	Events/DebugLoggingHandler.cs \
	Events/FileConflictEvent.cs \
	Events/FileTransmissionEvent.cs \
	Events/FSDeletionHandler.cs \
	Events/FSEvent.cs \
	Events/GenericSyncEventHandler.cs \
	Events/ISyncEvent.cs \
	Events/SyncEventHandler.cs \
	Events/SyncEventManager.cs \
	Events/SyncEventQueue.cs \
	IActivityListener.cs \
	FolderLock.cs \
  ##	Properties/AssemblyInfo.cs \
	RepoBase.cs \
	RepoInfo.cs \
	SyncItem.cs \
	Sync/ChangeLogStrategy.cs \
	Sync/CmisRepo.cs \
	Sync/CrawlStrategy.cs \
	Sync/SynchronizedFolder.cs \
	Sync/WatcherStrategy.cs \
	UserNotificationListener.cs \
	Utils.cs \
	Watcher.cs

EXTRA_DIST = CmisSync.Lib.csproj StaticDefines.cs

include $(top_srcdir)/build/build.mk
