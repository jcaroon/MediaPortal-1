rem Check for Microsoft Antispyware .BAT bug
if exist .\kernel32.dll exit 1

cd
if not exist plugins mkdir plugins
if not exist plugins\windows mkdir plugins\windows
if not exist plugins\TagReaders mkdir plugins\TagReaders
if not exist plugins\subtitle mkdir plugins\subtitle
if not exist plugins\ExternalPlayers mkdir plugins\ExternalPlayers
if not exist plugins\process mkdir plugins\process
if not exist Wizards mkdir Wizards

del /F /Q plugins\windows\*.*
del /F /Q plugins\tagreaders\*.*
del /F /Q plugins\subtitle\*.*
del /F /Q plugins\ExternalPlayers\*.*
del /F /Q plugins\process\*.*
del *.dll
del *.ax

copy ..\..\..\RemotePlugins\HCWHelper\HCWHelper\bin\Release\HCWHelper.exe .
copy ..\..\..\RemotePlugins\X10Remote\AxInterop.X10.dll .
copy ..\..\..\RemotePlugins\X10Remote\Interop.X10.dll .
copy ..\..\..\RemotePlugins\IrTrans\IRTrans.NET.dll .
if exist ..\..\..\lame_enc.dll copy ..\..\..\lame_enc.dll .
copy ..\..\..\MPSA.ax .
copy ..\..\..\TSFileSource.ax .
copy ..\..\..\MPTSWriter.ax .
regsvr32 /s MPSA.ax
regsvr32 /s TSFileSource.ax
regsvr32 /s MPTSWriter.ax
copy ..\..\..\core\directshowhelper\directshowhelper\release\dshowhelper.dll .
copy ..\..\..\core\fontengine\fontengine\release\fontengine.dll .
rem copy ..\..\..\Interop.DirectShowHelperLib.dll .
copy ..\..\..\AxInterop.MOZILLACONTROLLib.dll .
copy ..\..\..\Interop.MOZILLACONTROLLib.dll .
copy ..\..\..\mfc71.dll .
copy ..\..\..\msvcp71.dll .
copy ..\..\..\msvcr71.dll .
copy ..\..\..\Microsoft.ApplicationBlocks*.dll .
copy ..\..\..\d3dx9_26.dll .
copy ..\..\..\Microsoft.DirectX.Direct3D.dll .
copy ..\..\..\Microsoft.DirectX.Direct3DX.dll .
copy ..\..\..\Microsoft.DirectX.DirectDraw.dll .
copy ..\..\..\Microsoft.DirectX.dll .
copy ..\..\..\Microsoft.DirectX.DirectInput.dll .
rem ExternalDisplay plugin LCD driver DLLs
copy ..\..\..\FTD2XX.DLL .
copy ..\..\..\SG_VFD.dll .
if not exist LUI\. mkdir LUI
copy ..\..\..\LUI.dll LUI\.
copy ..\..\..\Communications.dll .
copy ..\..\..\Interop.GIRDERLib.dll .
copy ..\..\..\MediaPadLayer.dll .
rem 
copy ..\..\..\KCS.Utilities.dll .
rem copy ..\..\..\X10Plugin.* .
copy ..\..\..\X10Unified.* .
copy ..\..\..\xAPMessage.dll .
copy ..\..\..\xAPTransport.dll .
copy ..\..\..\mbm5.dll .
copy ..\..\..\madlldlib.dll .
copy ..\..\..\ECP2Assembly.dll .
copy ..\..\..\edtftpnet-1.2.2.dll .
copy ..\..\..\dvblib.dll .
copy ..\..\..\Interop.WMEncoderLib.dll .
copy ..\..\..\Interop.TunerLib.dll .
copy ..\..\..\Interop.iTunesLib.dll .
copy ..\..\..\Microsoft.Office.Interop.Outlook.dll .
copy ..\..\..\XPBurnComponent.dll .

copy ..\..\..\Configuration\Wizards\*.* Wizards
copy ..\..\..\Configuration\bin\Release\Configuration.exe .
copy ..\..\..\Configuration\bin\Release\Configuration.exe.config .
copy ..\..\..\TVGuideScheduler\bin\Release\TVGuideScheduler.exe .

copy ..\..\..\DirectShowLib\bin\Release\DirectShowLib.dll .
copy ..\..\..\core\bin\Release\Core.dll .
copy ..\..\..\tvcapture\bin\release\tvcapture.dll .
copy ..\..\..\databases\bin\release\databases.dll .
copy ..\..\..\SubtitlePlugins\bin\release\SubtitlePlugins.dll plugins\subtitle
copy ..\..\..\TagReaderPlugins\bin\release\TagReaderPlugins.dll plugins\TagReaders
copy ..\..\..\ExternalPlayers\bin\release\ExternalPlayers.dll plugins\ExternalPlayers
copy ..\..\..\WindowPlugins\bin\release\WindowPlugins.dll plugins\Windows
copy ..\..\..\XihSolutions.DotMSN.dll plugins\Windows
copy ..\..\..\ProcessPlugins\bin\release\ProcessPlugins.dll plugins\process\
copy ..\..\..\Dialogs\bin\release\Dialogs.dll plugins\Windows
copy ..\..\..\RemotePlugins\bin\release\RemotePlugins.dll .
copy ..\..\..\sqlite.dll .
copy ..\..\..\tag.exe .
copy ..\..\..\tag.cfg .
copy ..\..\..\TaskScheduler.dll .
copy ..\..\..\AxInterop.WMPLib.dll .
copy ..\..\..\Interop.WMPLib.dll .
copy ..\..\..\FireDTVKeyMap.XML .
copy ..\..\..\FireDTVKeyMap.XML.Schema .


copy ..\..\..\WebEPG\WebEPG\bin\Release\WebEPG.dll .
copy ..\..\..\Utils\bin\Release\Utils.dll .

copy ..\..\..\Utils\bin\Release\Utils.dll WebEPG\
copy ..\..\..\WebEPG\WebEPG\bin\Release\WebEPG.dll WebEPG\
copy ..\..\..\WebEPG\WebEPG-xmltv\bin\Release\WebEPG-xmltv.exe WebEPG.exe
copy ..\..\..\WebEPG\WebEPG-conf\bin\Release\WebEPG-conf.exe WebEPG\
copy ..\..\..\WebEPG\WebEPG-channels\bin\Release\WebEPG-channels.exe WebEPG\

@if exist postbuild2.bat call postbuild2.bat
