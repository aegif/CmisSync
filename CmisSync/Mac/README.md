## Building on Mac

You can choose to build SparkleShare from source or to download the SparkleShare bundle.


### Installing build requirements

Install [Xcode](https://developer.apple.com/xcode/), the [Mono Framework](http://www.mono-project.com/) (both MRE and MDK) and [MonoDevelop](http://monodevelop.com/).

Start MonoDevelop and install the MonoMac add-in (it's in the menus: <tt>MonoDevelop</tt> > <tt>Add-in Manager</tt>).


You may need to adjust some environment variables to let the build environment tools find mono:
   
```bash
$ export PATH=/Library/Frameworks/Mono.framework/Versions/Current/bin:$PATH
$ export PKG_CONFIG=/Library/Frameworks/Mono.framework/Versions/Current/bin/pkg-config
$ export PKG_CONFIG_PATH=/Library/Frameworks/Mono.framework/Versions/Current/lib/pkgconfig
```

Install <tt>automake</tt>, <tt>libtool</tt> and <tt>intltool</tt> using <tt>MacPorts</tt>:

```bash
$ sudo port install automake intltool libtool
```

Start the first part of the build:

```bash
$ make -f Makefile.am
$ ./configure --with-dotcmis=Extras/DotCMIS.dll
$ make
```

Now that you have compiled the libraries, open `SparkleShare/Mac/CmisSync.sln` in
MonoDevelop and start the build (Build > Build All).
