# Third-party notices

Islamic Screensaver is released under the **GNU General Public License v3** — see [LICENSE](LICENSE).
That licence covers this project's own code. The parts below come from elsewhere and keep their own
terms; every installed copy carries their full licence texts in its `licenses` folder.

## Software included in every installed copy

| Component | Licence | Text |
|---|---|---|
| [.NET Runtime](https://github.com/dotnet/runtime) 10.0.12, by Microsoft and the .NET Foundation | MIT | [licenses/dotnet-runtime.LICENSE.txt](licenses/dotnet-runtime.LICENSE.txt), with its own third-party notices in [licenses/dotnet-runtime.THIRD-PARTY-NOTICES.txt](licenses/dotnet-runtime.THIRD-PARTY-NOTICES.txt) |
| [Windows Desktop Runtime](https://github.com/dotnet/wpf) 10.0.12 (WPF), by Microsoft and the .NET Foundation | MIT | [licenses/dotnet-windowsdesktop.LICENSE.txt](licenses/dotnet-windowsdesktop.LICENSE.txt) |
| [NAudio](https://github.com/naudio/NAudio), © 2008–2026 Mark Heath | MIT | [licenses/NAudio.LICENSE.txt](licenses/NAudio.LICENSE.txt) |

The setup program is built with [Inno Setup](https://jrsoftware.org/isinfo.php), © 1997–2026 Jordan
Russell and Martijn Laan. Its licence asks nothing of the programs it installs; this is an
acknowledgement it says would be appreciated.

## Qur'an content

None of the recitations, Qur'an text or translations are part of this project or covered by its
licence. The screensaver downloads them, on the user's own machine, from the services that publish
them, and they remain under those services' terms:

- **[Quran.com](https://quran.com)** — recitations with word timings, the Uthmani text, chapter
  information and translations, through its public API. Translations carry their translators'
  copyrights (the default, Saheeh International, belongs to its publisher).
- **[MP3 Quran](https://mp3quran.net)** — recitations and ayah timings, through its public API.

`assets/catalog/catalog.json`, which ships with the app, is a list of reciters and of which surahs
each recording has. It is compiled from those two APIs by `tools/CatalogBuilder`.

## Recordings bundled with the app

`assets/audio/istiadhah.mp3` and `assets/audio/basmalah.mp3` are played before recitation when a
reciter's own isti'ādhah or basmalah is not available. **Their source and terms are still to be
confirmed**, and must be before any public release.
