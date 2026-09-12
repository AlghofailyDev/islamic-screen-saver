using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

/// <summary>Payload shapes captured from the live API v4 on 2026-09-13, trimmed to the fields we map.</summary>
public class QuranApiParserTests
{
    [Fact]
    public void ParseSurahAudio_MapsUrlTimingsAndWordSegments()
    {
        const string json = """
            {"audio_file":{"id":911,"chapter_id":1,"file_size":839808.0,"format":"mp3",
             "audio_url":"https://download.quranicaudio.com/qdc/mishari_al_afasy/murattal/1.mp3",
             "timestamps":[
               {"verse_key":"1:2","timestamp_from":6090,"timestamp_to":11680,"duration":5590,"segments":[[1,6025,7025],[2,7025,7885],[3,7885,8515],[4,8515,11550]]},
               {"verse_key":"1:1","timestamp_from":0,"timestamp_to":6090,"duration":6090,"segments":[[1,0,1000]]}
             ]}}
            """;

        var audio = QuranApiParser.ParseSurahAudio(json, reciterId: 7, surah: 1);

        Assert.Equal(7, audio.ReciterId);
        Assert.Equal(1, audio.Surah);
        Assert.Equal("https://download.quranicaudio.com/qdc/mishari_al_afasy/murattal/1.mp3", audio.AudioUrl);
        Assert.Equal(839808, audio.FileSize);
        Assert.Equal(2, audio.Timings.Count);
        // Sorted by verse even though the payload was not.
        Assert.Equal(new VerseKey(1, 1), audio.Timings[0].Key);
        var second = audio.Timings[1];
        Assert.Equal(6090, second.FromMs);
        Assert.Equal(11680, second.ToMs);
        Assert.Equal(4, second.Words.Count);
        Assert.Equal(new WordSegment(2, 7025, 7885), second.Words[1]);
    }

    [Fact]
    public void ParseSurahAudio_WithoutAudioUrl_Throws()
    {
        Assert.Throws<FormatException>(() => QuranApiParser.ParseSurahAudio("""{"audio_file":{"id":1}}""", 7, 1));
    }

    [Fact]
    public void ParseSurahText_KeepsWordsAndDropsEndMarker()
    {
        const string json = """
            {"verses":[{"id":2,"verse_key":"1:2","text_uthmani":"ٱلْحَمْدُ لِلَّهِ رَبِّ ٱلْعَـٰلَمِينَ",
              "words":[{"position":1,"char_type_name":"word","text_uthmani":"ٱلْحَمْدُ"},
                       {"position":2,"char_type_name":"word","text_uthmani":"لِلَّهِ"},
                       {"position":3,"char_type_name":"word","text_uthmani":"رَبِّ"},
                       {"position":4,"char_type_name":"word","text_uthmani":"ٱلْعَـٰلَمِينَ"},
                       {"position":5,"char_type_name":"end","text_uthmani":"٢"}]}]}
            """;

        var verses = QuranApiParser.ParseSurahText(json);

        var verse = Assert.Single(verses);
        Assert.Equal(new VerseKey(1, 2), verse.Key);
        Assert.Equal("ٱلْحَمْدُ لِلَّهِ رَبِّ ٱلْعَـٰلَمِينَ", verse.TextUthmani);
        Assert.Equal(["ٱلْحَمْدُ", "لِلَّهِ", "رَبِّ", "ٱلْعَـٰلَمِينَ"], verse.Words);
    }

    [Fact]
    public void ParseSurahTranslation_StripsFootnotes_KeepsVerseOrder()
    {
        const string json = """
            {"translations":[{"resource_id":20,"text":"First verse text,<sup foot_note=197829>1</sup>"},
                             {"resource_id":20,"text":"Second &amp; verse."}],
             "meta":{"translation_name":"Test","author_name":"Test"}}
            """;

        var t = QuranApiParser.ParseSurahTranslation(json, resourceId: 20, surah: 112);

        Assert.Equal(20, t.ResourceId);
        Assert.Equal(112, t.Surah);
        Assert.Equal(["First verse text,", "Second & verse."], t.Texts);
    }

    [Fact]
    public void ParseReciters_SortsByName_NullsEmptyStyle()
    {
        const string json = """
            {"recitations":[{"id":3,"reciter_name":"Zed","style":null,"translated_name":{"name":"Zed","language_name":"english"}},
                            {"id":2,"reciter_name":"Abc","style":"Murattal","translated_name":{"name":"Abc","language_name":"english"}}]}
            """;

        var reciters = QuranApiParser.ParseReciters(json);

        Assert.Equal([2, 3], reciters.Select(r => r.Id));
        Assert.Equal("Abc (Murattal)", reciters[0].DisplayName);
        Assert.Null(reciters[1].Style);
        Assert.Equal("Zed", reciters[1].DisplayName);
    }

    [Fact]
    public void ParseChapters_MapsNamesAndCounts()
    {
        const string json = """
            {"chapters":[{"id":1,"bismillah_pre":false,"name_simple":"Al-Fatihah","name_arabic":"الفاتحة","verses_count":7,"translated_name":{"language_name":"english","name":"The Opener"}}]}
            """;

        var chapter = Assert.Single(QuranApiParser.ParseChapters(json));

        Assert.Equal(new Chapter(1, "الفاتحة", "Al-Fatihah", "The Opener", 7, false), chapter);
    }

    [Fact]
    public void ParseTranslations_MapsResources()
    {
        const string json = """
            {"translations":[{"id":20,"name":"Saheeh International","author_name":"Saheeh International","slug":"en-sahih-international","language_name":"english"}]}
            """;

        var t = Assert.Single(QuranApiParser.ParseTranslations(json));
        Assert.Equal(new TranslationResource(20, "Saheeh International", "Saheeh International", "english"), t);
    }
}
