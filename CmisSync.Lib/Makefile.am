ASSEMBLY = CmisSync.Lib
TARGET = library

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSONLIBS) $(DOTCMISLIBS) $(WEBLIBS)

SOURCES =  \
	ActivityListener.cs \
	ActivityListenerAggregator.cs \
	Backend.cs \
	Config.cs \
	ConfigManager.cs \
	ConfigMigration.cs \
	Exceptions.cs \
	Fetcher.cs \
	RepoBase.cs \
	RepoInfo.cs \
	Utils.cs \
	Watcher.cs \
	Wrappers.cs \
	Sync/ChangeLogStrategy.cs \
	Sync/CmisRepo.cs \
	Sync/CrawlStrategy.cs \
	Sync/SynchronizedFolder.cs \
	Cmis/CmisPermissionDeniedException.cs \
	Cmis/CmisServerNotFoundException.cs \
	Cmis/CmisUtils.cs \
	Cmis/Crypto.cs \
	Cmis/Database.cs \
	Cmis/MimeType.cs

EXTRA_DIST = CmisSync.Lib.csproj StaticDefines.cs

include $(top_srcdir)/build/build.mk
