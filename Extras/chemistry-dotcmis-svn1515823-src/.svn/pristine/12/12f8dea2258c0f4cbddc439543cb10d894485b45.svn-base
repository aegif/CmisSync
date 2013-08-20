@echo off

rem
rem    Licensed to the Apache Software Foundation (ASF) under one
rem    or more contributor license agreements.  See the NOTICE file
rem    distributed with this work for additional information
rem    regarding copyright ownership.  The ASF licenses this file
rem    to you under the Apache License, Version 2.0 (the
rem    "License"); you may not use this file except in compliance
rem    with the License.  You may obtain a copy of the License at
rem
rem      http://www.apache.org/licenses/LICENSE-2.0
rem
rem    Unless required by applicable law or agreed to in writing,
rem    software distributed under the License is distributed on an
rem    "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
rem    KIND, either express or implied.  See the License for the
rem    specific language governing permissions and limitations
rem    under the License.
rem

rem This batch file creates a release.
rem It requires Cygwin.

set DOTCMISVERSION=0.4
set DOTCMISZIPSRC=chemistry-dotcmis-%DOTCMISVERSION%-src.zip
set DOTCMISZIPBIN=chemistry-dotcmis-%DOTCMISVERSION%-bin.zip
set DOTCMISRC=RC1

set CYGWIN=ntea

echo Building...
cd DotCMIS
call build.bat
cd ..

echo Creating release directories...
rmdir /S /Q release-src
mkdir release-src
rmdir /S /Q release-bin
mkdir release-bin

echo Copying readme, etc...
copy LICENSE release-src
copy LICENSE release-bin
copy NOTICE release-src
copy NOTICE release-bin
copy DEPENDENCIES release-src
copy DEPENDENCIES release-bin
copy README release-src
copy README release-bin

echo Copying binaries ...
copy DotCMIS\bin\Release\DotCMIS.dll release-bin
copy DotCMIS\doc\DotCMISDoc.chm release-bin
chmod -R a+rwx release-bin

echo Copying source...
mkdir release-src\src
xcopy DotCMIS release-src\src /E
rmdir /S /Q release-src\src\bin
rmdir /S /Q release-src\src\obj
rmdir /S /Q release-src\src\doc
chmod -R a+rwx release-src

echo Creating release file...
rmdir /S /Q artifacts
mkdir artifacts

cd release-src
zip -r  ../artifacts/%DOTCMISZIPSRC% *
cd ..

cd release-bin
zip -r  ../artifacts/%DOTCMISZIPBIN% *
cd ..

echo Signing release file...
cd artifacts

gpg --armor --output %DOTCMISZIPSRC%.asc --detach-sig %DOTCMISZIPSRC%
gpg --print-md MD5 %DOTCMISZIPSRC% > %DOTCMISZIPSRC%.md5
gpg --print-md SHA1 %DOTCMISZIPSRC% > %DOTCMISZIPSRC%.sha
gpg --print-md MD5 %DOTCMISZIPSRC%.asc > %DOTCMISZIPSRC%.asc.md5
gpg --print-md SHA1 %DOTCMISZIPSRC%.asc > %DOTCMISZIPSRC%.asc.sha

gpg --armor --output %DOTCMISZIPBIN%.asc --detach-sig %DOTCMISZIPBIN%
gpg --print-md MD5 %DOTCMISZIPBIN% > %DOTCMISZIPBIN%.md5
gpg --print-md SHA1 %DOTCMISZIPBIN% > %DOTCMISZIPBIN%.sha
gpg --print-md MD5 %DOTCMISZIPBIN%.asc > %DOTCMISZIPBIN%.asc.md5
gpg --print-md SHA1 %DOTCMISZIPBIN%.asc > %DOTCMISZIPBIN%.asc.sha

cd ..

echo Creating RC tag
rem svn copy https://svn.apache.org/repos/asf/chemistry/dotcmis/trunk https://svn.apache.org/repos/asf/chemistry/dotcmis/tags/chemistry-dotcmis-%DOTCMISVERSION%-%DOTCMISRC%

