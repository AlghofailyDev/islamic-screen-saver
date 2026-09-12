using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IslamicScreensaver.Player;

namespace IslamicScreensaver.Screensaver;

public partial class VersePanel : UserControl
{
    private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE4, 0xA3));
    private readonly List<Run> _wordRuns = [];
    private Brush _arabicBrush;

    public VersePanel()
    {
        InitializeComponent();
        _arabicBrush = ArabicText.Foreground;
        Visibility = System.Windows.Visibility.Collapsed;
    }

    public bool WordHighlight { get; set; } = true;

    public void ShowHint(string text)
    {
        Visibility = System.Windows.Visibility.Visible;
        SurahLabel.Text = string.Empty;
        ArabicText.Text = string.Empty;
        TranslationText.Text = text;
        ReciterLabel.Text = string.Empty;
    }

    public void Show(VerseDisplay verse)
    {
        Visibility = System.Windows.Visibility.Visible;

        SurahLabel.Text = verse.IsBismillah
            ? $"{verse.SurahNameArabic}  ·  {verse.SurahNameSimple}"
            : $"{verse.SurahNameArabic}  ·  {verse.SurahNameSimple}  ·  Verse {verse.VerseNumber} of {verse.VersesCount}";

        // One Run per word so the recited word can be lit without re-laying-out the text.
        ArabicText.Inlines.Clear();
        _wordRuns.Clear();
        if (verse.Words.Count > 0 && WordHighlight)
        {
            for (var i = 0; i < verse.Words.Count; i++)
            {
                if (i > 0)
                    ArabicText.Inlines.Add(new Run(" "));
                var run = new Run(verse.Words[i]) { Foreground = _arabicBrush };
                _wordRuns.Add(run);
                ArabicText.Inlines.Add(run);
            }
        }
        else
        {
            ArabicText.Inlines.Add(new Run(verse.ArabicText) { Foreground = _arabicBrush });
        }

        TranslationText.Text = verse.Translation ?? string.Empty;
        TranslationText.Visibility = string.IsNullOrEmpty(verse.Translation) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        ReciterLabel.Text = verse.ReciterName;
    }

    /// <param name="wordPosition">1-based word within the verse; 0 clears the highlight.</param>
    public void HighlightWord(int wordPosition)
    {
        for (var i = 0; i < _wordRuns.Count; i++)
            _wordRuns[i].Foreground = i + 1 == wordPosition ? HighlightBrush : _arabicBrush;
    }
}
