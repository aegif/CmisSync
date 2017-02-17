SUBDIRS = Pixmaps

ASSEMBLY = CmisSync
TARGET = exe

ASSEMBLY_INFO_SOURCE = Defines.cs

LIBS = $(REF_CMISSYNC) $(LOG4NET_LIBS) $(NOTIFY_SHARP_LIBS) $(LIB_CMISAUTH) $(DOTCMISLIBS)

SOURCES =  \
	../AboutController.cs \
	../SyncSizeController.cs \
	../CertPolicyHandler.cs \
	../ControllerBase.cs \
	../EditController.cs \
	../Program.cs \
	../SetupController.cs \
	../StatusIconController.cs \
	About.cs \
	SyncSize.cs \
	CertPolicyWindow.cs \
	CmisTree/CmisTreeStore.cs \
	CmisTree/LoadingStatusModel.cs \
	CmisTree/NodeLoader.cs \
	CmisTree/NodeModel.cs \
	CmisTree/NodeModelUtils.cs \
	CmisTree/StatusCellRenderer.cs \
	Controller.cs \
	Resources.Designer.cs \
	Setup.cs \
	SetupWindow.cs \
	Spinner.cs \
	StatusIcon.cs \
	UserNotificationListenerLinux.cs \
	GUI.cs \
	UIHelpers.cs \
	Utils.cs

RESOURCES = $(RESOURCES_RESOURCES)

RESOURCES_RESX = $(wildcard $(srcdir)/../Properties/*.resx)
RESOURCES_RESOURCES = $(addprefix CmisSync.,$(patsubst %.resx,%.resources,$(notdir $(RESOURCES_RESX))))

CmisSync.%.resources: $(srcdir)/../Properties/%.resx
	resgen2 $< $@

bin_SCRIPTS = cmissync

Applicationsdir = $(datadir)/applications
dist_Applications_DATA = cmissync.desktop 

EXTRA_DIST = README.md

include $(top_srcdir)/build/build.mk

CLEANFILES += $(RESOURCES_RESOURCES)
