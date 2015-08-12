## Building on Mac

You can choose to build CmisSync from source or to download the CmisSync bundle.


### Installing build requirements

Install [Xcode](https://developer.apple.com/xcode/), [MacPorts](https://www.macports.org), the [Mono Framework](http://www.mono-project.com) (both MRE and MDK) and [MonoDevelop](http://monodevelop.com) (which actually installs with the name "Xamarin Studio").

You may need to adjust some environment variables to let the build environment tools find mono:

```bash
export PATH=/Library/Frameworks/Mono.framework/Versions/Current/bin:$PATH
export PKG_CONFIG=/Library/Frameworks/Mono.framework/Versions/Current/bin/pkg-config
export PKG_CONFIG_PATH=/Library/Frameworks/Mono.framework/Versions/Current/lib/pkgconfig
```

Install <tt>automake</tt>, <tt>libtool</tt> and <tt>intltool</tt> using <tt>MacPorts</tt>:

```bash
$ sudo port install automake intltool libtool pkgconfig
```

Build log4net (if not Xamarin, `mdtoolpath` is in `/Applications/MonoDevelop.app/Contents/MacOS/`):
```bash
$ /Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build Extras/log4net-1.2.11/src/log4net.vs2010.csproj
```

Copy MonoMac.dll to `Extras` (if not Xamarin, `MonoMac.dll` might be in the MonoDevelop AddIns folder, often `~/.config/MonoDevelop/addins/`):
```bash
$ cp /Applications/Xamarin\ Studio.app/Contents/MacOS/MonoDoc.app/Contents/MonoBundle/MonoMac.dll Extras
```

Start the first part of the build:

```bash
$ make -f Makefile.am
$ ./configure --with-dotcmis=Extras/DotCMIS.dll \
 --with-newtonsoft-json=Extras/OpenDataSpaceDotCMIS/Newtonsoft.Json.dll \
 --with-nunit=Extras/nunit.framework.dll \
 --with-log4net=Extras/log4net-1.2.11/build/bin/net/2.0/debug/log4net.dll \
 --with-monomac=Extras/MonoMac.dll
$ make
```

Even if the last `make` step fails, Monodevelop (or Xamarin Studio) might be able to compile it better.

Now that you have compiled the libraries, open `CmisSync/Mac/CmisSync.sln` in
MonoDevelop (or Xamarin Studio) and start the build (Build > Build All).
