ASSEMBLY = TestLibrary
TARGET = library

ASSEMBLY_INFO_SOURCE = Properties/AssemblyInfo.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSONLIBS) $(DOTCMISLIBS) \
	   $(LIBS_CMISSYNCLIB_DEPS) \
	   -r:$(top_srcdir)/Extras/nunit.framework.dll \
	   -r:$(srcdir)/lib/Moq.dll \
	   -r:$(srcdir)/lib/xunit.dll \
	   -r:$(srcdir)/lib/xunit.extensions.dll

SOURCES =  \
	CmisSyncTests.cs \
	LocalFilesystemActivityGenerator.cs

EXTRA_DIST = TestLibrary.csproj \
	lib/Moq.dll \
	lib/xunit.dll \
	lib/xunit.extensions.dll

include $(top_srcdir)/build/build.mk
