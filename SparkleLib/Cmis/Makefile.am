ASSEMBLY = SparkleLib.Cmis
TARGET = library

ASSEMBLY_INFO_SOURCE = ../Defines.cs

LINK = -r:$(DIR_BIN)/SparkleLib.dll -r:lib/DotCMIS.dll -r:lib/Newtonsoft.Json.dll -r:lib/System.Data.SQLite.dll

SOURCES =  \
	CmisCrypto.cs \
	CmisDatabase.cs \
	CmisDirectory.cs \
	CmisDirectoryCrawlMethod.cs \
	CmisPermissionDeniedException.cs \
	CmisServerNotFoundException.cs \
	CmisUtils.cs \
	MimeType.cs \
	SparkleCmis.cs \
	SparkleFetcherCmis.cs \
	SparkleRepoCmis.cs
	


install-data-hook:
	for ASM in $(EXTRA_BUNDLE); do \
		$(INSTALL) -m 0755 $$ASM $(DESTDIR)$(moduledir); \
	done;

uninstall-hook:
	for ASM in $(EXTRA_BUNDLE); do \
		rm -f $(DESTDIR)$(moduledir)/`basename $$ASM`; \
	done;

include $(top_srcdir)/build/build.mk
