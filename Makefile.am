ACLOCAL_AMFLAGS = -I build/m4/cmissync -I build/m4/shamrock

basedirs = build CmisSync.Lib

SUBDIRS = $(basedirs) $(GUISUBDIRS)
DIST_SUBDIRS = $(basedirs) CmisSync

EXTRA_DIST = \
	README.md \
	legal/LICENSE.txt \
	legal/TRADEMARK.txt \
	legal/AUTHORS.txt \
	Extras/DotCMIS.dll \
	Extras/Newtonsoft.Json.dll \
	Extras/nunit.framework.dll \
	Extras/DotCMIS.dll

DISTCLEANFILES = \
	intltool-extract \
	intltool-merge \
	intltool-update \
	gnome-doc-utils.make

CLEANFILES = -r $(top_builddir)/bin

MAINTAINERCLEANFILES = \
	aclocal.m4 config.* configure install-sh ltmain* missing Makefile.in

bootstrap:
	autoreconf -f -s -i
