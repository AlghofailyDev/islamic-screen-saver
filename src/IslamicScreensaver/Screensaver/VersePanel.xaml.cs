using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Player;
using IslamicScreensaver.Rendering;

namespace IslamicScreensaver.Screensaver;

/// <summary>The frosted verse card plus its surah chip. Verse changes crossfade; the recited word glows.</summary>
public partial class VersePanel : UserControl
{
    private static readonly Duration FadeOut = TimeSpan.FromMilliseconds(220);
    private static readonly Duration FadeIn = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan FadeInDelay = TimeSpan.FromMilliseconds(90);
    private static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };
    private const double FloatDistance = 8;

    private readonly Brush _highlightBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xE4, 0xA3)));
    private Brush _arabicBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFB, 0xF5, 0xE6)));
    private readonly List<Run> _wordRuns = [];
    private bool _showingA = true;
    private bool _hasContent;

    public VersePanel()
    {
        InitializeComponent();
        foreach (var block in new[] { ArabicA, ArabicB, ChipArabic })
            block.FontFamily = AppFonts.Arabic;
        foreach (var block in new[] { TranslationA, TranslationB, ChipEnglish, ChipVerse })
            block.FontFamily = AppFonts.Ui;
        Visibility = Visibility.Collapsed;
    }

    public bool WordHighlight { get; set; } = true;

    /// <summary>Points both glass surfaces at the background so they can sample what is behind them.</summary>
    public void SetBackdrop(Visual backdrop)
    {
        Glass.Backdrop = backdrop;
        Chip.Backdrop = backdrop;
    }

    public void ApplyStyle(PanelSettings panel, Color autoTint)
    {
        Glass.BlurRadius = panel.Blur;
        Glass.Grain = panel.Grain;
        Glass.TintOpacity = panel.TintOpacity;
        Glass.TintColor = TryColor(panel.TintColor) ?? autoTint;

        _arabicBrush = Freeze(new SolidColorBrush(TryColor(panel.ArabicColor) ?? Color.FromRgb(0xFB, 0xF5, 0xE6)));
        var translation = Freeze(new SolidColorBrush(TryColor(panel.TranslationColor) ?? Color.FromRgb(0xC9, 0xC9, 0xC9)));
        foreach (var block in new[] { ArabicA, ArabicB })
            block.Foreground = _arabicBrush;
        foreach (var block in new[] { TranslationA, TranslationB })
            block.Foreground = translation;

        WordHighlight = panel.WordHighlight;
    }

    public void ShowHint(string text)
    {
        Visibility = Visibility.Visible;
        Chip.Visibility = Visibility.Collapsed;
        ProgressTrack.Visibility = Visibility.Collapsed;
        var (arabic, translation) = ActiveBlocks();
        arabic.Inlines.Clear();
        translation.Text = text;
        translation.Visibility = Visibility.Visible;
        translation.FlowDirection = Strings.IsRightToLeftText(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    public void Show(VerseDisplay verse)
    {
        Visibility = Visibility.Visible;
        Chip.Visibility = Visibility.Visible;
        ProgressTrack.Visibility = Visibility.Visible;

        ChipArabic.Text = verse.SurahNameArabic;
        ChipEnglish.Text = verse.SurahNameSimple;
        ChipVerse.Text = verse.IsBismillah ? string.Empty : Strings.Get("screensaver.verseOf", verse.VerseNumber, verse.VersesCount);
        ChipSeparator2.Visibility = ChipVerse.Visibility = verse.IsBismillah ? Visibility.Collapsed : Visibility.Visible;

        // Fill the hidden layer, then crossfade to it. First verse simply appears.
        var incoming = _hasContent ? (_showingA ? LayerB : LayerA) : LayerA;
        var outgoing = _hasContent ? (_showingA ? LayerA : LayerB) : null;
        var (arabic, translation) = incoming == LayerA ? (ArabicA, TranslationA) : (ArabicB, TranslationB);

        Fill(arabic, translation, verse);

        if (outgoing is not null)
        {
            Animate(outgoing, from: 1, to: 0, fromY: 0, toY: -FloatDistance, FadeOut, TimeSpan.Zero, collapseAtEnd: true);
            Animate(incoming, from: 0, to: 1, fromY: FloatDistance, toY: 0, FadeIn, FadeInDelay, collapseAtEnd: false);
            _showingA = incoming == LayerA;
        }
        else
        {
            incoming.Opacity = 1;
            incoming.Visibility = Visibility.Visible;
            _showingA = true;
        }

        _hasContent = true;
        SetProgress(0);
    }

    /// <param name="wordPosition">1-based word within the verse; 0 clears the highlight.</param>
    public void HighlightWord(int wordPosition)
    {
        for (var i = 0; i < _wordRuns.Count; i++)
            _wordRuns[i].Foreground = i + 1 == wordPosition ? _highlightBrush : _arabicBrush;
    }

    public void SetProgress(double fraction)
    {
        ProgressFill.Width = Math.Clamp(fraction, 0, 1) * ProgressTrack.ActualWidth;
    }

    private void Fill(TextBlock arabic, TextBlock translation, VerseDisplay verse)
    {
        arabic.Inlines.Clear();
        _wordRuns.Clear();

        if (verse.Words.Count > 0 && WordHighlight)
        {
            for (var i = 0; i < verse.Words.Count; i++)
            {
                if (i > 0)
                    arabic.Inlines.Add(new Run(" "));
                var run = new Run(verse.Words[i]) { Foreground = _arabicBrush };
                _wordRuns.Add(run);
                arabic.Inlines.Add(run);
            }
        }
        else
        {
            arabic.Inlines.Add(new Run(verse.ArabicText) { Foreground = _arabicBrush });
        }

        translation.Text = verse.Translation ?? string.Empty;
        translation.Visibility = string.IsNullOrEmpty(verse.Translation) ? Visibility.Collapsed : Visibility.Visible;
        // The translation follows its own language, not the app's: English stays left-to-right under Arabic chrome.
        translation.FlowDirection = Strings.IsRightToLeftText(verse.Translation)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private (TextBlock Arabic, TextBlock Translation) ActiveBlocks() =>
        _showingA ? (ArabicA, TranslationA) : (ArabicB, TranslationB);

    private static void Animate(StackPanel layer, double from, double to, double fromY, double toY, Duration duration, TimeSpan delay, bool collapseAtEnd)
    {
        layer.Visibility = Visibility.Visible;
        var transform = (TranslateTransform)layer.RenderTransform;

        var fade = new DoubleAnimation(from, to, duration) { BeginTime = delay, EasingFunction = Ease };
        var move = new DoubleAnimation(fromY, toY, duration) { BeginTime = delay, EasingFunction = Ease };
        if (collapseAtEnd)
            fade.Completed += (_, _) => { if (layer.Opacity < 0.01) layer.Visibility = Visibility.Collapsed; };

        layer.BeginAnimation(OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, move);
    }

    private static Color? TryColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return null;
        }
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
