ASSEMBLY = CmisSync.Lib
TARGET = library

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSON_LIBS) $(DOTCMISLIBS) $(WEBLIBS) $(MONOPOSIX)

SOURCES =  \
        IActivityListener.cs \
        ActivityListenerAggregator.cs \
        Backend.cs \
        Cmis/CmisUtils.cs \
        Cmis/Database.cs \
        Cmis/MimeType.cs \
        Cmis/PermissionDeniedException.cs \
        Cmis/ServerNotFoundException.cs \
        Cmis/BaseException.cs \
        Config.cs \
        ConfigManager.cs \
        ConfigMigration.cs \
        RepoBase.cs \
        RepoInfo.cs \
        ../CmisSync.Auth/Auth.cs \
        ../CmisSync.Auth/Password.cs \
        ../CmisSync.Auth/Crypto.cs \
        UserNotificationListener.cs \
        FolderLock.cs \
        Sync/ChangeLogStrategy.cs \
        Sync/CmisRepo.cs \
        Sync/CrawlStrategy.cs \
        Sync/SynchronizedFolder.cs \
        Sync/WatcherStrategy.cs \
        Utils.cs \
        Watcher.cs

EXTRA_DIST = CmisSync.Lib.csproj StaticDefines.cs

include $(top_srcdir)/build/build.mk
