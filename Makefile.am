basedirs = build CmisSync.Lib CmisSync.Lib/Cmis

SUBDIRS = $(basedirs) $(GUISUBDIRS)
DIST_SUBDIRS = $(basedirs) CmisSync

EXTRA_DIST = \
	NEWS \
        README.md \
	legal/LICENSE \
	legal/TRADEMARK \
	legal/AUTHORS

DISTCLEANFILES = \
	intltool-extract        \
	intltool-merge          \
	intltool-update		\
	gnome-doc-utils.make

clean-local:
	rm -rf $(top_builddir)/bin
