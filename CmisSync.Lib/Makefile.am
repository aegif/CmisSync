ASSEMBLY = CmisSync.Lib
TARGET = library

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSON_LIBS) $(DOTCMISLIBS) $(WEBLIBS) $(MONOPOSIX) $(LIB_CMISAUTH)

SOURCES =  \
	ActivityListenerAggregator.cs \
	Backend.cs \
	Cmis/BaseException.cs \
	Cmis/CmisProfile.cs \
	Cmis/PermissionDeniedException.cs \
	Cmis/CmisUtils.cs \
	Cmis/ServerNotFoundException.cs \
	Cmis/MimeType.cs \
	Config.cs \
	ConfigManager.cs \
	ConfigMigration.cs \
	Database/Database.cs \
	Database/DatabaseMigration.cs \
	Database/DatabaseMigrationBase.cs \
	Database/DatabaseMigrationToVersion3.cs \
	IActivityListener.cs \
	FolderLock.cs \
  ##	Properties/AssemblyInfo.cs \
	RepoBase.cs \
	RepoInfo.cs \
	SyncItem.cs \
	Sync/ChangeLogThenCrawlStrategy.cs \
	Sync/CmisRepo.cs \
	Sync/CrawlStrategy.cs \
	Sync/LocalComparisonStrategy.cs \
	Sync/SynchronizedFolder.cs \
	Sync/WatcherStrategy.cs \
	UserNotificationListener.cs \
	Utils.cs \
	Watcher.cs

EXTRA_DIST = CmisSync.Lib.csproj StaticDefines.cs

include $(top_srcdir)/build/build.mk
