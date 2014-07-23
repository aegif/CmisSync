ASSEMBLY = CmisSync.Auth
TARGET = library

ASSEMBLY_INFO_SOURCE = Properties/AssemblyInfo.cs

LIBS = $(LOG4NET_LIBS) $(DATALIBS) $(JSON_LIBS) $(DOTCMISLIBS) $(WEBLIBS) $(MONOPOSIX)

SOURCES =  \
	Auth.cs \
	Credentials.cs \
	Crypto.cs \
	Password.cs

EXTRA_DIST=
##EXTRA_DIST = CmisSync.Lib.csproj StaticDefines.cs

include $(top_srcdir)/build/build.mk
