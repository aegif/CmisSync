dnl Process this file with autoconf to produce a configure script.
m4_define([cmissync_version], [0.9.1])

AC_PREREQ([2.54])
AC_INIT([CmisSync], cmissync_version)
AC_CONFIG_SRCDIR([CmisSync/Program.cs])
AM_INIT_AUTOMAKE([foreign dist-bzip2 dist-zip no-dist-gzip])
AM_MAINTAINER_MODE

dnl Export Version Info
AC_SUBST([ASM_VERSION], [cmissync_version])

dnl pkg-config
AC_PATH_PROG(PKG_CONFIG, pkg-config, no)
if test "x$PKG_CONFIG" = "xno"; then
        AC_MSG_ERROR([You need to install pkg-config])
fi

SHAMROCK_EXPAND_LIBDIR
SHAMROCK_EXPAND_BINDIR
SHAMROCK_EXPAND_DATADIR

AC_PROG_INSTALL

dnl Mono and gmcs
SHAMROCK_CHECK_MONO_MODULE(2.8)
SHAMROCK_FIND_MONO_RUNTIME
    
PKG_CHECK_EXISTS([mono >= 2.8],
[SHAMROCK_FIND_MONO_4_0_COMPILER
SHAMROCK_CHECK_MONO_4_0_GAC_ASSEMBLIES([
    System
    System.Security
    System.Windows.Forms
    Mono.Posix
])]
)

CMISSYNC_CHECK_NOTIFY_SHARP

AC_ARG_ENABLE(debug,
	AC_HELP_STRING([--enable-debug],
		[Use 'DEBUG' Configuration [default=YES]]),
		enable_debug=yes, enable_debug=no)
AM_CONDITIONAL(ENABLE_DEBUG, test x$enable_debug = xyes)
if test "x$enable_debug" = "xyes" ; then
	CONFIG_REQUESTED="yes"
fi
AC_ARG_ENABLE(release,
	AC_HELP_STRING([--enable-release],
		[Use 'RELEASE' Configuration [default=NO]]),
		enable_release=yes, enable_release=no)
AM_CONDITIONAL(ENABLE_RELEASE, test x$enable_release = xyes)
if test "x$enable_release" = "xyes" ; then
	CONFIG_REQUESTED="yes"
fi
if test -z "$CONFIG_REQUESTED" ; then
	AM_CONDITIONAL(ENABLE_DEBUG, true)
	enable_debug=yes
fi


dnl package checks, common for all configs
#PKG_CHECK_MODULES([NDESK_DBUS], [ndesk-dbus-1.0])
#AC_SUBST(NDESK_DBUS_LIBS)
#PKG_CHECK_MODULES([NDESK_DBUS_GLIB], [ndesk-dbus-glib-1.0])
#AC_SUBST(NDESK_DBUS_GLIB_LIBS)


CMISSYNC_CHECK_GTK_SHARP

AC_ARG_ENABLE(appindicator,
	AS_HELP_STRING([--enable-appindicator[=@<:@no/auto/yes@:>@]],[Build support for application indicators ]),
	[enable_appindicator=$enableval],
	[enable_appindicator="auto"])

OSNAME=`uname -s`
if test "$OSNAME" = "Linux" ; then
	dnl check for webkit-sharp
	PKG_CHECK_MODULES(WEBKIT_SHARP, webkit-sharp-1.0, have_webkit_sharp=yes, have_webkit_sharp=no)
	if test "x$have_webkit_sharp" = "xno" ; then
		AC_ERROR("webkit-sharp is a required dependency: you need to install the appropriate devel package before you can compile")
	fi
	AC_SUBST(WEBKIT_SHARP_LIBS)

	dnl check for notify-sharp
	PKG_CHECK_MODULES(NOTIFY_SHARP, notify-sharp, have_notify_sharp=yes, have_notify_sharp=no)
	if test "x$have_notify_sharp" = "xno" ; then
		AC_ERROR("notify-sharp is a required dependency: you need to install the appropriate devel package before you can compile")
	fi
	AC_SUBST(NOTIFY_SHARP_LIBS)

    dnl check for log4net
	PKG_CHECK_MODULES(LOG4NET, log4net, have_log4net=yes, have_log4net=no)
	if test "x$have_log4net" = "xno" ; then
		AC_ERROR("log4net is a required dependency: you need to install the appropriate devel package before you can compile")
	fi
	AC_SUBST(LOG4NET_LIBS)

	APPINDICATOR_REQUIRED=0.0.7

	if test x$enable_appindicator = xauto ; then
			PKG_CHECK_EXISTS([appindicator-sharp-0.1 >= $APPINDICATOR_REQUIRED],
							 enable_appindicator="yes",
							 enable_appindicator="no")
	fi

	if test x$enable_appindicator = xyes ; then
			PKG_CHECK_EXISTS([appindicator-sharp-0.1 >= $APPINDICATOR_REQUIRED],,
							 AC_MSG_ERROR([appindicator-sharp-0.1 is not installed]))
			PKG_CHECK_MODULES(APP_INDICATOR,
							appindicator-sharp-0.1 >= $APPINDICATOR_REQUIRED)
			AC_SUBST(APP_INDICATOR_CFLAGS)
			AC_SUBST(APP_INDICATOR_LIBS)
            [BUILD_DEFINES="$BUILD_DEFINES -define:HAVE_APP_INDICATOR"]
	fi

    AC_SUBST(DATALIBS,[-r:System.Data.dll,Mono.Data.Sqlite.dll])
    AC_SUBST(JSONLIBS,[[-r:\$\(top_srcdir\)/Extras/Newtonsoft.Json.dll]])
    AC_SUBST(DOTCMISLIBS,[[-r:\$\(top_srcdir\)/Extras/DotCMIS.dll]])

    [BUILD_DEFINES="$BUILD_DEFINES -define:USE_MONO_SQLITE"]

    AC_SUBST(BUILD_DEFINES,"${BUILD_DEFINES}")
	GUISUBDIRS=CmisSync
    enable_gtkui=yes
else
	GUISUBDIRS=
    enable_gtkui=no
fi

AC_SUBST([GUISUBDIRS])

SHAMROCK_CHECK_NUNIT

AC_OUTPUT([
build/Makefile
build/m4/Makefile
CmisSync.Lib/Defines.cs
CmisSync.Lib/Makefile
CmisSync/Common/Makefile
CmisSync/Common/HTML/Makefile
CmisSync/Common/Plugins/Makefile
CmisSync/Common/Pixmaps/Makefile
CmisSync/Linux/Defines.cs
CmisSync/Linux/Makefile
CmisSync/Linux/Pixmaps/Makefile
CmisSync/Linux/Pixmaps/icons/Makefile
CmisSync/Linux/Pixmaps/icons/ubuntu-mono-dark/Makefile
CmisSync/Linux/Pixmaps/icons/ubuntu-mono-light/Makefile
CmisSync/Linux/cmissync
CmisSync/Makefile
Makefile
])

echo "
CmisSync ${VERSION}

Configuration:
       Prefix              : ${prefix}
       Build Gtk+ UI       : ${enable_gtkui}

"

