ASSEMBLY = CmisSync.Lib
TARGET = library

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSONLIBS) $(DOTCMISLIBS)

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


install-data-hook:
	for ASM in $(EXTRA_BUNDLE); do \
		$(INSTALL) -m 0755 $$ASM $(DESTDIR)$(moduledir); \
	done;

uninstall-hook:
	for ASM in $(EXTRA_BUNDLE); do \
		rm -f $(DESTDIR)$(moduledir)/`basename $$ASM`; \
	done;

include $(top_srcdir)/build/build.mk
