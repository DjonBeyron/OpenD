# OpenD

**English** · [Русский](README.ru.md)

A tiny, dark, tray-based YouTube downloader for Windows 10/11. Copy a link, press a hotkey, watch a small HUD in the bottom-right corner. Downloads survive reboots and network drops.

Built on open-source tools: [yt-dlp](https://github.com/yt-dlp/yt-dlp) (downloading), [ffmpeg](https://ffmpeg.org) (merging video and audio), [Deno](https://deno.com) (YouTube's JS challenges) and Microsoft Edge WebView2 (the sign-in window). UI fonts: [Montserrat](https://github.com/JulietaUla/Montserrat) and [Comfortaa](https://github.com/googlefonts/comfortaa) (SIL Open Font License, see `assets/fonts`).

## Download

**[Download the latest release](https://github.com/DjonBeyron/OpenD/releases/latest)**

| File | What it is |
| --- | --- |
| `OpenD-win-x64.zip` | Portable build: unzip anywhere and run `OpenD.exe`. Keep the four files together. |

Requirements: Windows 10/11 (.NET Framework 4.8 is built in) and the Edge WebView2 Runtime (already present on Windows 11).
On the first launch OpenD downloads yt-dlp, ffmpeg and Deno into `%APPDATA%\OpenD\bin` (about 150 MB, internet needed; if you are offline it waits and retries). After that it keeps yt-dlp up to date by itself.

## How to use

1. Start `OpenD.exe`. It lives in the system tray.
2. **Sign in once** (YouTube asks to "confirm you're not a bot"): tray icon → right click → **Sign in to YouTube…**, log in, press **Done**. Use a separate throwaway Google account, not your main one.
3. Copy a YouTube link and press **Ctrl+Alt+D**. A small HUD appears in the bottom-right corner with the title and progress.
4. Left click the tray icon to open the queue window. **Ctrl+Alt+H** shows or hides the HUD.

Optional: **Settings → Watch clipboard** adds links automatically, without the hotkey.

## What it can do

- Global hotkeys, tray icon, clipboard pickup, compact minimalist dark UI (Montserrat + Comfortaa).
- Mini HUD: only the title and percent, does not steal focus, appears during downloads and hides itself. Can be disabled or toggled with a hotkey.
- Queue window with a clean, jitter-free layout: percent, size, speed and ETA sit in fixed columns. Icon toolbar with hover and tooltips.
- Persistent queue: after a reboot or a crash, downloads resume from where they stopped.
- **Network-proof**: if the internet drops, downloads wait ("No network, retry in N s"), retry with a growing delay and continue by themselves when the connection returns. Partial files are resumed, not restarted.
- Quality: **Original** (best streams, no re-encoding), 2160p, 1440p, 1080p, 720p, 480p, MP4 H.264, audio only (original) or MP3.
- Container: Auto, MP4, MKV, WebM.
- Each row shows size, speed, ETA, the *ORIGINAL* mark and the exact format (for example `ORIGINAL 1080p AV1 WEBM`). Finished videos are brighter than queued ones.
- Four fixed icon slots per row: open the file's folder (the file is selected), pause / resume, stop (removes partial files), remove. Right click: open file, copy link, show error, delete together with the file.
- Files deleted in Explorer are marked "Deleted" in the list and can be downloaded again with one click. yt-dlp and ffmpeg run in a Windows Job Object, so no orphan process can keep a file locked.
- Settings: start with Windows, start hidden in the tray or with a window, clipboard watching, mini window on/off, **remappable hotkeys**, download folder, YouTube sign-in.
- Up to 2 parallel downloads, automatic yt-dlp update daily and after a failure.
- Debug log: tray → **Open log** (`%APPDATA%\OpenD\log.txt`).

Videos are saved to `Videos\OpenD`. Settings and queue are in `%APPDATA%\OpenD`.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| "Sign in to confirm you're not a bot" | Tray → **Sign in to YouTube…**. This is the reliable way, also over VPN. |
| Cookies from Chrome/Edge do not work | Newer Chrome encrypts cookies so other apps cannot read them. Use **Sign in to YouTube…** instead. |
| A hotkey is busy | Open **Settings**, click the hotkey field and press another combination. |
| Something else | Tray → **Open log** and look at the last lines. |

Optional `settings.json` field `Proxy` (for example `http://127.0.0.1:8080`) routes yt-dlp through a proxy.

## Build from source

```
build.cmd
```

It uses the C# compiler that ships with Windows (.NET Framework 4.8), nothing to install. The result is in `dist\`. The WebView2 DLLs live in `lib\`, the fonts in `assets\fonts\` are embedded into the exe.

## Legal

For personal use. Respect copyright and YouTube's Terms of Service; download only what you have the right to download.
