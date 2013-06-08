SUBDIRS = Linux \
	Common

ASSEMBLY = CmisSync
TARGET = exe

LIBS = $(REF_CMISSYNC) $(NOTIFY_SHARP_LIBS) $(WEBKIT_SHARP_LIBS)

SOURCES =  \
	Program.cs \
	AboutController.cs \
	BubblesController.cs \
	ControllerBase.cs \
	Extensions.cs \
	SetupController.cs \
	StatusIconController.cs

include $(top_srcdir)/build/build.mk
	
install-data-hook:
	test -f $(datadir)/applications/defaults.list && \
	test -z $(DESTDIR) && update-desktop-database $(datadir)/applications || :
