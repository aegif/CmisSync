ACLOCAL_AMFLAGS = -I build/m4/cmissync -I build/m4/shamrock

basedirs = build CmisSync.Auth CmisSync.Lib 

SUBDIRS = $(basedirs) $(GUISUBDIRS)
DIST_SUBDIRS = $(basedirs) CmisSync

EXTRA_DIST = \
	README.md \
	legal/LICENSE.txt \
	legal/TRADEMARK.txt \
	legal/AUTHORS.txt \
	Extras/DotCMIS.dll \
	Extras/Newtonsoft.Json.dll \
	Extras/nunit.framework.dll

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

debian-package:
	rm -rf $(top_builddir)/debian/usr
	make DESTDIR=$(abs_top_builddir)/debian install
	make VERSION=`git describe --match linux* | cut -d - -f 2` -C debian build

apple-package:
	"/Applications/Xamarin Studio.app/Contents/MacOS/mdtool" build CmisSync/Mac/CmisSync.sln -c:Release -t:Clean
	"/Applications/Xamarin Studio.app/Contents/MacOS/mdtool" build CmisSync/Mac/CmisSync.sln -c:Release
	(cd build; ./dmg-creator.sh ../bin/CmisSync.app/; cd ..)

none:
install-exec-hook:
	cp $(DOTCMISPATH) $(libdir)/cmissync/
dist-hook:
	make $(PACKAGE_TARGET)
