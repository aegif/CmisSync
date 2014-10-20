### 1) Install required packages

According to your Linux distributions, run one of the following commands to install required packages:

#### Ubuntu

```bash
$ sudo apt-get install libappindicator0.1-cil-dev gtk-sharp2 mono-runtime mono-devel \
  monodevelop libndesk-dbus1.0-cil-dev nant libnotify-cil-dev libgtk2.0-cil-dev mono-mcs \
  mono-gmcs libwebkit-cil-dev intltool libtool libndesk-dbus-glib1.0-cil-dev \
  liblog4net-cil-dev libnewtonsoft-json-cil-dev gvfs
```

#### Fedora

```bash
$ sudo yum install gtk-sharp2 gtk-sharp2-devel mono-core mono-devel monodevelop \
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


### 2) Make sure you have a recent Mono

Run the following command: `mono --version`

If the output says something like `Mono [...] version 3.x.y` then proceed to the next paragraph.

If it says `Mono [...] version 2.x.y` the you must first install a newer version of Mono.

### 3) Build

At the root of the CmisSync root folder, run the following commands:

```bash
$ git submodule init
$ git submodule update
$ make -f Makefile.am
$ ./configure --with-dotcmis=Extras/DotCMIS.dll
$ make
```

### 4) Install

Run the following commands:

```
$ sudo make install
$ sudo cp Extras/DotCMIS.dll /usr/local/lib/cmissync/
$ sudo cp Extras/OpenDataSpaceDotCMIS/Newtonsoft.Json.dll /usr/local/lib/cmissync/
```

Done! You can now run CmisSync with this command: `cmissync start`
