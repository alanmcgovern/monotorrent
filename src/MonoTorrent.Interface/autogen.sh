#!/bin/sh

PROJECT=monotorrent
CONFIGURE=configure.ac

ACLOCAL=aclocal
AUTOCONF=autoconf
AUTOMAKE=automake-1.9
INTLTOOLIZE=intltoolize

SRCDIR=`dirname $0`
test -z "$SRCDIR" && SRCDIR=.
cd "$SRCDIR"

DIE=0

($AUTOCONF --version) < /dev/null > /dev/null 2>&1 || {
    echo
    echo "You must have autoconf installed to compile $PROJECT."
    echo
    DIE=1
}

($AUTOMAKE --version) < /dev/null > /dev/null 2>&1 || {
    echo
    echo "You must have automake installed to compile $PROJECT."
    echo
    DIE=1
}

($ACLOCAL --version) < /dev/null > /dev/null 2>&1 || {
    echo
    echo "You must have aclocal installed to compile $PROJECT."
    echo
    DIE=1
}

($INTLTOOLIZE --version) < /dev/null > /dev/null 2>&1 || {
    echo
    echo "You must have intltoolize installed to compile $PROJECT."
    echo
    DIE=1
}

if test "$DIE" -eq 1; then
    exit 1
fi
                                                                                
if test -z "$*"; then
    echo "I am going to run ./configure with no arguments - if you wish "
    echo "to pass any to it, please specify them on the $0 command line."
fi

GETTEXTIZECOMMAND="glib-gettextize --force --copy"
echo "Running $GETTEXTIZECOMMAND ..."
$GETTEXTIZECOMMAND ||
    { echo "$GETTEXTIZECOMMAND failed."; exit 1; }

INTLTOOLIZECOMMAND="$INTLTOOLIZE --force --copy --automake"
echo "Running $INTLTOOLIZECOMMAND ..."
$INTLTOOLIZECOMMAND ||
    { echo "$INTLTOOLIZECOMMAND failed."; exit 1; }

#
# Patch the resulting Makefile.in.in to install locale stuff into 
# the monotorrent directory.
#
sed 's,^itlocaledir =.*,itlocaledir = @libdir@/monotorrent/locale,'< po/Makefile.in.in > po/tmp && mv po/tmp po/Makefile.in.in 

echo "Running $ACLOCAL ..."
$ACLOCAL ||
    { echo "$ACLOCAL failed."; exit 1; }

AUTOMAKECOMMAND="$AUTOMAKE --add-missing --gnu"
echo "Running $AUTOMAKECOMMAND ..."
$AUTOMAKECOMMAND ||
    { echo "$AUTOMAKECOMMAND failed."; exit 1; }

echo "Running $AUTOCONF ..."
$AUTOCONF ||
    { echo "$AUTOCONF failed."; exit 1; }

CONFIGURECOMMAND="./configure --enable-maintainer-mode $@"
echo "Running $CONFIGURECOMMAND ..."
$CONFIGURECOMMAND
