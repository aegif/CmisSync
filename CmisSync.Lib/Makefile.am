ASSEMBLY = SparkleLib
TARGET = library

ASSEMBLY_INFO_SOURCE = Defines.cs

SOURCES =  \
	ActivityListener.cs \
	ActivityListenerAggregator.cs \
	Defines.cs \
	SparkleBackend.cs \
	SparkleConfig.cs \
	SparkleExceptions.cs \
	SparkleExtensions.cs \
	SparkleFetcherBase.cs \
	SparkleListenerBase.cs \
	SparkleListenerFactory.cs \
	SparkleListenerTcp.cs \
	SparkleLogger.cs \
	SparkleRepoBase.cs \
	SparkleRepoInfo.cs \
	SparkleUser.cs \
	SparkleWatcher.cs \
	SparkleWrappers.cs
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
