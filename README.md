# Desktop Streamer

Desktop Streamer captures a selected monitor and publishes it as an RTSP stream for VLC, ffplay and surveillance clients.

## Features

- monitor-only capture (window capture is intentionally not available);
- main stream plus optional lower-resolution sub-stream;
- microphone, PC playback audio, or both mixed as G.711 μ-law;
- Russian and English interface with in-app Help;
- minimize to the Windows system tray;
- application, FFmpeg and MediaMTX logs beside the executable;
- portable Windows 10/11 distribution with FFmpeg, MediaMTX and AudioLoopback included.

## Download

**Ready-to-run Windows build:** [Download DesktopStreamer-Windows.zip](https://github.com/Prostyle90/rtsp-streamer/releases/latest/download/DesktopStreamer-Windows.zip)

Extract the ZIP and run `DesktopStreamer.exe`. The archive already contains the executable and all required media components; no Python, FFmpeg or MediaMTX installation or manual compilation is required.

The default RTSP port is `8554`. Allow inbound TCP traffic in Windows Firewall when connecting from another device.

## Build

See [BUILD.md](BUILD.md) for the Windows build command. The application uses the .NET Framework 4.x component normally available in Windows 10/11.

## Contact

`prostyle1992@gmail.com`

## License

See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for bundled FFmpeg and MediaMTX license information.

## TRASSIR / DSSL

RTSP-адрес из приложения можно добавить в ПО TRASSIR компании DSSL как RTSP-канал: создайте IP/RTSP-камеру, укажите адрес основного или субпотока, выберите TCP-транспорт и сохраните канал.
