using System.Globalization;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Core.Localisation;

/// <summary>
/// Every piece of text the app shows, in English and Arabic. Kept as a dictionary rather than RESX so
/// the two languages sit side by side and a missing translation is obvious at a glance.
/// </summary>
public static class Strings
{
    private static readonly Dictionary<string, (string En, string Ar)> Table = new()
    {
        // Shell
        ["app.name"] = ("Islamic Screensaver", "شاشة توقف إسلامية"),
        ["nav.library"] = ("Library", "المكتبة"),
        ["nav.reciters"] = ("Reciters", "القرّاء"),
        ["nav.surahs"] = ("Surahs", "السور"),
        ["nav.audio"] = ("Audio", "الصوت"),
        ["nav.background"] = ("Background", "الخلفية"),
        ["nav.appearance"] = ("Appearance", "المظهر"),
        ["nav.translation"] = ("Translation", "الترجمة"),
        ["nav.about"] = ("About", "حول"),
        ["shell.run"] = ("Run screensaver now", "تشغيل شاشة التوقف الآن"),

        // Common
        ["common.choose"] = ("Choose…", "اختر…"),
        ["common.open"] = ("Open", "فتح"),
        ["common.reset"] = ("Reset", "إعادة تعيين"),
        ["common.change"] = ("Change…", "تغيير…"),
        ["common.download"] = ("Download", "تنزيل"),
        ["common.downloaded"] = ("Downloaded", "تم التنزيل"),
        ["common.cancel"] = ("Cancel", "إلغاء"),
        ["common.refresh"] = ("Refresh", "تحديث"),
        ["common.selectAll"] = ("Select all", "تحديد الكل"),
        ["common.clear"] = ("Clear", "مسح"),
        ["common.delete"] = ("Delete", "حذف"),
        ["common.test"] = ("Test", "اختبار"),
        ["common.stop"] = ("Stop", "إيقاف"),
        ["common.listen"] = ("Listen", "استماع"),
        ["common.on"] = ("On", "مفعّل"),
        ["common.off"] = ("Off", "معطّل"),
        ["common.verses"] = ("{0} verses", "{0} آية"),
        ["common.openFolder"] = ("Open folder", "فتح المجلد"),
        ["common.megabytes"] = ("{0} MB", "{0} ميجابايت"),

        // Library
        ["library.title"] = ("Library", "المكتبة"),
        ["library.subtitle"] = ("What is downloaded on this PC. Tick a reciter to include them in playback.",
                                "ما تم تنزيله على هذا الجهاز. اختر قارئًا لإدراجه في التشغيل."),
        ["library.rotate"] = ("Rotate reciters", "تناوب القرّاء"),
        ["library.rotate.desc"] = ("Each ticked reciter takes one surah in turn", "كل قارئ مُحدَّد يتلو سورة بالتناوب"),
        ["library.add"] = ("Add a reciter", "إضافة قارئ"),
        ["library.empty"] = ("Nothing downloaded yet", "لم يتم تنزيل أي شيء بعد"),
        ["library.empty.desc"] = ("Browse the Reciters page to find a reciter and download the surahs you want.",
                                  "تصفّح صفحة القرّاء لاختيار قارئ وتنزيل السور التي تريدها."),
        ["library.browse"] = ("Browse reciters", "تصفّح القرّاء"),
        ["library.storage"] = ("Storage", "التخزين"),
        ["library.location"] = ("Where downloads are kept", "مكان حفظ التنزيلات"),
        ["library.surahsOf"] = ("{0} of 114 surahs", "{0} من 114 سورة"),

        // Reciters
        ["reciters.title"] = ("Reciters", "القرّاء"),
        ["reciters.subtitle"] = ("Open a reciter to see their recitations, choose one to hear and download the surahs you want.",
                                 "افتح قارئًا لعرض تلاواته، واختر ما تستمع إليه ونزّل السور التي تريدها."),
        ["reciters.empty"] = ("The reciter catalog is missing. Reinstall the app, or click Refresh list.",
                              "قائمة القرّاء غير موجودة. أعد تثبيت التطبيق أو اضغط تحديث القائمة."),
        ["reciters.search"] = ("Search reciters", "ابحث عن قارئ"),
        ["reciters.refresh"] = ("Refresh list", "تحديث القائمة"),
        ["reciters.count"] = ("{0} reciters", "{0} قارئًا"),
        ["reciters.nothingDownloaded"] = ("Nothing downloaded", "لم يُنزَّل شيء"),
        ["reciters.allSurahs"] = ("All 114 surahs", "جميع السور 114"),
        ["reciters.someDownloaded"] = ("{0} of 114 downloaded", "{0} من 114 تم تنزيلها"),
        ["reciters.selectMissing"] = ("Select missing", "تحديد غير المنزَّل"),
        ["reciters.searchSurahs"] = ("Search surahs", "ابحث عن سورة"),
        ["reciters.downloadSelected"] = ("Download selected", "تنزيل المحدد"),
        ["reciters.deleteSelected"] = ("Delete selected", "حذف المحدد"),
        ["reciters.notDownloaded"] = ("Not downloaded", "غير منزَّل"),
        ["reciters.downloading"] = ("Downloading…", "جارٍ التنزيل…"),
        ["reciters.notAvailable"] = ("Not available", "غير متوفر"),

        // Recitations — one reciter can have several, by riwāyah and style
        ["recitation.one"] = ("1 recitation", "تلاوة واحدة"),
        ["recitation.many"] = ("{0} recitations", "{0} تلاوات"),
        ["recitation.choose"] = ("Choose a recitation", "اختر تلاوة"),
        ["recitation.chooseSurahs"] = ("Choose surahs", "اختر السور"),
        ["recitation.timing.word"] = ("Follows each word", "يتابع كل كلمة"),
        ["recitation.timing.verse"] = ("Follows each verse", "يتابع كل آية"),
        ["recitation.timing.none"] = ("No verse sync", "بدون تزامن الآيات"),
        ["recitation.timing.partial"] = ("{0} of {1} surahs sync", "{0} من {1} سورة متزامنة"),
        ["recitation.surahsAvailable"] = ("{0} surahs available", "{0} سورة متاحة"),
        ["recitation.downloadedOf"] = ("{0} of {1} downloaded", "{0} من {1} تم تنزيلها"),
        ["recitation.playing"] = ("In playback", "ضمن التشغيل"),
        ["recitation.notPlaying"] = ("Not in playback", "ليست ضمن التشغيل"),
        ["recitation.source.qurancom"] = ("Quran.com", "Quran.com"),
        ["recitation.source.mp3quran"] = ("MP3 Quran", "MP3 Quran"),
        ["recitation.noneSelected"] = ("No recitation chosen yet", "لم تُختَر أي تلاوة بعد"),
        ["recitation.basmalahIncluded"] = ("Opens with the basmalah", "تبدأ بالبسملة"),

        // Surahs
        ["surahs.title"] = ("Surahs", "السور"),
        ["surahs.subtitle"] = ("Which surahs play, and in what order.", "أي السور تُتلى، وبأي ترتيب."),
        ["surahs.order"] = ("Order", "الترتيب"),
        ["surahs.order.desc"] = ("Straight through, or shuffled each time the screensaver starts",
                                 "بالتسلسل، أو عشوائيًا في كل مرة تبدأ فيها شاشة التوقف"),
        ["surahs.sequential"] = ("Sequential", "بالتسلسل"),
        ["surahs.shuffled"] = ("Shuffled", "عشوائي"),
        ["surahs.resume"] = ("Continue where it left off", "المتابعة من حيث توقفت"),
        ["surahs.resume.desc"] = ("Play resumes from the last surah instead of restarting",
                                  "يستأنف التشغيل من آخر سورة بدل البدء من جديد"),
        ["surahs.allSelected"] = ("All 114 selected", "تم تحديد جميع السور 114"),
        ["surahs.someSelected"] = ("{0} of {1} selected", "{0} من {1} محددة"),

        // Audio
        ["audio.title"] = ("Audio", "الصوت"),
        ["audio.subtitle"] = ("How loud the recitation is, where it plays, and how closely the text follows it.",
                              "مستوى صوت التلاوة، وأين تُشغَّل، ومدى تطابق النص معها."),
        ["audio.output"] = ("Output", "مخرج الصوت"),
        ["audio.playThrough"] = ("Play through", "التشغيل عبر"),
        ["audio.playThrough.desc"] = ("Leave on System default to follow Windows, or pick the speakers you want the screensaver to use",
                                      "اتركه على الافتراضي ليتبع ويندوز، أو اختر السماعات التي تريدها لشاشة التوقف"),
        ["audio.systemDefault"] = ("System default", "الافتراضي للنظام"),
        ["audio.notConnected"] = ("{0} (not connected)", "{0} (غير متصل)"),
        ["audio.test.desc"] = ("Plays a few seconds of a downloaded surah through the chosen device",
                               "يشغّل بضع ثوانٍ من سورة منزَّلة عبر الجهاز المختار"),
        ["audio.playback"] = ("Playback", "التشغيل"),
        ["audio.volume"] = ("Volume", "مستوى الصوت"),
        ["audio.override"] = ("Turn the speakers up if they are down", "ارفع صوت السماعات إن كان منخفضًا"),
        ["audio.override.desc"] = ("Raise the system volume when the screensaver starts if it is muted or very low, and put it back on exit",
                                   "ارفع مستوى صوت النظام عند بدء شاشة التوقف إن كان صامتًا أو منخفضًا جدًا، وأعده كما كان عند الخروج"),
        ["audio.override.floor"] = ("Raise to", "ارفعه إلى"),
        ["audio.volume.desc"] = ("How loud the recitation plays, within the system volume",
                                 "مدى ارتفاع صوت التلاوة ضمن مستوى صوت النظام"),
        ["audio.timing"] = ("Text timing", "توقيت النص"),
        ["audio.timing.desc"] = ("Nudge the verse text later (+) or earlier (−) if it does not match a reciter",
                                 "أخّر النص (+) أو قدّمه (−) إن لم يطابق تلاوة القارئ"),

        // Background
        ["background.title"] = ("Background", "الخلفية"),
        ["background.subtitle"] = ("A calm visual behind the verse. Bundled scenes drift slowly; your own photo or video works too.",
                                   "مشهد هادئ خلف الآية. المشاهد المضمّنة تتحرك ببطء، ويمكنك اختيار صورة أو مقطع من عندك."),
        ["background.bundled"] = ("Bundled", "مضمّنة"),
        ["background.dawn"] = ("Dawn", "الفجر"),
        ["background.night"] = ("Night", "الليل"),
        ["background.yourOwn"] = ("Your own", "من عندك"),
        ["background.photoOrVideo"] = ("Photo or video", "صورة أو مقطع"),
        ["background.noFile"] = ("No file chosen", "لم يتم اختيار ملف"),
        ["background.fileTypes"] = ("Images: JPG, PNG, BMP, WebP. Video: MP4, MOV, WMV, MKV (muted, loops).",
                                    "الصور: JPG وPNG وBMP وWebP. الفيديو: MP4 وMOV وWMV وMKV (بلا صوت، متكرر)."),
        ["background.monitors"] = ("Monitors", "الشاشات"),
        ["background.mirror"] = ("Show the verse on every monitor", "إظهار الآية على كل الشاشات"),
        ["background.mirror.desc"] = ("Otherwise other monitors show only the background", "وإلا فستعرض الشاشات الأخرى الخلفية فقط"),

        // Appearance
        ["appearance.title"] = ("Appearance", "المظهر"),
        ["appearance.subtitle"] = ("How the verse panel looks over your background.", "شكل لوحة الآية فوق الخلفية."),
        ["appearance.preview"] = ("L I V E   P R E V I E W", "م ع ا ي ن ة   ح ي ة"),
        ["appearance.resetDefaults"] = ("Reset to defaults", "استعادة الافتراضي"),
        ["appearance.panel"] = ("Panel", "اللوحة"),
        ["appearance.blur"] = ("Blur strength", "شدة الضبابية"),
        ["appearance.blur.desc"] = ("How much the background is softened behind the panel", "مقدار تنعيم الخلفية خلف اللوحة"),
        ["appearance.grain"] = ("Grain", "الحبيبات"),
        ["appearance.grain.desc"] = ("A fine texture over the glass", "نسيج دقيق فوق الزجاج"),
        ["appearance.tint"] = ("Panel tint", "لون اللوحة"),
        ["appearance.tint.desc"] = ("Automatic picks a colour from the background", "التلقائي يختار لونًا من الخلفية"),
        ["appearance.automatic"] = ("Automatic", "تلقائي"),
        ["appearance.custom"] = ("Custom", "مخصص"),
        ["appearance.opacity"] = ("Panel opacity", "شفافية اللوحة"),
        ["appearance.opacity.desc"] = ("How strongly the tint covers the blurred background", "مدى تغطية اللون للخلفية الضبابية"),
        ["appearance.text"] = ("Text", "النص"),
        ["appearance.arabicColour"] = ("Arabic text colour", "لون النص العربي"),
        ["appearance.translationColour"] = ("Translation text colour", "لون نص الترجمة"),
        ["appearance.position"] = ("Verse panel", "لوحة الآيات"),
        ["appearance.position.desc"] = ("Where the verses appear, or turn the panel off for the background alone",
                                        "مكان ظهور الآيات، أو أخفِ اللوحة للاكتفاء بالخلفية"),
        ["appearance.position.top"] = ("Top of the screen", "أعلى الشاشة"),
        ["appearance.position.middle"] = ("Middle of the screen", "وسط الشاشة"),
        ["appearance.position.bottom"] = ("Bottom of the screen", "أسفل الشاشة"),
        ["appearance.position.hidden"] = ("Hidden — background only", "مخفية — الخلفية فقط"),
        ["appearance.wordHighlight"] = ("Word highlight", "إبراز الكلمة"),
        ["appearance.wordHighlight.desc"] = ("Light up the word being recited", "إضاءة الكلمة التي تُتلى"),
        ["appearance.theme"] = ("Theme", "السمة"),
        ["appearance.theme.desc"] = ("Light or dark chrome for this settings window", "مظهر فاتح أو داكن لنافذة الإعدادات"),
        ["appearance.theme.system"] = ("Use Windows setting", "حسب إعداد ويندوز"),
        ["appearance.theme.light"] = ("Light", "فاتح"),
        ["appearance.theme.dark"] = ("Dark", "داكن"),
        ["appearance.language"] = ("Language", "اللغة"),
        ["appearance.language.desc"] = ("Language of this app — the translation shown on screen is chosen on the Translation page",
                                        "لغة هذا التطبيق — أما الترجمة المعروضة فتُختار من صفحة الترجمة"),
        ["appearance.language.system"] = ("Use Windows setting", "حسب إعداد ويندوز"),

        // Translation
        ["translation.title"] = ("Translation", "الترجمة"),
        ["translation.subtitle"] = ("Show a translation beneath the Arabic, in the language you choose.",
                                    "إظهار ترجمة تحت النص العربي، باللغة التي تختارها."),
        ["translation.show"] = ("Show translation", "إظهار الترجمة"),
        ["translation.language"] = ("Language", "اللغة"),
        ["translation.which"] = ("Translation", "الترجمة"),
        ["translation.which.desc"] = ("Translations are fetched from Quran.com and cached on this PC",
                                      "تُجلب الترجمات من Quran.com وتُحفظ على هذا الجهاز"),

        // About
        ["about.title"] = ("About", "حول"),
        ["about.content"] = ("Content", "المحتوى"),
        ["about.storage"] = ("Storage", "التخزين"),
        ["about.downloadedContent"] = ("Downloaded content", "المحتوى المنزَّل"),
        ["about.settingsFile"] = ("Settings file", "ملف الإعدادات"),
        ["about.logs"] = ("Logs", "السجلات"),
        ["about.logs.desc"] = ("Useful if something misbehaves", "مفيدة إذا حدث خلل"),
        ["about.clearCache"] = ("Clear cache", "مسح التنزيلات"),

        // Status and messages
        ["status.recitation"] = ("Recitation", "تلاوة"),
        ["status.reciterN"] = ("Reciter {0}", "قارئ {0}"),
        ["status.finishing"] = ("Finishing…", "جارٍ الإنهاء…"),
        ["status.preparing"] = ("Preparing…", "جارٍ التحضير…"),
        ["status.downloadingItem"] = ("Downloading {0}", "جارٍ تنزيل {0}"),
        ["status.downloadingOf"] = ("Downloading {0}  ·  {1} of {2}", "جارٍ تنزيل {0}  ·  {1} من {2}"),
        ["status.doneOf"] = ("{0} of {1} done", "{0} من {1} اكتملت"),
        ["status.movingFile"] = ("Moving {0}  ·  {1} of {2}", "جارٍ نقل {0}  ·  {1} من {2}"),
        ["status.mbOnDisk"] = ("{0} MB on disk  ·  {1}", "{0} ميجابايت على القرص  ·  {1}"),
        ["status.downloadedReciters"] = ("Downloaded reciters", "القرّاء المنزَّلون"),
        ["status.recitersSurahs"] = ("{0} reciters  ·  {1} surahs", "{0} قرّاء  ·  {1} سورة"),
        ["status.defaultLocation"] = ("default location", "الموقع الافتراضي"),
        ["status.selectedCount"] = ("{0} selected", "{0} محددة"),
        ["status.allDownloaded"] = ("All 114 surahs downloaded", "تم تنزيل جميع السور 114"),
        ["status.allAvailableDownloaded"] = ("All {0} available surahs downloaded", "تم تنزيل جميع السور المتاحة ({0})"),
        ["status.nothingDownloadedYet"] = ("Nothing downloaded yet", "لم يُنزَّل شيء بعد"),
        ["status.downloadedOf"] = ("{0} of {1} surahs downloaded", "{0} من {1} سورة تم تنزيلها"),
        ["status.selectBelow"] = ("Select surahs below, or use Select all / Select missing.",
                                  "اختر السور أدناه، أو استخدم تحديد الكل / تحديد غير المنزَّل."),
        ["status.notHosted"] = ("{0} surahs are not hosted by Quran.com for this reciter.",
                                "{0} سورة غير متوفرة لدى Quran.com لهذا القارئ."),
        ["status.alreadyDownloaded"] = ("Everything selected is already downloaded.", "كل ما هو محدد تم تنزيله بالفعل."),
        ["status.toDownload"] = ("{0} selected surahs to download", "{0} سورة محددة للتنزيل"),
        ["status.downloadN"] = ("Download {0}", "تنزيل {0}"),
        ["status.deleteN"] = ("Delete {0}", "حذف {0}"),
        ["status.savedDevice"] = ("Saved device", "الجهاز المحفوظ"),
        ["status.deviceNotConnected"] = ("“{0}” isn't connected right now — the screensaver will use the system default until it is back.",
                                         "«{0}» غير متصل حاليًا — ستستخدم شاشة التوقف الجهاز الافتراضي حتى يعود."),

        // Dialogs
        ["dialog.downloadFailed"] = ("Download failed", "فشل التنزيل"),
        ["dialog.refreshFailed"] = ("Refresh failed", "فشل التحديث"),
        ["dialog.cannotReach"] = ("Could not reach Quran.com. Check your connection and try again.",
                                  "تعذّر الوصول إلى Quran.com. تحقق من اتصالك وحاول مجددًا."),
        ["dialog.nothingDownloadedYet"] = ("Nothing downloaded yet", "لم يُنزَّل شيء بعد"),
        ["dialog.downloadFirst"] = ("Download a surah first, then you can listen.", "نزّل سورة أولًا، ثم يمكنك الاستماع."),
        ["dialog.downloadFirstTest"] = ("Download a surah first, then you can test the output here.",
                                        "نزّل سورة أولًا، ثم يمكنك اختبار المخرج هنا."),
        ["dialog.nothingToPlay"] = ("Nothing to play", "لا يوجد ما يُشغَّل"),
        ["dialog.someSkipped"] = ("Some surahs were skipped", "تم تخطي بعض السور"),
        ["dialog.deleteDownloads"] = ("Delete downloads", "حذف التنزيلات"),
        ["dialog.deleteReciterAsk"] = ("Delete all {0} downloaded surahs by {1} from this PC?",
                                       "حذف كل السور المنزَّلة ({0}) للقارئ {1} من هذا الجهاز؟"),
        ["dialog.deleteSelectedAsk"] = ("Delete {0} downloaded surahs by {1} from this PC?",
                                        "حذف {0} سورة منزَّلة للقارئ {1} من هذا الجهاز؟"),
        ["dialog.chooseLocation"] = ("Choose where downloads are kept", "اختر مكان حفظ التنزيلات"),
        ["dialog.cannotUseFolder"] = ("Cannot use that folder", "تعذّر استخدام هذا المجلد"),
        ["dialog.moveAsk"] = ("Move the {0} MB already downloaded to:\n\n{1}\n\nChoose No to leave it where it is and start fresh in the new folder.",
                              "نقل {0} ميجابايت المنزَّلة إلى:\n\n{1}\n\nاختر «لا» لتركها مكانها والبدء من جديد في المجلد الجديد."),
        ["dialog.movePrompt"] = ("Move downloads?", "نقل التنزيلات؟"),
        ["dialog.moveFailed"] = ("Move failed", "فشل النقل"),
        ["dialog.moveFailedBody"] = ("The downloads could not be moved: {0}\n\nNothing was deleted; the library is still at {1}.",
                                     "تعذّر نقل التنزيلات: {0}\n\nلم يُحذف شيء، والمكتبة ما زالت في {1}."),
        ["dialog.chooseBackground"] = ("Choose a background", "اختر خلفية"),

        // Screensaver
        // Plain words: the panel spaces the Latin label out for looks, which Arabic letters cannot
        // take — spacing them apart breaks the joins and leaves "ا ل ق ا ر ئ".
        ["screensaver.reciter"] = ("RECITER", "القارئ"),
        ["screensaver.verseOf"] = ("Verse {0} of {1}", "الآية {0} من {1}"),
        ["screensaver.juz"] = ("Juz {0}", "جزء {0}"),
        ["screensaver.nothing"] = ("No recitations downloaded yet — open the screensaver's Settings to choose a reciter.",
                                   "لا توجد تلاوات منزَّلة — افتح إعدادات شاشة التوقف لاختيار قارئ."),
        ["screensaver.folderMissing"] = ("The folder holding the recitations isn't available right now — reconnect the drive, or choose another folder in Settings.",
                                         "مجلد التلاوات غير متاح حاليًا — أعد توصيل القرص، أو اختر مجلدًا آخر من الإعدادات."),
    };

