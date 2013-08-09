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

PKG_CHECK_MODULES(NUNIT, nunit >= 2.6.0, have_nunit=yes, have_nunit=no)
if test "x$have_nunit" = "xno" ; then
    dnl On som distro's the package is named mono-nunit
    PKG_CHECK_MODULES(NUNIT, mono-nunit >= 2.6.0, have_nunit=yes, have_nunit=no)
    if test "x$have_nunit" = "xno" ; then
        AC_ERROR("nunit is a required dependency: you need to install the appropriate devel package before you can compile")
    fi
fi
AC_SUBST(NUNIT_LIBS)

AC_ARG_WITH([newtonsoft-json],
            AS_HELP_STRING([--with-newtonsoft-json=PATH],[Specify path to NewtonSoft.Json.dll]),
            [newtonsoft_json_dll=${withval}],[newtonsoft_json_dll=no])
if test "x$newtonsoft_json_dll" = "xno" ; then
    PKG_CHECK_MODULES(JSON, [newtonsoft-json >= 4.5r6], have_newtonsoft_json=yes, have_newtonsoft_json=no)
    if test "x$have_newtonsoft_json" = "xno" ; then
        AC_ERROR("newtonsoft-json is a required dependency: you need to install the appropriate devel package before you can compile")
    fi
else
    if test -f ${newtonsoft_json_dll} ; then
        case ${newtonsoft_json_dll} in
            /*)
            AC_SUBST(JSON_LIBS,[-r:${newtonsoft_json_dll}])
            AC_SUBST(JSON_PATH,[${newtonsoft_json_dll}])
            ;;
            *)
            AC_SUBST(JSON_LIBS,[[-r:\$\(top_srcdir\)/${newtonsoft_json_dll}]])
            AC_SUBST(JSON_PATH,[[\$\(top_srcdir\)/${newtonsoft_json_dll}]])
            ;;
        esac
    else
        AC_ERROR([${newtonsoft_json_dll} not found])
    fi
fi
AC_SUBST(NUNIT_LIBS)

CMISSYNC_CHECK_NOTIFY_SHARP
CMISSYNC_CHECK_GTK_SHARP

AC_ARG_ENABLE(appindicator,
	AS_HELP_STRING([--enable-appindicator[=@<:@no/auto/yes@:>@]],[Build support for application indicators ]),
	[enable_appindicator=$enableval],
	[enable_appindicator="auto"])

AC_ARG_WITH([dotcmis],
            AS_HELP_STRING([--with-dotcmis=PATH],[Specify path to DotCMIS.dll]),
            [dotcmis_dll=${withval}],[dotcmis_dll=no])
if test "x$dotcmis_dll" = "xno" ; then
    AC_ERROR([Please specify the path to DotCMIS.dll using --with-dotcmis (use --help for more information.)])
fi
if test -f ${dotcmis_dll} ; then
    case ${dotcmis_dll} in
        /*)
        AC_SUBST(DOTCMISLIBS,[-r:${dotcmis_dll}])
        AC_SUBST(DOTCMISPATH,[${dotcmis_dll}])
        ;;
        *)
        AC_SUBST(DOTCMISLIBS,[[-r:\$\(top_srcdir\)/${dotcmis_dll}]])
        AC_SUBST(DOTCMISPATH,[[\$\(top_srcdir\)/${dotcmis_dll}]])
        ;;
    esac
else
    AC_ERROR([${dotcmis_dll} not found])
fi

TEST_URL="https://devel.dataspace.cc/cmis/atom"
AC_ARG_WITH([test-url],
    AS_HELP_STRING([--with-test-url=URL],[Specify server URL for testing]),
    TEST_URL=${withval})
AC_SUBST(TEST_URL)

TEST_LOCALPATH="/localpath"
AC_ARG_WITH([test-localpath],
    AS_HELP_STRING([--with-test-localpath=PATH],[Specify local path for testing]),
    TEST_LOCALPATH=${withval})
AC_SUBST(TEST_LOCALPATH)

TEST_REMOTEPATH="/remotepath"
AC_ARG_WITH([test-remotepath],
    AS_HELP_STRING([--with-test-remotepath=PATH],[Specify remote path for testing]),
    TEST_REMOTEPATH=${withval})
AC_SUBST(TEST_REMOTEPATH)

TEST_USER="testuser"
AC_ARG_WITH([test-user],
    AS_HELP_STRING([--with-test-user=PATH],[Specify username for testing]),
    TEST_USER=${withval})
AC_SUBST(TEST_USER)

TEST_PASSWORD="secret"
AC_ARG_WITH([test-password],
    AS_HELP_STRING([--with-test-password=PATH],[Specify password for testing]),
    TEST_PASSWORD=${withval})
AC_SUBST(TEST_PASSWORD)

TEST_REPOID="1094505"
AC_ARG_WITH([test-repoid],
    AS_HELP_STRING([--with-test-repoid=PATH],[Specify remote repository-ID for testing]),
    TEST_REPOID=${withval})
AC_SUBST(TEST_REPOID)

OSNAME=`uname -s`
if test "$OSNAME" = "Linux" ; then

	dnl check for notify-sharp
	PKG_CHECK_MODULES(NOTIFY_SHARP, notify-sharp, have_notify_sharp=yes, have_notify_sharp=no)
	if test "x$have_notify_sharp" = "xno" ; then
		AC_ERROR([notify-sharp is a required dependency: you need to install the appropriate devel package before you can compile])
	fi
	AC_SUBST(NOTIFY_SHARP_LIBS)

    dnl check for log4net
	PKG_CHECK_MODULES(LOG4NET, log4net, have_log4net=yes, have_log4net=no)
	if test "x$have_log4net" = "xno" ; then
		AC_ERROR([log4net is a required dependency: you need to install the appropriate devel package before you can compile])
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
    AC_SUBST(WEBLIBS,[-r:System.Web.dll])
    AC_SUBST(MONOPOSIX,[-r:Mono.Posix.dll])

    AC_SUBST(BUILD_DEFINES,"${BUILD_DEFINES}")
	GUISUBDIRS=CmisSync
    enable_gtkui=yes
else
	GUISUBDIRS=
    enable_gtkui=no
fi

AC_SUBST([GUISUBDIRS])

AC_OUTPUT([
build/cmissync.spec
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
CmisSync/TestLibrary/Makefile
CmisSync/TestLibrary/test-servers.json
CmisSync/TestLibraryRunner/Makefile
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
