## Building on Linux distributions

To run CmisSync, you'll need the following packages:

```
curl
gtk-sharp2
mono-core >= 2.8
notify-sharp
webkit-sharp
```

**Note:** These packages may not overlap with the packages required to perform a build, so please make sure that at least the above packages are installed.

Optional packages:

```
gvfs (to change file/folder icons)
libappindicator (for Ubuntu integration)
```

### Installing build requirements

You can use one of the commands listed below for the most used Linux distributions:


#### Ubuntu

```bash
$ sudo apt-get install libappindicator0.1-cil-dev gtk-sharp2 mono-runtime mono-devel \
  monodevelop libndesk-dbus1.0-cil-dev nant libnotify-cil-dev libgtk2.0-cil-dev mono-mcs \
  mono-gmcs libwebkit-cil-dev intltool libtool libndesk-dbus-glib1.0-cil-dev liblog4net-cil-dev libnewtonsoft-json-cil-dev
```

#### Fedora

```bash
$ sudo yum install gtk-sharp2-devel mono-core mono-devel monodevelop \
  ndesk-dbus-devel ndesk-dbus-glib-devel nant \
  notify-sharp-devel webkit-sharp-devel webkitgtk-devel libtool intltool \
  desktop-file-utils
```

#### Debian

```bash
$ sudo apt-get install gtk-sharp2 mono-runtime mono-devel monodevelop \
  libndesk-dbus1.0-cil-dev nant libnotify-cil-dev libgtk2.0-cil-dev mono-mcs mono-gmcs \
  libwebkit-cil-dev intltool libtool libndesk-dbus-glib1.0-cil-dev \
  desktop-file-utils
```

#### openSUSE

```bash
$ sudo zypper install gtk-sharp2 mono-core mono-devel monodevelop \
  ndesk-dbus-glib-devel nant desktop-file-utils \
  notify-sharp-devel webkit-sharp libwebkitgtk-devel libtool intltool make log4net
```

### Starting the build

You can build and install CmisSync like this:

Download DotCMIS.dll from http://chemistry.apache.org/dotnet/dotcmis.html and copy it to Extras/

```bash
$ make -f Makefile.am
$ ./configure --with-dotcmis=Extras/DotCMIS.dll
$ make
$ sudo make install
$ sudo cp Extras/DotCMIS.dll /usr/local/lib/cmissync/
```

### Resetting CmisSync settings

```
rm -rf ~/CmisSync
rm -rf ~/.config/cmissync
```

### Uninstalling

```
sudo make uninstall
```

