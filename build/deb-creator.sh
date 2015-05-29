#!/bin/sh

# Change this setting depending on whether the CmisSync files have been
# installed to either "/usr" or "/usr/local"
USR_PREFIX="/usr/local"

# Ensure that the script is being run from the correct directory (build)
# See: http://stackoverflow.com/questions/3349105/how-to-set-current-working-directory-to-the-directory-of-the-script
cd "${0%/*}"

# Path to configure.ac file and grep string used to find the version line
CAC_PATH="../configure.ac"
CAC_GREP_VERSION_LINE=m4_define\(\\[cmissync_version\\]

# Extract the version number from the relevant line in configure.ac
CAC_VER=$( grep ${CAC_GREP_VERSION_LINE} "${CAC_PATH}" | cut -d] -f2 | cut -d[ -f2 )

# Extract the major, minor and revision numbers from the version number
MAJ=$( echo "${CAC_VER}" | cut -d. -f1 )
MIN=$( echo "${CAC_VER}" | cut -d. -f2 )
REV=$( echo "${CAC_VER}" | cut -d. -f3 )

# Re-concatenate the numbers in the required format for deb packaging
CAC_VER_MOD="${MAJ}.${MIN}-${REV}"

# Give the user a chance to either confirm the version number for the deb
# or input a version number manually
echo
echo "Version number based on configure.ac: ${CAC_VER_MOD}"
read -p "Press ENTER to use ${CAC_VER_MOD} or type in a different version number: " VER
echo

# No user input means use the automatically generated version number
if [ -z "${VER}" ]; then
    VER="${CAC_VER_MOD}"
fi

# We need a directory for building the deb
# If it already exists, this suggests that the user needs to consider what to
# do with it (i.e. remove/rename) before re-running this script
DEB_DIR="cmissync_${VER}"
if [ -d "${DEB_DIR}" ]; then
    echo "ERROR: The directory ${DEB_DIR} already exists"
    echo "Perhaps the package has already been built or the version number needs updating"
    echo
    echo "Please use a different version number or remove/rename this directory before re-running this script"
    echo
    exit 1
fi

# Create the directory and cd into it
mkdir "${DEB_DIR}"
cd "${DEB_DIR}"

# Copy over the DEBIAN build directory
DEBIAN_PATH="../DEBIAN"
cp -r "${DEBIAN_PATH}" .

# Modify the DEBIAN control file to contain the version number for this package
DEBIAN_CONTROL_FILE_FOR_PACKAGE="DEBIAN/control"
sed -i "s_^Version:.*_Version: ${VER}_" "${DEBIAN_CONTROL_FILE_FOR_PACKAGE}"

# Copy over all of the CmisSync files
# rsync used with the -R switch to create directory paths accordingly
rsync -aR "${USR_PREFIX}/bin/cmissync" .
rsync -aR "${USR_PREFIX}/lib/cmissync" .
rsync -aR "${USR_PREFIX}/share/cmissync" .
rsync -aR "${USR_PREFIX}/share/applications/cmissync.desktop" .
rsync -aR "${USR_PREFIX}/share/icons/hicolor/16x16/apps/app-cmissync.png" .
rsync -aR "${USR_PREFIX}/share/icons/hicolor/22x22/apps/app-cmissync.png" .
rsync -aR "${USR_PREFIX}/share/icons/hicolor/24x24/apps/app-cmissync.png" .
rsync -aR "${USR_PREFIX}/share/icons/hicolor/24x24/status/process-syncing-"* .
rsync -aR "${USR_PREFIX}/share/icons/hicolor/32x32/apps/app-cmissync.png" .
rsync -aR "${USR_PREFIX}/share/icons/hicolor/48x48/apps/app-cmissync.png" .
rsync -aR "${USR_PREFIX}/share/icons/hicolor/256x256/apps/app-cmissync.png" .

# Remove this commented section if the ubuntu-mono-* theme icons have been removed from CmisSync
# TODO: Also modify both DEBIAN/post* files as well
# rsync -aR "${USR_PREFIX}/share/icons/ubuntu-mono-dark/status/24/process-syncing-"* .
# rsync -aR "${USR_PREFIX}/share/icons/ubuntu-mono-light/status/24/process-syncing-"* .

# Output the DEBIAN control file so it can be verified after the build
cat "${DEBIAN_CONTROL_FILE_FOR_PACKAGE}"
echo

# Return to the parent directory and build the deb package
cd ..
fakeroot dpkg-deb --build "${DEB_DIR}"
echo
echo "Built deb package is available at: ${0%/*}/${DEB_DIR}.deb"
echo

exit 0
