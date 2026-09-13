# Islamic Screensaver — Project Plan

A native Windows 11 screensaver that plays Quran recitation over a calm background, showing the
verse being recited (Arabic + optional translation) in a frosted-glass panel, synced to the audio.
Ships with a settings app for reciters, surahs, order, backgrounds, panel look, and translation.

This file is the source of truth for scope and progress. Update the milestone checklists as work
lands; record any decision change in the Decision Log at the bottom rather than silently editing.

---

## 1. Product behaviour

- Activates after the idle timeout Windows already manages — it is a real `.scr`, selectable from
  Settings > Personalization > Lock screen > Screen saver.
- Any mouse movement or key press exits immediately, exactly like a stock screensaver.
- Works fully offline once content is cached. The screensaver never makes network calls.
- Multi-monitor: verse panel on the primary display, background-only on the others (option to mirror).

### Customisation surface

| Setting | Options |
|---|---|
| Reciters | One favourite, or several rotating in turn |
| Surahs | All, or a chosen selection |
| Order | Sequential or shuffled |
| Background | Bundled calm visuals, or a user image / video |
| Verse panel | Blur strength, grain amount, panel tint, text colours |
| Translation | On/off, language |
| Word highlight | On/off (uses word-level timestamps) |

---

## 2. Tech stack (decided)

| Area | Choice | Notes |
|---|---|---|
| Language / UI | C# + WPF on **.NET 10 (LTS)** | Native = instant paint, one process, safest on the secure desktop |
| Settings UI kit | **WPF UI (lepo.co)**, MIT | Fluent 2 look: NavigationView, Mica backdrop, cards, toggles |
| Publish | Self-contained single-file `.scr` | No runtime install for users |
| Install location | `%ProgramFiles%\IslamicScreensaver\` + `HKCU\Control Panel\Desktop\SCRNSAVE.EXE` | Not `System32` — the binary is ~70 MB |
| Audio + sync | **Quran.com API v4** — surah-level MP3 with verse + word timestamps | Gapless; sync = player position → verse lookup |
| Text / translations | Quran.com API v4 (Uthmani text, translations) | Same source as audio so verse keys align |
| Arabic font | KFGQPC Uthmanic Hafs (free, King Fahd Complex) | Bundled as a resource |
| UI font | Segoe UI Variable (system) | |
| Glass panel | Custom HLSL pixel shader: blur + grain + tint, one pass | Over a `VisualBrush` of the background layer |
| Video background | WPF `MediaElement` (hardware decode) | Revisit (FFME) only if 4K playback stutters |
| Settings storage | `%APPDATA%\IslamicScreensaver\settings.json` | `System.Text.Json`, versioned schema |
| Content cache | `%LOCALAPPDATA%\IslamicScreensaver\cache\` | Audio, timings, text, translations |
| Tests | xUnit for pure logic (playlist, timing lookup, settings migration) | UI is verified manually via the run checklist |

Rejected: Tauri/WebView2 (multi-process startup flash, secure-desktop risk), WinUI 3 (unpackaged
`.scr` friction), Electron (weight), per-ayah audio files (gaps between verses).

---

## 3. Architecture

Single binary, three modes — Windows launches the `.scr` with one of:

| Arg | Mode | Behaviour |
|---|---|---|
| `/s` | Run | One fullscreen topmost window per monitor, cursor hidden, exit on input |
| `/c` (or `/c:<hwnd>`) | Configure | Opens the settings window |
| `/p <hwnd>` | Preview | Renders a static preview into the Windows dialog's thumbnail |
| _(none)_ | Configure | Same as `/c` — makes double-clicking the file useful |

### Projects

```
src/
  IslamicScreensaver/            WPF app → published as IslamicScreensaver.scr
    App.xaml(.cs)                Mode dispatch from args
    Screensaver/                 Fullscreen windows, input watchdog, monitor enumeration
    Player/                      Playback engine, verse clock, playlist
    Rendering/                   Verse panel control, glass shader, background layer, animations
    Settings/                    Settings window + pages (WPF UI), live preview host
  IslamicScreensaver.Core/       No WPF dependency — testable
    Models/                      Settings, Reciter, Surah, Verse, Timing
    Content/                     Quran.com client, cache manager, download queue
    Playlist/                    Sequential / shuffled ordering, reciter rotation
    Timing/                      Position → verse / word lookup
  IslamicScreensaver.Tests/      xUnit
assets/
  fonts/                         KFGQPC Uthmanic Hafs
  backgrounds/                   Bundled default visuals
  shaders/                       glass.hlsl → glass.ps (compiled)
