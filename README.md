# Desktop Streamer

## Русский

Desktop Streamer захватывает выбранный монитор и публикует изображение по RTSP для VLC, ffplay, TRASSIR и других клиентов видеонаблюдения.

### Возможности

- Захват только выбранного монитора (выбор отдельных окон не используется);
- Основной поток и дополнительный поток с меньшим разрешением;
- Микрофон, звук компьютера или их совместный микс в G.711 μ-law;
- Интерфейс и справка на русском и английском языках;
- Сворачивание окна в системный трей;
- Логи приложения, FFmpeg и MediaMTX рядом с программой;
- Портативный архив Windows 10/11 со всеми необходимыми компонентами.

### Скачать и запустить

[Скачать готовый архив DesktopStreamer-Windows.zip](https://github.com/Prostyle90/rtsp-streamer/releases/latest/download/DesktopStreamer-Windows.zip)

Распакуйте ZIP и запустите DesktopStreamer.exe. Архив уже содержит исполняемый файл, AudioLoopback, FFmpeg и MediaMTX — ручная компиляция и установка дополнительных пакетов не нужны.

Порт RTSP по умолчанию — 8554. При подключении с другого устройства разрешите этот TCP-порт в брандмауэре Windows.

### TRASSIR / DSSL

RTSP-адрес из приложения можно добавить в ПО TRASSIR компании DSSL как RTSP-канал: создайте IP/RTSP-камеру, укажите адрес основного или субпотока, выберите TCP-транспорт и сохраните канал.

### Сборка для разработчиков

Команды сборки из исходников находятся в [BUILD.md](BUILD.md). Для обычного использования сборка не требуется — используйте готовый ZIP из раздела выше.

### Контакт и лицензии

Связь: prostyle1992@gmail.com. Лицензии встроенных компонентов указаны в [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

---

## English

Desktop Streamer captures the selected monitor and publishes it as an RTSP stream for VLC, ffplay, TRASSIR and other surveillance clients.

### Features

- Capture of the selected monitor only (individual window capture is not used);
- Main stream plus an optional lower-resolution sub-stream;
- Microphone, PC playback audio, or a mixed G.711 μ-law track;
- Russian and English interface and Help;
- Minimize to the Windows system tray;
- Application, FFmpeg and MediaMTX logs beside the program;
- Portable Windows 10/11 archive with all required components.

### Download and run

[Download the ready-to-run DesktopStreamer-Windows.zip](https://github.com/Prostyle90/rtsp-streamer/releases/latest/download/DesktopStreamer-Windows.zip)

Extract the ZIP and run DesktopStreamer.exe. The archive already contains the executable, AudioLoopback, FFmpeg and MediaMTX — no manual compilation or extra package installation is required.

The default RTSP port is 8554. Allow this TCP port through Windows Firewall when connecting from another device.

### TRASSIR / DSSL

The RTSP address from the application can be added to TRASSIR by DSSL as an RTSP channel: create an IP/RTSP camera, enter the main or sub-stream address, select TCP transport and save the channel.

### Build from source

See [BUILD.md](BUILD.md) for build commands. For normal use, no build is required — download the ready-to-run ZIP above.

### Contact and licenses

Contact: prostyle1992@gmail.com. See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for bundled component licenses.
