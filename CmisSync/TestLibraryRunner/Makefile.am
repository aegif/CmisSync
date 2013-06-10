ASSEMBLY = TestLibraryRunner
TARGET = exe

ASSEMBLY_INFO_SOURCE = Properties/AssemblyInfo.cs

LIBS = $(JSONLIBS) -r:$(DIR_BIN)/TestLibrary.dll \
	   -r:$(top_srcdir)/Extras/nunit.framework.dll

SOURCES = Program.cs

EXTRA_DIST = TestLibraryRunner.csproj app.config

include $(top_srcdir)/build/build.mk
