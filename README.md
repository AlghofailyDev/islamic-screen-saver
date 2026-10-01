# Islamic Screensaver

A Windows screensaver that recites the Qur'an over a calm background, with the verse being recited
shown in a frosted-glass panel and kept in time with the reciter. It comes with a settings app for
choosing reciters, surahs, the look of the panel, and when it should stay quiet.

> **This is a test build, not a release.** It works, but expect rough edges — and please tell us
> about them. See [What to report](#what-to-report).

## Install

You need **Windows 10 or 11** (64-bit) and an internet connection. Nothing else — no administrator
rights, no extra software.

1. Download **`IslamicScreensaver-Setup-….exe`** from this repository's
   [Releases](../../releases) page.
2. Double-click it.
3. Windows will probably say **"Windows protected your PC"**. That's because this test build isn't
   signed with a paid certificate — not because anything is wrong with it. Click **More info**, then
   **Run anyway**.
4. Click through the welcome page and the license. There's nothing to accept — it's free software,
   and the license is there to read.
5. **Where to keep recitations** — choose a folder, or keep the suggested one. They can take a few
   gigabytes, so another drive is worth considering. You can move them later in *Library → Storage*.
6. Leave both boxes ticked:
   - **Use it as my screensaver**
   - **Download starter recitations now** — Mishari al-ʿAfasy reciting Al-Fātiḥah and Juzʾ ʿAmma
     (about 55 MB), so it has something to play straight away
7. On the last page, leave **Open the screensaver's settings** ticked to have a look around.

The installer is in English or Arabic, following your Windows language.

You'll need about **160 MB** for the app, plus whatever recitations you download — a full Qur'an
from one reciter is around 1.3 GB.

## Try it

Open **Settings → Personalization → Lock screen → Screen saver**. *IslamicScreensaver* will already
be selected.

- **Preview** shows it immediately. Move the mouse or press a key to leave.
- **Settings…** opens its settings app, where you choose reciters and everything else.
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
- the **log file** for that day, from `%LOCALAPPDATA%\IslamicScreensaver\logs` — paste that path
  into File Explorer's address bar to open it. Its first line for each run says which version you
  have.

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

Download the newer setup and run it. It replaces the old version and keeps your settings and
downloads. You can untick *Download starter recitations* this time.

## Uninstall

**Settings → Apps → Installed apps → Islamic Screensaver → Uninstall.**

It asks whether to also remove your settings and downloaded recitations. **No** keeps them, so
they're still there if you install again; **Yes** removes everything, including a library you moved
to another drive.

## License

Islamic Screensaver is free software, released under the
[GNU General Public License v3](LICENSE). You may use it, share it and change it. If you share a
changed version, it has to stay under the same license, with its source available and the original
credited — so it can be built on, but never turned into a closed product.

The recitations, Qur'an text and translations are not part of this project: they come from
[Quran.com](https://quran.com) and [MP3 Quran](https://mp3quran.net), under their own terms. See
[THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md) for those and for the software bundled with the app.

## Where things are kept

| What | Where |
|---|---|
| The app | `%LOCALAPPDATA%\Programs\IslamicScreensaver` |
| Your settings | `%APPDATA%\IslamicScreensaver\settings.json` |
| Downloaded recitations | `%LOCALAPPDATA%\IslamicScreensaver\cache` — or wherever you moved them in *Library → Storage* |
| Logs | `%LOCALAPPDATA%\IslamicScreensaver\logs` |

---

## Building it yourself

For working on the code rather than testing it. You'll need the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

**Install straight from the source** — builds, installs and registers it in one go:

```powershell
powershell -ExecutionPolicy Bypass -File installer\install.ps1 -Seed
```

`-ExecutionPolicy Bypass` applies to that one command only; Windows blocks scripts by default.
Remove it again with `installer\uninstall.ps1` (add `-Purge` to delete settings and downloads too).

**Build the setup.exe** for a tester. This also needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)
on your machine only (`winget install --id JRSoftware.InnoSetup --scope user`):

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-setup.ps1
```

It writes `installer\Output\IslamicScreensaver-Setup-0.9.<n>.exe`, where *n* is the commit count, so
each build upgrades the last. Attach that file to a GitHub release — it's kept out of the repository
on purpose.
