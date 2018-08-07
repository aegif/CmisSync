### 1) Install required packages

According to your Linux distributions, run one of the following commands to install required packages:

#### Ubuntu

```bash
$ sudo apt-get install libappindicator0.1-cil-dev gtk-sharp2 mono-runtime mono-devel \
  monodevelop libndesk-dbus1.0-cil-dev nant libnotify-cil-dev libgtk2.0-cil-dev mono-mcs \
  mono-gmcs libwebkit-cil-dev intltool libtool libndesk-dbus-glib1.0-cil-dev \
  liblog4net-cil-dev libnewtonsoft-json-cil-dev gvfs libmono-cil-dev mono-dmcs
```

Then make 4.5 fill the role of 4.0:
```
cd /usr/lib/mono
sudo rmdir 4.0
sudo ln -s 4.5 4.0
```

#### Debian

```bash
$ sudo apt-get install gtk-sharp2 mono-runtime mono-devel monodevelop \
  libndesk-dbus1.0-cil-dev nant libnotify-cil-dev libgtk2.0-cil-dev mono-mcs mono-gmcs \
  libwebkit-cil-dev intltool libtool libndesk-dbus-glib1.0-cil-dev \
  desktop-file-utils
```

#### RedHat/Fedora

```bash
$ sudo yum install gtk-sharp2 gtk-sharp2-devel mono-core mono-devel monodevelop \
  ndesk-dbus-devel ndesk-dbus-glib-devel nant \
  notify-sharp-devel webkit-sharp-devel webkitgtk-devel libtool intltool \
  desktop-file-utils log4net-devel
```

#### openSUSE

```bash
$ sudo zypper install gtk-sharp2 mono-core mono-devel monodevelop \
  ndesk-dbus-glib-devel nant desktop-file-utils \
  notify-sharp-devel webkit-sharp libwebkitgtk-devel libtool intltool make log4net
```

(Packages sometimes change, if you find that other packages are needed please let us know at CmisSync@aegif.jp thanks!)

### 2) Make sure you have a recent Mono

Run the following command: `mono --version`

If it says `Mono [...] version 2.x.y` the you must first install a newer version of Mono.

### 3) Build

At the root of the DotCMIS submodule, build DotCMIS, and copy the resulting DLL to `Extras/DotCMIS.dll`.

Then, at the root of the CmisSync root folder, run the following commands:

```bash
$ git submodule init
$ git submodule update
$ make -f Makefile.am
$  ./configure --with-dotcmis=Extras/DotCMIS.dll --with-newtonsoft-json=Extras/Newtonsoft.Json.dll
$ make
```

The platform-independent library CmisSync.Lib evolves very fast, and we unfortunately lack the budget to maintain the Linux version at every change. So you might need to add/remove a few .cs files from the Makefiles. Rarely you might even need to modify some library calls within the source code. We warmly welcome pull requests, thanks a lot!

### 4) Install

Run the following commands:

```
$ sudo make install
$ sudo cp Extras/DotCMIS.dll /usr/local/lib/cmissync/
$ sudo cp Extras/OpenDataSpaceDotCMIS/Newtonsoft.Json.dll /usr/local/lib/cmissync/
```

Done! You can now run CmisSync with this command: `cmissync start`
