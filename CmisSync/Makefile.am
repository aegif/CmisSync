SUBDIRS = Linux Common TestLibrary TestLibraryRunner

ASSEMBLY = CmisSync
TARGET = exe

SOURCES =  \
	Program.cs \
	AboutController.cs \
	BubblesController.cs \
	ControllerBase.cs \
	Extensions.cs \
	SetupController.cs \
	StatusIconController.cs

install-data-hook:
	test -f $(datadir)/applications/defaults.list && \
	test -z $(DESTDIR) && update-desktop-database $(datadir)/applications || :

EXTRA_DIST = Properties

include $(top_srcdir)/build/build.mk
