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
- Settings: `%APPDATA%\IslamicScreensaver\settings.json`. Cache: `%LOCALAPPDATA%\IslamicScreensaver\cache\`.
