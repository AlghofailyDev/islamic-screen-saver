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
Investigated 2026-09-13: the catalog is NOT the problem — all 12 reciters list all 114 files. The 404
is on the CDN itself (reciter 8, `siddiq_al-minshawi/mujawwad/001.mp3`), i.e. Quran.com advertises audio
that is not hosted. So availability cannot be trusted from metadata; the download path must be resilient.
Search stays local (filtering the cached catalog), so typing costs no requests.
- [x] Catalog cached with a fetched-at timestamp (`manifest.CatalogFetchedAt`); refreshed in the
      background when older than a day; **Reciters** search filters the cached list locally
      (zero requests per keystroke)
- [x] **Download resilience (the real fix):** a 404 throws `ContentUnavailableException`, marks that
      surah unavailable, is not retried, and does not abort the job — the rest continue and the page
      reports "N of M surahs are not available from this reciter" at the end
- [x] Unavailable surahs remembered in the cache manifest; dimmed on the reciter page as "Not
      available", not selectable, excluded from Select all / Select missing and from download counts
- [x] Clear error text for the remaining failure cases (offline, rate-limited, server error, disk)
      instead of raw HTTP messages (`DownloadManager.Describe`)
- [x] New **Library** page: everything installed on this PC per reciter — surah count, size on disk,
      playback tick, rotate toggle, listen, delete, open reciter page; empty state points at Reciters
- [x] Keep working offline: Library reads only the cache; the screensaver shows a clear hint when the
      download folder is on a drive that is not attached

### M5e — Isti'ādhah and basmalah before recitation (requested 2026-09-17)
Etiquette of recitation: the isti'ādhah once when recitation begins, the basmalah before each surah.
Quran.com's recordings are uniform — **only Al-Fatihah contains the basmalah** (as its first verse) —
so every other surah needs one supplied. At-Tawbah is recited without one.
- [x] `RecitationProgram` (Core, unit tested) turns a playlist into steps: isti'ādhah once, basmalah
      where needed, then each surah; resuming mid-surah skips that surah's basmalah but keeps the isti'ādhah
- [x] Basmalah audio comes from the same reciter's Al-Fatihah, verse 1 sliced by its own timings —
      their own voice, no extra download, no licensing question (`Core/Playlist/BasmalahSource`, unit tested)
- [x] **When that reciter's Al-Fatihah is not downloaded** (older downloads, or the user deleted it),
      a bundled basmalah recording is used instead; the Audio page names those reciters and offers to
      download their Al-Fatihah so their own voice is used
- [x] Al-Fatihah is fetched alongside any reciter download while the basmalah is enabled (< 1 MB)
- [x] Isti'ādhah and fallback basmalah ship in `assets/audio/`; either can be replaced by a user file
- [x] Their level is matched to the recitation automatically (`Loudness.MatchGain`), since they come
      from a different master — measured 0.39× on the shipped isti'ādhah
- [x] The panel shows the isti'ādhah / basmalah text and translation while they play
- [x] **Not configurable**: this is how the Qur'an is recited, so there are no toggles, no file
      pickers and nothing to switch off — the bundled recordings are always used
- [x] Verified end to end 2026-09-17

### M5c — Audio page: output device + playback (requested 2026-09-13)
- [x] New **Audio** page in Settings; Volume and Text timing moved here from Reciters
- [x] **Output device** picker: "System default" or a specific render endpoint, listed live via
      NAudio's `MMDeviceEnumerator` (active devices only, friendly names), with a Refresh button
- [x] Persisted as the endpoint id (`AppSettings.OutputDeviceId`, null = default) plus its name;
      resolved at launch, falling back to the default — with a log line — if it is missing/unplugged.
      An unplugged saved device still appears in the list, marked "(not connected)", with a note
- [x] `PlaybackEngine.CreateOutput` builds the WASAPI player with `.WithDevice(...)`; the Listen
      buttons use the same builder so what you test is what you get
- [x] "Test" button plays a downloaded surah through the chosen device (toggles to Stop)
- [x] "System default" uses `.WithDefaultDeviceStreamRouting()`, so it follows Windows at runtime
- [x] Verified on this PC 2026-09-16: 4 endpoints listed (HyperX headset, HyperX speakers, 2 monitors)
- [ ] User to try: pick the speakers while the headset is the Windows default

### M5d — Custom storage location (requested 2026-09-13)
- [x] `AppSettings.CacheDirectory` (null = `%LOCALAPPDATA%\IslamicScreensaver\cache`) with
      `ResolvedCacheDirectory`; `ContentCache.For(settings)` used by the screensaver, settings app and seeder
