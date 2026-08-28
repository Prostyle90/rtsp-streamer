# Building Desktop Streamer

The distributable GUI is built from `DesktopStreamer.cs` with the C# compiler that ships with Windows/.NET Framework:

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
  /out:dist\DesktopStreamer.exe `
  /reference:System.dll /reference:System.Core.dll `
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
  DesktopStreamer.cs
```

Copy the resulting executable together with `AudioLoopback.exe`, `ffmpeg.exe` and `mediamtx.exe` into `release\DesktopStreamer-Windows`, then create `DesktopStreamer-Windows.zip` from that folder. The release folder intentionally excludes legacy PyInstaller build outputs and diagnostic binaries.

`desktop_streamer.py` and its PyInstaller specs are retained as legacy development artifacts; the GitHub-ready package uses the native media binaries and the WinForms executable above.
