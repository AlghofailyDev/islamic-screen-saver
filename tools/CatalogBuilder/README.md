# Catalog builder

Builds `assets/catalog/catalog.json` — the list of every reciter, their recordings and the surahs
each recording really has. The app reads this file instead of asking quran.com and mp3quran.net at
runtime, so a user's library never depends on a third-party API being up, and a recording that has
quietly disappeared stops being offered instead of failing at download.

Nothing here ships. Run it when the sources change, read the diff, commit.

```
dotnet run --project tools/CatalogBuilder
```

| Option | Effect |
|---|---|
| `--out <path>` | Write somewhere other than `assets/catalog/catalog.json` |
| `--no-probe` | Trust quran.com's chapter list instead of asking the CDN for each of its 1,368 files. Seconds instead of half a minute, but publishes surahs that will 404 |
| `--no-cache` | Refetch everything. Responses are otherwise cached under `%TEMP%\IslamicScreensaver.CatalogBuilder`, so a re-run is nearly free |

## What it does

**mp3quran.net** gives a reciter and their *moshaf* — one recording per riwāyah and style, each with
its own surah list. A second endpoint says which surahs of which have ayah timings. Every recording
is published, timed or not; each says for itself which of its surahs the verse panel can follow.

**quran.com** gives twelve recordings, all Ḥafṣ ʿan ʿĀṣim and all timed word by word — the only
source that can light the word being recited. Its chapter list is aspirational, so every file is
asked for before it is published.

The two are joined by `ReciterAliases`, twelve rows written by hand. Their spellings match no
automatic rule ("Mishari Rashid al-`Afasy" against "Mishary Alafasi"), and a fuzzy matcher that
merged two different reciters would be worse than a table that needs a line added now and then.

## Things the sources get wrong, and what is done about them

- **quran.com recitation 8** (Minshawi, Mujawwad) is listed in full and hosts nothing at all — the
  source of the 404s users used to hit. The probe drops it. The alias row stays for the day it returns.
- **mp3quran files two styles as riwāyāt.** "المصحف المجود" and "المصحف المعلم" are ids 21 and 22 in
  its riwāyāt table, but both are Ḥafṣ. Published as-is they would split one reciter across two
  riwāyāt. `Riwayat.Describe` moves them to the style.
- **The basmalah is in most mp3quran recordings but only some of them say so.** Roughly three in four
  label it "ayah 0"; the rest leave it untimed and start verse 1 several seconds in — Shatri at 7.9s,
  al-ʿAfasy at 6.7s. Reading the ayah number alone would have called those "no basmalah" and had the
  app play a second one over the first. Both shapes are treated as yes; a lead-in under a second is a
  no (five recordings genuinely open without one). Untimed recordings borrow the answer from another
  recording by the same reciter, and stay `null` when there is none.
- **Read ids and moshaf ids are the same number** on mp3quran — checked across all 115 timed reads —
  so one `mp3quran:{id}` addresses both the audio and its timings.
