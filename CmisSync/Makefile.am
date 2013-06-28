SUBDIRS = Common TestLibrary TestLibraryRunner Linux

EXTRA_DIST =  \
	Program.cs \
	AboutController.cs \
	BubblesController.cs \
	ControllerBase.cs \
	Extensions.cs \
	SetupController.cs \
	StatusIconController.cs \
	CertPolicyHandler.cs \
	Properties

install-data-hook:
	test -f $(datadir)/applications/defaults.list && \
	test -z $(DESTDIR) && update-desktop-database $(datadir)/applications || :