    private static bool _arabic;

    /// <summary>True when the app is showing Arabic, so layouts can flip to right-to-left.</summary>
    public static bool IsArabic => _arabic;

    public static event Action? Changed;

    public static void Use(AppLanguage language)
    {
        var arabic = language switch
        {
            AppLanguage.Arabic => true,
            AppLanguage.English => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase),
        };

        if (arabic == _arabic)
            return;

        _arabic = arabic;
        Changed?.Invoke();
    }

    /// <summary>The text for a key. Unknown keys return the key itself, which is easier to spot than an empty label.</summary>
    public static string Get(string key) =>
        Table.TryGetValue(key, out var pair) ? (_arabic ? pair.Ar : pair.En) : key;

    public static string Get(string key, params object[] args) => string.Format(Get(key), args);

    /// <summary>
    /// Whether a piece of content reads right-to-left, judged by its own letters rather than by the UI
    /// language: an English translation stays left-to-right inside an Arabic layout, and vice versa.
    /// </summary>
    public static bool IsRightToLeftText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return _arabic;

        foreach (var c in text)
        {
            if (c is (>= '؀' and <= 'ۿ') or (>= 'ݐ' and <= 'ݿ') or (>= 'ﭐ' and <= '﻿'))
                return true;
            if (char.IsLetter(c))
                return false;
        }
        return _arabic;
    }

    /// <summary>Keys whose Arabic is still missing — used by a test so translations cannot silently rot.</summary>
    /// <summary>
    /// Proper nouns that read the same in both languages. They are listed rather than left out of the
    /// table, so every user-visible string still has one home — but an identical pair is normally a
    /// missing translation, and that is what <see cref="UntranslatedKeys"/> is for.
    /// </summary>
    private static readonly HashSet<string> SameInBothLanguages =
    [
        "recitation.source.qurancom",
        "recitation.source.mp3quran",
    ];

    public static IReadOnlyList<string> UntranslatedKeys() =>
        Table.Where(kv => !SameInBothLanguages.Contains(kv.Key))
             .Where(kv => string.IsNullOrWhiteSpace(kv.Value.Ar) || kv.Value.Ar == kv.Value.En)
             .Select(kv => kv.Key)
             .ToList();

    public static IReadOnlyCollection<string> AllKeys => Table.Keys;
}