installer/                       Install / uninstall script (later: MSI)
docs/                            Design references, API notes
```

### Key runtime flow (`/s`)

1. Load `settings.json`; if missing, defaults.
2. Enumerate monitors → create one `ScreensaverWindow` per monitor, black, topmost, no cursor.
3. Build playlist from cached content only (skip anything not downloaded). If nothing cached,
   show background + a one-line "open settings to download recitations" hint.
4. Start `MediaPlayer` on the first surah; a 60 Hz `DispatcherTimer` reads `Position`, looks up
   the current verse/word, and updates the panel with a crossfade on change.
5. Input watchdog (mouse delta > small threshold, any key) → `Application.Shutdown()`.
   Ignore the first ~500 ms of mouse noise after launch (stock Windows behaviour).

### Data contract

- Verse key `surah:ayah` (e.g. `2:255`) is the join key across text, translations, and timings.
- Timing file per (reciter, surah): `[{ key, from_ms, to_ms, words: [{ from_ms, to_ms }] }]`.
- Cache manifest records which (reciter, surah) pairs are complete, so playlist building is a
  simple manifest read, never a filesystem scan.

---

## 4. Design direction

### Screensaver
- Full-bleed background with slow Ken Burns drift (~2 % scale over 60 s); soft edge vignette.
- One frosted card, centred-low (lower third), max width ~1100 px at 1080p; blur + fine grain +
  tint; 1 px low-opacity inner border; 48–64 px padding.
- Arabic: Uthmanic Hafs, ~56–72 px at 1080p (DPI-scaled), line-height ~1.9.
  Translation: Segoe UI Variable ~22 px, muted.
- Chip above the panel: surah name (Arabic + English) and ayah number. Reciter name in a corner.
- Verse change: old text fades + floats up ~8 px, new fades in; ~400 ms, eased. Hairline progress
  bar along the panel's bottom edge. No bounce, no slide — calm.
- Optional word highlight following the recitation.
- Default panel tint sampled from the background's average colour; user-overridable.

### Settings app
- Left `NavigationView`: Reciters · Surahs · Background · Appearance · Translation · About.
- Mica window, cards, Fluent controls (WPF UI).
- Appearance page hosts the **real** verse-panel control as a live preview above the sliders.
- Surahs: 114-item grid, search, select-all / none. Reciters: list with play-sample and download
  state. Background: bundled gallery + "choose file".

A design canvas (mockups) will be produced before Milestone 4 — see Milestone 0.

---

## 5. Milestones

Each milestone ends with something runnable. Do not start the next until the checklist is green.

### M0 — Foundation
- [x] Project plan (this file)
- [x] Design canvas: screensaver frame (2 backgrounds) + Appearance, Reciters, Surahs pages —
      sources in `docs/design/`, canvas at https://claude.ai/code/artifact/2e0c7130-9a55-447e-bfd3-49f62026fea0
- [x] Install .NET 10 SDK; confirm `dotnet --version` (10.0.401)
- [x] Solution + three projects scaffolded, builds clean
- [x] `git init`, `.gitignore`, first commit

### M1 — Screensaver shell
- [x] Arg parsing → `/s`, `/c`, `/p`, none (unit tested)
- [x] One fullscreen topmost window per monitor, cursor hidden, black background
- [x] Input watchdog with launch grace period; clean shutdown (origin sampled via `GetCursorPos`
      after 500 ms; keys/clicks exit immediately)
- [x] `/p` renders a static placeholder into the given HWND (visual check in the Windows dialog pending)
- [x] Placeholder settings window opens for `/c` (with a "Run screensaver now" dev button)
- [x] `installer/install.ps1` publishes self-contained to `%LOCALAPPDATA%\Programs\IslamicScreensaver`
      and registers `SCRNSAVE.EXE`; `uninstall.ps1` reverses it
- [x] **Appears and runs from the Windows screensaver dropdown** (confirmed 2026-09-13: dropdown,
      preview thumbnail, Preview button and Settings… all work)
- [x] Exit latency < 100 ms (measured 47 ms key → process exit)
- [ ] Verified: DPI scaling on mixed-DPI monitors (positioning works on 2 monitors; content DPI
      check waits for M4 when there is something to look at)

### M2 — Content pipeline (Core)
- [x] Quran.com v4 client: reciters, chapters, audio URL + timings (verse + word), Uthmani text,
      translations list + text (`Content/QuranApiClient` + pure `QuranApiParser`)
- [x] Cache manager with manifest; atomic writes; resume/retry downloads (`ContentCache`, `ContentDownloader`)
- [x] Download progress events (`IProgress<DownloadProgress>`; a queue/UI wrapper comes with M5)
- [x] Starter set: reciter 7 (Alafasy), Al-Fatihah + Juz ʿAmma, Saheeh International — fetched at
      install time via `install.ps1 -Seed` (`.scr /seed`), not shipped in the package (51 MB)
- [x] Unit tests: parser against captured payloads, verse keys, translation cleaning, manifest,
      cache round-trips, resumable/retrying/truncated downloads (58 tests)

### M3 — Playback + sync
- [x] Playlist builder: surah selection, sequential / shuffled, reciter rotation (`Core/Playlist`)
- [x] `MediaPlayer` engine with a pre-opened standby player for surah-to-surah advance (`Player/PlaybackEngine`)
- [x] Verse clock: position → verse (+ word) with binary search; unit tested (`Core/Timing/VerseClock`)
- [x] Plain-text verse display updating in sync, word highlight via per-word `Run`s (`Screensaver/VersePanel`)
- [x] Offline behaviour: uncached items skipped, empty-cache hint shown
- [x] Settings model + JSON store with normalisation and schema version (`Core/Settings`)
- [x] Resume where left off: `playback-state.json` saved on every verse change and on exit; verified live
- [ ] User to confirm: audio audible, verse text visible on the primary monitor

### M4 — Visuals
- [x] Background layer: bundled procedural visuals (dawn, night), user image + video, Ken Burns drift,
      vignette (`Rendering/BackgroundLayer`, `BundledBackgrounds`)
- [x] Glass panel — blur, grain, tint, hairline edge — via WPF `BlurEffect` over a live `VisualBrush`
      (`Rendering/GlassPanel`); no shader compiler needed
- [x] Verse panel: typography, chip, reciter label, progress hairline; overlay scaled from a 1080p design
- [x] Verse-change crossfade (fade + 8 px float); word highlight
- [x] Tint sampling from a user image (darkened average colour); bundled visuals carry their own tint
- [x] Matches the design canvas (screenshot-verified on 2560×1440, 2026-09-13)
- [x] User reviewed 2026-09-13: "so far so good"; minor tweaks to come later
- [ ] Optional: KFGQPC Uthmanic Hafs font in `assets/fonts/` (auto-detected; system Arabic faces otherwise)

### M5 — Settings app
- [x] Shell: nav pane, Fluent theme (`Window.ThemeMode`), in-house `SettingsCard` / toggle switch / buttons (`Settings/Theme.xaml`)
- [x] Pages: Reciters (selection, rotation, download per reciter, listen, volume, text timing), Surahs
      (grid, search, select all/clear, order, resume), Background (bundled thumbnails, own file, mirror),
      Appearance (live preview hosting the real panel, sliders, colours, reset), Translation (language →
      translation, download), About (attribution, cache/settings/log folders, clear cache)
- [x] Settings saved on change (debounced 400 ms) through `SettingsStore`
- [x] Download manager: one job at a time, per-row progress, cancel, failures surfaced
- [x] All six pages screenshot-verified 2026-09-13
- [ ] User to review the settings app; feedback folds into M6

### M5b — Live reciter catalog + Library (requested 2026-09-13)
Today's Reciters page mixes two jobs: *what is available online* and *what is on this PC*. Split them.
- [ ] **Reciters** page becomes a live search over Quran.com: fetches `resources/recitations` on demand
      and, per reciter, `chapter_recitations/{id}` to learn which surahs are actually hosted — so a
      reciter with no audio (the current 404s) is shown as unavailable, not offered for download
- [ ] Availability shown on the reciter page: tiles for surahs the API does not host are greyed out
      and cannot be selected; the page header says "N of 114 available"
- [ ] Clear error text for the remaining failure cases (offline, server error) instead of raw HTTP messages
- [ ] New **Library** page: everything installed on this PC, grouped by reciter — surah count, size on
      disk, play/rotation tick, open reciter page, delete reciter. This is where playback selection lives
- [ ] Catalog cached with a timestamp; refreshed in the background when older than a day
- [ ] Keep working offline: Library is fully usable with no network; Reciters page shows a friendly
      "connect to browse" state

### M7 — Localisation + dark mode (requested 2026-09-13)
- [ ] Settings app strings moved to resource dictionaries; **Arabic** UI with full RTL layout
      (`FlowDirection="RightToLeft"` on the window, mirrored nav pane and icons, Arabic UI font)
- [ ] Language picker in Settings (System / English / العربية); screensaver chip and labels follow it
- [ ] **Dark mode** for Settings: `Window.ThemeMode` Light/Dark/System plus a dark variant of every
      palette brush in `Theme.xaml` (cards, nav, dividers, text); thumbnails and live preview unaffected
- [ ] Theme picker in Settings (System / Light / Dark) and Windows accent colour option

### M6 — Hardening + release
- [ ] Locked / secure-desktop behaviour tested (audio, cache access)
- [ ] Multi-monitor mirror option
- [ ] Memory / GPU profile over a 1-hour run; no leaks on verse transitions
- [ ] Installer (script → MSI), uninstall cleans registry
- [ ] Attribution screen: Quran.com, KFGQPC font, translation licences
- [ ] v1.0 tag

---

## 6. Risks & open questions

| Risk | Mitigation |
|---|---|
| Secure-desktop restrictions break audio or cache reads | Test in M1/M6; fall back to background-only with a hint if playback fails |
| Quran.com API changes or rate limits | Client behind an interface; cache aggressively; starter set bundled |
| WPF `MediaElement` weak on 4K video | Cap bundled videos at 1080p; evaluate FFME if user files stutter |
| Blur shader cost on 4K displays | Blur a downscaled capture of the panel region, not the full frame |
| Translation licences vary | Only ship translations whose licence allows redistribution; list them in About |
| Some reciters in `resources/recitations` have no hosted surah audio → `chapter_recitations/{id}/{surah}` returns 404 (seen 2026-09-13) | M5b: query `chapter_recitations/{id}` first and only offer what exists |
| **Quran.com timing data is misaligned for some surahs** (confirmed 2026-09-13: reciter 7, surah 78 — Al-Fatihah is fine; audio and timings come from the same API response, so it is upstream data) | Test other reciters/surahs in M6; compare against Quran.com's own player; evaluate the newer `audio/reciters/{id}/audio_files?segments=true` endpoint as an alternate timing source; surface `SyncOffsetMs` in Settings |

Open:
- Which reciters ship in the starter set beyond the first one?
- Do we want a "pause on this verse" hotkey, or strictly exit-on-any-key? (Default: strict.)

---

## 7. Decision log

| Date | Decision | Why |
|---|---|---|
| 2026-09-12 | WPF over Tauri/WebView2 | Screensaver lifecycle: instant paint, single process, secure-desktop safety |
| 2026-09-12 | Surah-level audio + timestamps over per-ayah files | Gapless playback; sync is a position lookup |
| 2026-09-12 | .NET 10 over .NET 8 | .NET 8 support ends Nov 2026; .NET 10 is LTS and ships WPF Fluent baseline |
| 2026-09-12 | WPF UI (lepo.co) for the settings app | NavigationView + Mica; built-in Fluent theme lacks composite controls |
| 2026-09-12 | Install to Program Files, not System32 | Self-contained binary is large; registry path works either way |
| 2026-09-13 | Install per-user to `%LOCALAPPDATA%\Programs\IslamicScreensaver`, superseding Program Files | `SCRNSAVE.EXE` lives in HKCU anyway, so no elevation is needed for install or reinstall |
| 2026-09-13 | Self-contained **folder** publish (ReadyToRun), not single-file | WPF single-file still extracts native DLLs to `%TEMP%` at first run — slower cold start and a risk on the secure desktop; a folder has neither |
| 2026-09-13 | Starter content is downloaded at install time, not bundled | 51 MB of MP3 does not belong in git or the package; `install.ps1 -Seed` runs `.scr /seed`, idempotent |
| 2026-09-13 | Default translation: Saheeh International (id 20) | Quran.com's `quran/translations/131` (Clear Quran) no longer serves text; 20 does and is widely used |
| 2026-09-13 | Download completeness judged by HTTP `Content-Length`, not the API's `file_size` | API metadata is stale (claimed 839,808 bytes for a 793,327-byte file) |
| 2026-09-13 | Audio via **NAudio** (MIT) — Media Foundation decode, WASAPI output, one continuous stream — instead of WPF `MediaPlayer` | `MediaPlayer.Position` is the decoder's position (ahead of the speakers by ~a word) and drifts on VBR MP3s, which Quran.com's files are; the device's rendered-sample count is exact. Also gives true gapless surah advance |
| 2026-09-13 | A verse is on screen from its first word's timestamp until the next verse's first word | Matches the timing data's own granularity; `SyncOffsetMs` setting exists as a manual trim |
| 2026-09-13 | Glass effect via WPF `BlurEffect` + `VisualBrush`, not a custom HLSL shader | Same GPU path, no `fxc` build dependency; grain is a tiled noise brush, tint a colour layer |
| 2026-09-13 | Bundled backgrounds are procedural (gradients, tiled drawings), not image files | Resolution-independent, tiny, and no asset licensing to track |
| 2026-09-13 | Settings app on .NET 10's built-in Fluent theme + a few in-house controls (nav pane, toggle switch, settings card), superseding WPF UI (lepo.co) | Covers the design with one fewer dependency; the pieces WPF UI added (NavigationView, ToggleSwitch) are small to write and fully under our control |
