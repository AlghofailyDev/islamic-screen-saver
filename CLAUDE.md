# Islamic Screensaver

Native Windows 11 `.scr` in C# / WPF (.NET 10) that plays Quran recitation over a calm background
with the recited verse shown in a frosted-glass panel, plus a settings app.

**Read `PLAN.md` first.** It holds the decided stack, architecture, design direction, milestone
checklists, and decision log. Work milestone by milestone; tick items off in `PLAN.md` as they land.
If a decision changes, add a row to the decision log instead of rewriting history.

## Conventions
- `IslamicScreensaver.Core` has no WPF dependency and is where all testable logic lives.
- The screensaver (`/s` mode) never touches the network; only the settings app downloads.
- Verse key format is `surah:ayah` (e.g. `2:255`) everywhere.
- Settings: `%APPDATA%\IslamicScreensaver\settings.json`. Downloads default to
  `%LOCALAPPDATA%\IslamicScreensaver\cache\` but the user can move them — always resolve the root with
  `ContentCache.For(settings)`, never `AppPaths.CacheDirectory` directly.
- Quran.com lists recordings its CDN does not host. A 404 is expected data: mark it unavailable, skip
  it, carry on — never abort a batch or surface a raw HTTP message.
- No user-visible string is written inline. Add a key to `Core/Localisation/Strings` with both English
  and Arabic (a test fails otherwise), then use `{s:Loc key}` in XAML or `Strings.Get` in code.
- Theme brushes are referenced with `DynamicResource`, never `StaticResource`, so dark mode can swap
  them at runtime. Content keeps its own text direction — see `Strings.IsRightToLeftText`.
