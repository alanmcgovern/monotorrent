(cd BuildScripts/; sh AutoTools.sh)
aclocal
automake -a
autoconf
./configure $*
