# Command-line CmisSync

This tool allows you to synchronize CMIS folders without a graphical user interface.

# Usage
1. Write your config.xml file as described at https://github.com/aegif/CmisSync/wiki/Internals#configxml . You can also use the GUI version of CmisSync to easily create the config.xml file, then close it.

2. Launch synchronization and let it run at configured interval
```
CmisSyncConsole.exe
```

# Build on Ubuntu 2018.04
```
git clone git@github.com:aegif/CmisSync.git
cd CmisSync/CmisSync.Lib/
xbuild CmisSync.Lib.csproj /p:TargetFrameworkVersion="v4.5"
cd ../CmisSync.Console/
xbuild CmisSync.Console.csproj
```
The executable is generated at `bin/Debug/CmisSync.Console.exe`.
