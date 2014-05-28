#!/bin/bash

# Thanks to Ludvig A Norin
# http://stackoverflow.com/questions/96882/how-do-i-create-a-nice-looking-dmg-for-mac-os-x-using-command-line-tools 


if [ $# -ne 1 ]; then
	echo "Please pass the source directory as argument"
	exit
fi

source=$1
applicationName="CmisSync"
backgroundPictureName="dmgBackground.png"
title=$applicationName
finalDMGName=$applicationName".dmg"
volumeIcon="dmgVolumeIcon.icns"

echo "Application Name: $applicationName"
echo "Background Icon: $backgroundPictureName"
echo "Volume Name: /Volumes/$title"
echo "Final DMG Name: $finalDMGName"
echo "Volume Icon: $volumeIcon"

size=`du -chsk "$source" | tail -1 | sed 's/\s//g' | sed 's/total//g' | sed "s/[ \t	]*//g"`
echo Temporary DMG file size:"$size"0k

#Remove old temp dmg
rm -f pack.temp.dmg

#Create a new temp dmg file
hdiutil create -srcfolder "$source" -volname "CmisSync" -fs HFS+ -fsargs "-c c=64,a=16,e=16" -format UDRW -size "$size"0k pack.temp.dmg
device=$(hdiutil attach -readwrite -noverify -noautoopen "pack.temp.dmg" | \
         egrep '^/dev/' | sed 1q | awk '{print $1}')
# Wait for mount
sleep 10

echo "$device"

mkdir "/Volumes/$title/.background"

cp "../CmisSync/Mac/Pixmaps/dmgBackground.png" "/Volumes/$title/.background/"
#cp "../CmisSync/Mac/Pixmaps/$volumeIcon" "/Volumes/$title/.VolumeIcon.icns"
#SetFile -c icnC "/Volumes/$title/.VolumeIcon.icns"
SetFile -a C "/Volumes/$title"

echo '
   tell application "Finder"
     tell disk "'${title}'"
           open
           set current view of container window to icon view
           set toolbar visible of container window to false
           set statusbar visible of container window to false
           set the bounds of container window to {400, 100, 950, 430}
           set theViewOptions to the icon view options of container window
           set arrangement of theViewOptions to not arranged
           set icon size of theViewOptions to 90
           set background picture of theViewOptions to file ".background:'${backgroundPictureName}'"
           make new alias file at container window to POSIX file "/Applications" with properties {name:"Applications"}
           set position of item "'${applicationName}'" of container window to {170, 170}
           set position of item "Applications" of container window to {370, 170}
           close
           open
           update without registering applications
           delay 5
     end tell
   end tell
' | osascript

# Change permissions of the temp volume
chmod -Rf go-w /Volumes/"${title}"
# write changes to temp dmg
sync
# umount temp dmg
hdiutil detach ${device}
# remove old final dmg file
rm -f "../bin/${finalDMGName}"
# Pack the temp dmg to final dmg
hdiutil convert "pack.temp.dmg" -format UDZO -imagekey zlib-level=9 -o "../bin/${finalDMGName}"
rm -f pack.temp.dmg
