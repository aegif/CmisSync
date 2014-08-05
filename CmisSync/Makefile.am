
if HAVE_MAC_OS
  SUBDIRS = Common Mac TestLibrary TestLibraryRunner
else
 ## SUBDIRS = Common Linux TestLibrary TestLibraryRunner
  SUBDIRS = Common Linux
endif

EXTRA_DIST =  \
	Program.cs \
	AboutController.cs \
	BubblesController.cs \
	ControllerBase.cs \
	EditController.cs \
	SetupController.cs \
	StatusIconController.cs \
	CertPolicyHandler.cs \
	Properties

install-data-hook:
	test -f $(datadir)/applications/defaults.list && \
	test -z $(DESTDIR) && update-desktop-database $(datadir)/applications || :

MAINTAINERCLEAN_FILES = Makefile.in
