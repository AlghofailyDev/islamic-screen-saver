namespace IslamicScreensaver.CatalogBuilder;

/// <summary>
/// Which person each source's recording belongs to.
///
/// mp3quran.net already groups recordings under one reciter, so its reciter id is the person and we
/// build the canonical id from it. Quran.com does not: it has twelve recordings and no notion of a
/// reciter beyond a name string, and its spellings ("Mishari Rashid al-`Afasy") match no automatic
/// rule against mp3quran's ("Mishary Alafasi"). Twelve rows written by hand beat a fuzzy matcher that
/// would quietly merge two different people.
/// </summary>
internal static class ReciterAliases
{
    /// <summary>Quran.com recitation id → the mp3quran.net reciter id for the same person.</summary>
    private static readonly Dictionary<int, int> QuranComToMp3Quran = new()
    {
        [1] = 51,   // AbdulBaset AbdulSamad (Mujawwad)  → عبدالباسط عبدالصمد
        [2] = 51,   // AbdulBaset AbdulSamad (Murattal)
        [3] = 54,   // Abdur-Rahman as-Sudais            → عبدالرحمن السديس
        [4] = 4,    // Abu Bakr al-Shatri                → شيخ أبو بكر الشاطري
        [5] = 89,   // Hani ar-Rifai                     → هاني الرفاعي
        [6] = 118,  // Mahmoud Khalil Al-Husary          → محمود خليل الحصري
        [7] = 123,  // Mishari Rashid al-`Afasy          → مشاري العفاسي
        [8] = 112,  // Mohamed Siddiq al-Minshawi (Mujawwad) → محمد صديق المنشاوي
        [9] = 112,  // Mohamed Siddiq al-Minshawi (Murattal)
        [10] = 31,  // Sa`ud ash-Shuraym                 → سعود الشريم
        [11] = 106, // Mohamed al-Tablawi                → محمد الطبلاوي
        [12] = 118, // Mahmoud Khalil Al-Husary (Muallim)
    };

    /// <summary>
    /// The canonical reciter id. Derived from the mp3quran id rather than the name, because a name is
    /// spelled four ways and can change; an id in a published catalog must never move.
    /// </summary>
    public static string CanonicalId(int mp3QuranReciterId) => $"mq-{mp3QuranReciterId}";

    /// <summary>The person a Quran.com recitation belongs to, or null when we have not mapped it yet.</summary>
    public static string? ForQuranCom(int recitationId) =>
        QuranComToMp3Quran.TryGetValue(recitationId, out var reciter) ? CanonicalId(reciter) : null;

    public static IReadOnlyCollection<int> MappedQuranComIds => QuranComToMp3Quran.Keys;
}
