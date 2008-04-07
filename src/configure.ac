dnl Warning: This is an automatically generated file, do not edit!
dnl Process this file with autoconf to produce a configure script.
AC_PREREQ([2.54])
AC_INIT([MonoTorrent], [0.3])
AM_INIT_AUTOMAKE([foreign])

dnl pkg-config
AC_PATH_PROG(PKG_CONFIG, pkg-config, no)
if test "x$PKG_CONFIG" = "xno"; then
        AC_MSG_ERROR([You need to install pkg-config])
fi

SHAMROCK_EXPAND_LIBDIR
SHAMROCK_EXPAND_BINDIR
SHAMROCK_EXPAND_DATADIR

AC_PROG_INSTALL

AC_PATH_PROG(GMCS, gmcs, no)
if test "x$GMCS" = "xno"; then
        AC_MSG_ERROR([gmcs Not found])
fi


AC_ARG_ENABLE(debug_any_cpu,
	AC_HELP_STRING([--enable-debug_any_cpu],
		[Use 'DEBUG_ANY_CPU' Configuration [default=YES]]),
		enable_debug_any_cpu=yes, enable_debug_any_cpu=no)
AM_CONDITIONAL(ENABLE_DEBUG_ANY_CPU, test x$enable_debug_any_cpu = xyes)
if test "x$enable_debug_any_cpu" = "xyes" ; then
	CONFIG_REQUESTED="yes"
fi
AC_ARG_ENABLE(release_any_cpu,
	AC_HELP_STRING([--enable-release_any_cpu],
		[Use 'RELEASE_ANY_CPU' Configuration [default=NO]]),
		enable_release_any_cpu=yes, enable_release_any_cpu=no)
AM_CONDITIONAL(ENABLE_RELEASE_ANY_CPU, test x$enable_release_any_cpu = xyes)
if test "x$enable_release_any_cpu" = "xyes" ; then
	CONFIG_REQUESTED="yes"
fi
if test -z "$CONFIG_REQUESTED" ; then
	AM_CONDITIONAL(ENABLE_DEBUG_ANY_CPU, true)
	enable_debug_any_cpu=yes
fi


dnl package checks, common for all configs

dnl package checks, per config


AC_CONFIG_FILES([
Makefile
SampleClient/Makefile
TrackerApp/Makefile
SampleClient/sampleclient
TrackerApp/trackerapp
MonoTorrent/Makefile
MonoTorrent/monotorrent.pc

])

AC_OUTPUT([
	AssemblyInfo.cs.in
])
