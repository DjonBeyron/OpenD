# OpenD

**English** · [Русский](README.ru.md)

A tiny, dark, tray-based YouTube downloader for Windows 10/11. Copy a link, press a hotkey, watch a mini HUD in the bottom-right corner. Downloads survive reboots.

Built on open-source tools: [yt-dlp](https://github.com/yt-dlp/yt-dlp) (downloading), [ffmpeg](https://ffmpeg.org) (merging video and audio), [Deno](https://deno.com) (YouTube's JS challenges) and Microsoft Edge WebView2 (the sign-in window).

## Download

**[Download the latest release](https://github.com/DjonBeyron/OpenD/releases/latest)**

| File | What it is |
| --- | --- |
| `OpenD-win-x64.zip` | Portable build: unzip anywhere and run `OpenD.exe`. Keep the four files together. |

Requirements: Windows 10/11 (.NET Framework 4.8 is built in) and the Edge WebView2 Runtime (already present on Windows 11).
On the first launch OpenD downloads yt-dlp, ffmpeg and Deno into `%APPDATA%\OpenD\bin` (about 150 MB, internet needed). After that it keeps yt-dlp up to date by itself.

## How to use

1. Start `OpenD.exe`. It lives in the system tray.
2. **Sign in once** (needed because YouTube asks for "confirm you're not a bot"): tray icon → right click → **Sign in to YouTube…**, log in, press **Done**. Use a separate throwaway Google account, not your main one.
3. Copy a YouTube link and press **Ctrl+Alt+D**. A mini HUD appears in the bottom-right corner with progress, speed and ETA.
4. Left click the tray icon to open the queue window.

Optional: tray → **Watch clipboard** adds links automatically, without the hotkey.

## What it can do

- Global hotkey, tray icon, clipboard pickup, compact minimalist dark UI.
- Mini HUD: does not steal focus, stays while downloads run, hides itself.
- Persistent queue: after a reboot, downloads resume from where they stopped.
- Quality: **Original** (best streams, no re-encoding), 2160p, 1440p, 1080p, 720p, 480p, MP4 H.264, audio only (original) or MP3.
- Container: Auto, MP4, MKV, WebM.
- Each row shows size, speed, ETA, the *ORIGINAL* mark and the exact format (for example `ORIGINAL · 1080p · AV1 · Opus · WEBM`).
- Row buttons: open the file's folder (the file is selected), pause / resume, stop (removes partial files), delete. Right click: open file, copy link, show error, delete with file.
- Settings: start with Windows, start hidden in the tray or with a window, clipboard watching, download folder.
- Up to 2 parallel downloads, automatic yt-dlp update daily and after a failure.
- Debug log: tray → **Open log** (`%APPDATA%\OpenD\log.txt`).

Videos are saved to `Videos\OpenD`. Settings and queue are in `%APPDATA%\OpenD`.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| "Sign in to confirm you're not a bot" | Tray → **Sign in to YouTube…**. This is the reliable way, also over VPN. |
| Cookies from Chrome/Edge do not work | Newer Chrome encrypts cookies so other apps cannot read them. Use **Sign in to YouTube…** instead. |
| Hotkey is busy | Change `Mods`/`Key` in `%APPDATA%\OpenD\settings.json` (Mods: 1=Alt, 2=Ctrl, 4=Shift, 8=Win, summed; Key: virtual-key code, 68 = D). |
| Something else | Tray → **Open log** and look at the last lines. |

## Build from source

```
build.cmd
```

It uses the C# compiler that ships with Windows (.NET Framework 4.8), nothing to install. The result is in `dist\`. The WebView2 DLLs live in `lib\`.

## Legal

For personal use. Respect copyright and YouTube's Terms of Service; download only what you have the right to download.
