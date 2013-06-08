SUBDIRS = Pixmaps

ASSEMBLY = CmisSync
TARGET = exe

LIBS = $(REF_CMISSYNC) $(NOTIFY_SHARP_LIBS) $(WEBKIT_SHARP_LIBS) $(LOG4NET_LIBS)

SOURCES =  \
	../Program.cs \
	../AboutController.cs \
	../BubblesController.cs \
	../ControllerBase.cs \
	../Extensions.cs \
	../SetupController.cs \
	../StatusIconController.cs \
	About.cs \
	Bubbles.cs \
	Controller.cs \
	Setup.cs \
	SetupWindow.cs \
	Spinner.cs \
	StatusIcon.cs \
	UI.cs \
	UIHelpers.cs

include $(top_srcdir)/build/build.mk

bin_SCRIPTS = cmissync

Applicationsdir = $(datadir)/applications
dist_Applications_DATA = cmissync.desktop \
	cmissync-invite-opener.desktop
