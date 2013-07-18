ASSEMBLY = TestLibraryRunner
TARGET = exe

ASSEMBLY_INFO_SOURCE = Properties/AssemblyInfo.cs

LIBS = $(LOG4NET_LIBS) $(JSON_LIBS) -r:$(DIR_BIN)/TestLibrary.dll \
	   $(LIBS_CMISSYNCLIB_DEPS) \
	   $(NUNIT_LIBS)

SOURCES = Program.cs

EXTRA_DIST = TestLibraryRunner.csproj app.config

include $(top_srcdir)/build/build.mk
