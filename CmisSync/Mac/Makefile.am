SUBDIRS = Pixmaps

ASSEMBLY = CmisSync
TARGET = exe

ASSEMBLY_INFO_SOURCE =  \
	../AboutController.cs \
	../CertPolicyHandler.cs \
	../ControllerBase.cs \
	../Program.cs \
	../Properties/Resources.Designer.cs \
	../SetupController.cs \
	../StatusIconController.cs \
	About.cs \
	AppDelegate.cs \
	Badger.cs \
	Controller.cs \
	MacWatcher.cs \
	MainMenu.xib.designer.cs \
	Setup.cs \
	SetupWindow.cs \
	StatusIcon.cs \
	UI.cs

LIBS = $(REF_CMISSYNC) $(LOG4NET_LIBS) $(NOTIFY_SHARP_LIBS)

SOURCES =  \
	../AboutController.cs \
	../CertPolicyHandler.cs \
	../ControllerBase.cs \
	../Program.cs \
	../Properties/Resources.Designer.cs \
	../SetupController.cs \
	../StatusIconController.cs \
	About.cs \
	AboutDelegate.cs \
	Badger.cs \
	Setup.cs \
	SetupWindow.cs \
	Spinner.cs \
	StatusIcon.cs \
	UI.cs \
	UIHelpers.cs \
	Utils.cs

RESOURCES = $(RESOURCES_RESOURCES)

RESOURCES_RESX = $(wildcard $(srcdir)/../Properties/*.resx)
RESOURCES_RESOURCES = $(addprefix CmisSync.,$(patsubst %.resx,%.resources,$(notdir $(RESOURCES_RESX))))

CmisSync.%.resources: $(srcdir)/../Properties/%.resx
	resgen2 $< $@

EXTRA_DIST = README.md

include $(top_srcdir)/build/build.mk

CLEANFILES += $(RESOURCES_RESOURCES)
