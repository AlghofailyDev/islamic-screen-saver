# Islamic Screensaver

A Windows screensaver that recites the Qur'an over a calm background, with the verse being recited
shown in a frosted-glass panel and kept in time with the reciter. It comes with a settings app for
choosing reciters, surahs, the look of the panel, and when it should stay quiet.

> **This is a test build, not a release.** It works, but expect rough edges — and please tell us
> about them. See [What to report](#what-to-report).

## What you need

- **Windows 10 or 11**, 64-bit
- **The .NET 10 SDK** — [download it here](https://dotnet.microsoft.com/download/dotnet/10.0).
  Pick the SDK for Windows x64, not the "Runtime". To check it's there, run `dotnet --list-sdks`
  and look for a line starting with `10.`
- **An internet connection** for the first install — it builds the app and downloads some starting
  recitations
- About **160 MB** free for the app, plus whatever recitations you download (a full Qur'an from one
  reciter is around 1.3 GB)

You do **not** need administrator rights. Everything installs for your Windows account only.

## Install

Open **PowerShell** and run:

```powershell
git clone <this repository's URL>
cd <the folder it just created>
powershell -ExecutionPolicy Bypass -File installer\install.ps1 -Seed
```

No git? Use **Code → Download ZIP** on GitHub, unzip it, open PowerShell in that folder, and run the
last line.

What that does:

1. Builds the app and installs it to `%LOCALAPPDATA%\Programs\IslamicScreensaver`.
2. Sets it as your screensaver.
3. With `-Seed`, downloads enough to play straight away: Mishari al-ʿAfasy reciting Al-Fātiḥah and
   Juzʾ ʿAmma, with the Saheeh International translation (about 55 MB).

The **first** run takes a few minutes while .NET fetches what it needs. Later runs are quick.

> `-ExecutionPolicy Bypass` applies to this one command only — Windows blocks scripts by default, and
> this lets the installer run without changing that setting for anything else.

## Try it

Open **Settings → Personalization → Lock screen → Screen saver**. *IslamicScreensaver* should
already be selected.

- **Preview** shows it immediately. Move the mouse or press a key to leave.
- **Settings…** opens its settings app. This is where you choose reciters and everything else.
- **Wait** is how many idle minutes before it starts on its own.

Things worth trying in the settings app:

- **Reciters** — over 240 reciters, many with several recordings (different riwāyāt such as Ḥafṣ and
  Warsh, murattal and mujawwad). Open one, choose a recording, pick surahs, download.
- **Appearance** — where the verse panel sits (or hide it), text size, blur and colours, Arabic or
  English, light or dark.
- **Quiet hours** — a window of the day it should stay out of, so it never wakes you at night.
- **Audio** — which speakers to use, so it can play through speakers while your headset stays the
  Windows default.

## What to report

Anything that looks wrong, sounds wrong, or confuses you. The most useful reports say:

- **what you did**, and **what you expected** versus **what happened**
- **which reciter and surah** were playing, if it's about the recitation or the text
- the **log file** for that day, from `%LOCALAPPDATA%\IslamicScreensaver\logs`

Paste that folder path into File Explorer's address bar to open it.

## Known issues

- **On a few surahs the text runs slightly ahead of or behind the reciter.** That comes from the
  timing data some sources publish. *Settings → Audio → Text timing* can nudge it.
- **Some recordings show the verse panel without following along.** About 170 recordings, from the
  MP3 Quran source, have no verse timings at all. They play correctly, but the panel can't track them
  yet. The surah list marks those surahs.
- **Very long verses use smaller text.** Nineteen verses don't fit on screen at the normal size, so
  they're shrunk to fit. Āyat ad-dayn (2:282) is noticeably smaller. This is deliberate.
- **The Arabic uses Windows' built-in font.** A proper Qur'an font isn't included yet.

## Update to a newer test build

```powershell
git pull
powershell -ExecutionPolicy Bypass -File installer\install.ps1
```

Your settings and downloads are kept.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File installer\uninstall.ps1
```

That removes the app and unregisters the screensaver, but **keeps** your settings and downloaded
recitations in case you reinstall. To remove those too:

```powershell
powershell -ExecutionPolicy Bypass -File installer\uninstall.ps1 -Purge
```

`-Purge` also finds and removes a library you moved to another drive.

## Where things are kept

| What | Where |
|---|---|
| The app | `%LOCALAPPDATA%\Programs\IslamicScreensaver` |
| Your settings | `%APPDATA%\IslamicScreensaver\settings.json` |
| Downloaded recitations | `%LOCALAPPDATA%\IslamicScreensaver\cache` — or wherever you moved them in *Library → Storage* |
| Logs | `%LOCALAPPDATA%\IslamicScreensaver\logs` |
