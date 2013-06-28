SUBDIRS = Pixmaps

ASSEMBLY = CmisSync
TARGET = exe

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(REF_CMISSYNC) $(NOTIFY_SHARP_LIBS) $(WEBKIT_SHARP_LIBS) $(LOG4NET_LIBS)

SOURCES =  \
	../Program.cs \
	../AboutController.cs \
	../BubblesController.cs \
	../ControllerBase.cs \
	../Extensions.cs \
	../SetupController.cs \
	../StatusIconController.cs \
	../CertPolicyHandler.cs \
	About.cs \
	Bubbles.cs \
	Controller.cs \
	Setup.cs \
	SetupWindow.cs \
	Spinner.cs \
	StatusIcon.cs \
	UI.cs \
	UIHelpers.cs \
	Utils.cs \
	../Properties/Resources.Designer.cs

RESOURCES = $(RESOURCES_RESOURCES)

RESOURCES_RESX = $(wildcard $(srcdir)/../Properties/*.resx)
RESOURCES_RESOURCES = $(addprefix CmisSync.,$(patsubst %.resx,%.resources,$(notdir $(RESOURCES_RESX))))

CmisSync.%.resources: $(srcdir)/../Properties/%.resx
	resgen2 $< $@

bin_SCRIPTS = cmissync

Applicationsdir = $(datadir)/applications
dist_Applications_DATA = cmissync.desktop \
	cmissync-invite-opener.desktop

EXTRA_DIST = README.md

include $(top_srcdir)/build/build.mk

CLEANFILES += $(RESOURCES_RESOURCES)
