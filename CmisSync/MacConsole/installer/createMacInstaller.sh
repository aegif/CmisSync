#!/bin/sh
 
if [ $# -ne 1 ]; then
echo "usage : createMacInstaller.sh VERSION"
exit
fi

cd `dirname $0`

FILES="../../../CmisSync.Console/bin/Mac Debug/"
DISTXML="./Distribution.xml"
TARGET="/usr/local/lib/cmissync.console"
IDENTIFIER="jp.aegif.cmissync.console"
RESOURCES="./Resources"
SCRIPTS="./Scripts"
PKGNAME="temp.pkg"
OUTPUT="CmisSyncConsole.pkg"
VERSION=$1

cp ./cmissync_once "$FILES"

pkgbuild  \
--root "$FILES" \
--script "$SCRIPTS" \
--identifier "$IDENTIFIER" \
--install-location "$TARGET" \
--version $VERSION \
"$PKGNAME"

productbuild  \
--distribution "$DISTXML" \
--package-path . \
--resources "$RESOURCES" \
"$OUTPUT"

rm "$PKGNAME"