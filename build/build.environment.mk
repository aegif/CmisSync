# Initializers
MONO_BASE_PATH = 

# Install Paths
DEFAULT_INSTALL_DIR = $(pkglibdir)

DIR_BIN = $(top_builddir)/bin

# External libraries to link against, generated from configure
LIBS_SYSTEM = -r:System
LIBS_MONO_POSIX = -r:Mono.Posix
LIBS_WINDOWS_FORMS = -r:System.Windows.Forms

LIBS_GLIB = $(GLIBSHARP_LIBS)
LIBS_GNOME = $(GNOME_SHARP_LIBS)
LIBS_DBUS = $(NDESK_DBUS_LIBS) $(NDESK_DBUS_GLIB_LIBS)
LIBS_DBUS_NO_GLIB = $(NDESK_DBUS_LIBS)
LIBS_APP_INDICATOR = $(APP_INDICATOR_LIBS)

REF_CMISSYNCLIB = $(LIBS_SYSTEM) $(LIBS_MONO_POSIX)
LIBS_CMISSYNCLIB = -r:$(DIR_BIN)/CmisSync.Lib.dll
LIBS_CMISSYNCLIB_DEPS = $(REF_CMISSYNCLIB) $(LIBS_CMISSYNCLIB)

LIB_CMISAUTH=-r:$(DIR_BIN)/CmisSync.Auth.dll
REF_CMISSYNC = $(LIBS_DBUS) $(GTKSHARP_LIBS) $(LIBS_CMISSYNCLIB_DEPS) $(LIBS_APP_INDICATOR) $(LIBS_WINDOWS_FORMS)

# Cute hack to replace a space with something
colon:= :
empty:=
space:= $(empty) $(empty)

# Build path to allow running uninstalled
RUN_PATH = $(subst $(space),$(colon), $(MONO_BASE_PATH))