- [x] "Storage location" card on the Library page (About points at it): current path, size on disk,
      **Open**, **Change…** folder picker, **Reset**
- [x] Changing the location offers to **move** existing content (copy with progress, then delete the
      source — never the other way round) or leave it and start fresh
- [x] Validation before saving: writable, not inside the app folder, not inside the current folder
      (`CacheMover.Validate`, unit tested)
- [x] Screensaver shows a clear hint instead of failing when the folder is unavailable at launch
- [x] Manifest paths stay relative, so a folder moved by hand (same layout) keeps working
- [ ] User to try: move the library to another drive and back

### M7 — Localisation + dark mode (requested 2026-09-13)
- [x] All UI text in one place (`Core/Localisation/Strings`), English + Arabic side by side; a test
      fails if any key lacks a translation. XAML uses `{s:Loc key}`, code uses `Strings.Get`
- [x] **Arabic** UI with full RTL layout (window `FlowDirection`, mirrored nav pane, Arabic UI font)
- [x] Direction follows the *content*, not the UI: an English translation and a Latin reciter name stay
      left-to-right inside an Arabic layout; hex codes, paths and measurements likewise (`IsRightToLeftText`)
- [x] Language picker on Appearance (System / English / العربية); the screensaver's chip, verse counter
      and reciter caption follow it
- [x] **Dark mode**: `ThemePalette` swaps every brush at runtime (Theme.xaml uses `DynamicResource`),
      and `Window.ThemeMode` drives WPF's own Fluent controls — applied after `InitializeComponent`,
      which is what makes combo boxes and scroll bars follow
- [x] Theme picker on Appearance (System / Light / Dark); System reads Windows' `AppsUseLightTheme`
- [x] Changing either rebuilds the shell in place, keeping the open page
- [x] Verified on screen 2026-09-18: settings and screensaver in Arabic dark
- [ ] Windows accent colour as an option (deferred; the green accent is the app's identity)

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
| Quran.com advertises audio the CDN does not host → the MP3 URL 404s even though the API lists the file (confirmed 2026-09-13: reciter 8 Minshawi Mujawwad, `siddiq_al-minshawi/mujawwad/001.mp3`) | M5b: a 404 marks that surah unavailable, is not retried, and never aborts the job; unavailable surahs are remembered and shown as such |
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
| 2026-09-18 | One in-code string table rather than RESX satellite assemblies | The two languages sit side by side so a gap is obvious, and a unit test can fail on any untranslated key |
| 2026-09-18 | Text direction is decided per string by its own letters, not by the UI language | A translation, a reciter's Latin name, a file path and a hex code each keep their natural direction inside an Arabic layout |
| 2026-09-17 | Quran.com surah recordings are treated uniformly: only Al-Fatihah contains the basmalah | Confirmed by the user; per-file special-casing would be unmaintainable across 114 surahs × every reciter |
| 2026-09-17 | The isti'ādhah and basmalah are not settings — no toggles, no file pickers | They are part of how the Qur'an is recited, not a matter of taste; making them optional would invite reciting without them |
| 2026-09-17 | "Continue where it left off" now applies to shuffled play as well as sequential | The order is reshuffled every launch, but the setting is about carrying on the surah you were hearing, which is just as meaningful when shuffled |
| 2026-09-17 | Audio from outside Quran.com is loudness-matched to the recitation rather than trusted to be level | The isti'ādhah measured 2.5× louder than the recitation; a jump in volume is jarring at night |
| 2026-09-16 | The output device is pinned by endpoint id, and a missing one silently falls back to the system default | A screensaver must never be silent because a speaker was unplugged; the Settings page says when the choice is not currently connected |
| 2026-09-14 | Downloads are per-reciter (Reciters → reciter page), separate from playback selection (Library, Surahs) | "What is on disk" and "what plays" are different questions; conflating them made downloads follow the surah selection |
| 2026-09-14 | A 404 is data, not an error: remembered as unavailable, never retried, never aborts a batch | Quran.com lists recordings its CDN does not host; one bad file must not stop a 114-surah download |
| 2026-09-13 | Settings app on .NET 10's built-in Fluent theme + a few in-house controls (nav pane, toggle switch, settings card), superseding WPF UI (lepo.co) | Covers the design with one fewer dependency; the pieces WPF UI added (NavigationView, ToggleSwitch) are small to write and fully under our control |
