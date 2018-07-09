## How to build CmisSync.Console on Mac

### Installing build requirements

Install [Xcode](https://developer.apple.com/xcode/), the [Mono Framework](http://www.mono-project.com/) (both MRE and MDK) and [MonoDevelop](http://monodevelop.com/) (which actually installs with the name "Xamarin Studio").

Start MonoDevelop and install the MonoMac add-in (it's in the menus: <tt>MonoDevelop</tt> > <tt>Add-in Manager</tt>).  
Latest MonoDevelop(Xamarin Studio 4.x.x) contains the MonoMac add-in.

### Build

Start MonoDevelop and open CmisSync/MacConsole/CmisSync.Console.Mac.sln

Build.


## How to use CmisSync.Console

### Install

Open CmisSync/MacConsole/installer/CmisSyncConsole.pkg

### Usage

```
$ cmissync_once <sync folder name>
```

### Uninstall

```
rm -r /usr/local/lib/cmissync.console
rm /usr/local/bin/cmissync_once
```
