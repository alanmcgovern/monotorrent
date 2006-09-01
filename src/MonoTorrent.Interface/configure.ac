AC_INIT([monotorrent], 0.1)

COPYRIGHT="Copyright (c) 2006 by Piotr Wolny <gildur@gmail.com>"
AC_COPYRIGHT($COPYRIGHT)
AC_SUBST(COPYRIGHT)

AM_INIT_AUTOMAKE
AM_MAINTAINER_MODE

AC_PROG_INTLTOOL
AC_PROG_INSTALL

AC_PATH_PROG(PKG_CONFIG, pkg-config, no)
if test "x$PKG_CONFIG" = "xno"; then
    AC_MSG_ERROR([You need to install pkg-config])
fi

MONO_REQUIRED_VERSION=1.1
PKG_CHECK_MODULES(MONO, mono >= $MONO_REQUIRED_VERSION)

MONODIR=`$PKG_CONFIG --variable=exec_prefix mono`
MONOBIN="$MONODIR/bin"
MONO="$MONOBIN/mono"
MCS="$MONOBIN/gmcs"

AC_SUBST(MONO)
AC_SUBST(MCS)

GTKSHARP_REQUIRED_VERSION=2.8
PKG_CHECK_MODULES(GTKSHARP, gtk-sharp-2.0 >= $GTKSHARP_REQUIRED_VERSION)

GCONFSHARP_REQUIRED_VERSION=2.8
PKG_CHECK_MODULES(GCONFSHARP, gconf-sharp-2.0 >= $GCONFSHARP_REQUIRED_VERSION)

ALL_LINGUAS="pl"
AC_SUBST(ALL_LINGUAS)
GETTEXT_PACKAGE=$PACKAGE
AC_SUBST(GETTEXT_PACKAGE)
AC_DEFINE_UNQUOTED(GETTEXT_PACKAGE, "$GETTEXT_PACKAGE", [Gettext Package])
AM_GLIB_GNU_GETTEXT

AC_OUTPUT([
    Makefile
    res/Makefile
    src/Makefile
    src/AssemblyInfo.cs
    po/Makefile.in
])

echo ""
echo "Configuration summary:"
echo ""
echo "    * Installation prefix = $prefix"
echo "    * C# compiler = $MCS"
echo ""
