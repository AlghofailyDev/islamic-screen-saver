using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using IslamicScreensaver.Core.Content;
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
        foreach (var block in new[] { TranslationA, TranslationB, ChipJuz, ChipVerse })
            block.FontFamily = AppFonts.Ui;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Design-value margins from the screen edge, matching the 1080p overlay.</summary>
    private const double EdgeMargin = 88;

    /// <summary>
    /// Set once from the settings. Every verse calls <see cref="Show"/>, which makes the panel
    /// visible, so "off" has to be remembered here rather than by collapsing it from outside.
    /// </summary>
    private bool _hidden;

    /// <summary>Moves the panel, or takes it off screen entirely.</summary>
    public void ApplyPosition(PanelPosition position)
    {
        _hidden = position == PanelPosition.Hidden;
        if (_hidden)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        VerticalAlignment = position switch
        {
            PanelPosition.Top => VerticalAlignment.Top,
            PanelPosition.Middle => VerticalAlignment.Center,
            _ => VerticalAlignment.Bottom,
        };

        Margin = position switch
        {
            PanelPosition.Top => new Thickness(0, EdgeMargin, 0, 0),
            PanelPosition.Middle => new Thickness(0),
            _ => new Thickness(0, 0, 0, EdgeMargin),
        };

        // Coming back from Hidden: the panel is only shown again by the next verse, which in the
        // settings preview never arrives. Put it back straight away if it already has something.
        if (_hasContent)
            Visibility = Visibility.Visible;
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
        // A hint is the only thing a hidden panel still shows: it is how the screensaver says it has
        // nothing to play, and silence with no explanation would look broken.
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
        if (_hidden)
            return;

        Visibility = Visibility.Visible;
        Chip.Visibility = Visibility.Visible;
        ProgressTrack.Visibility = Visibility.Visible;

        ChipArabic.Text = verse.SurahNameArabic;
        ChipJuz.Text = verse.JuzLabel;
        // "جزء عمّ" is Arabic whichever language the chrome is in; the transliteration is not.
        ChipJuz.FlowDirection = Strings.IsRightToLeftText(verse.JuzLabel) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
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

    // --- Fitting a long verse -----------------------------------------------------------------
    // Nineteen verses of the Qur'an do not fit the card at the design size; 2:282 needs about 72%
    // more room than there is. They are stepped down until they fit rather than clipped or scrolled.
    //
    // A Viewbox would be fewer lines and the wrong answer: it scales uniformly, so the glass panel
    // would grow narrower as well as shorter and its width would jump from verse to verse. Only the
    // type shrinks here — the panel keeps its width and reflows.

    /// <summary>Design-size type, and the line height that goes with it.</summary>
    private const double ArabicSize = 66;
    private const double ArabicLineHeight = 125;
    private const double TranslationSize = 22;
    private const double TranslationLineHeight = 33;

    /// <summary>
    /// How tall the text may be, in 1080p design units: the screen less the chip above it, the
    /// panel's own padding and the margin from the screen edge. Generous rather than exact — the
    /// card should never look wedged against the edge of the display.
    /// </summary>
    private const double MaxTextHeight = 800;

    /// <summary>
    /// Below this the verse stops being something you can read across a room. Measured against the
    /// whole Qur'an, nothing needs it: the longest verse fits at about 48.
    /// </summary>
    private const double MinArabicSize = 38;

    /// <summary>
    /// Shrinks the type until the verse fits, and leaves it alone when it already does — which is
    /// every verse but nineteen. Measured rather than estimated from character counts, so it holds
    /// for whatever Quran font the user drops into assets/fonts.
    /// </summary>
    private static void FitToCard(TextBlock arabic, TextBlock translation)
    {
        var width = arabic.ActualWidth > 0 ? arabic.ActualWidth : 912;   // card width less its padding

        for (var size = ArabicSize; ; size -= 2)
        {
            var scale = size / ArabicSize;
            arabic.FontSize = size;
            arabic.LineHeight = ArabicLineHeight * scale;
            translation.FontSize = TranslationSize * scale;
            translation.LineHeight = TranslationLineHeight * scale;

            if (size <= MinArabicSize)
                return;

            // Ask the layout system, so word runs, wrapping and the translation's own margin all count.
            arabic.Measure(new Size(width, double.PositiveInfinity));
            translation.Measure(new Size(width, double.PositiveInfinity));
            if (arabic.DesiredSize.Height + translation.DesiredSize.Height <= MaxTextHeight)
                return;
        }
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

        FitToCard(arabic, translation);
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
