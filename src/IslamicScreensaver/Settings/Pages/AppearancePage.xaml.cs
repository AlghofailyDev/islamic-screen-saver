using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Player;

namespace IslamicScreensaver.Settings.Pages;

public partial class AppearancePage : UserControl
{
    private readonly SettingsContext _ctx;
    private bool _loading;

    internal AppearancePage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        PreviewPanel.SetBackdrop(PreviewBackground);
        PreviewBackground.Load(_ctx.Settings.Background);
        _sceneSignature = SceneSignature();
        _verseSignature = VerseSignature();
        RefreshVerse();

        _ctx.SettingsChanged += () =>
        {
            // Other pages may have changed the scene or what the verse shows; sliders here only restyle.
            if (SceneSignature() != _sceneSignature)
            {
                _sceneSignature = SceneSignature();
                PreviewBackground.Load(_ctx.Settings.Background);
                ApplyPreview();
            }
            if (VerseSignature() != _verseSignature)
            {
                _verseSignature = VerseSignature();
                RefreshVerse();
            }
        };

        LoadControls();
    }

    private string _sceneSignature = string.Empty;
    private string _verseSignature = string.Empty;

    private string SceneSignature() =>
        $"{_ctx.Settings.Background.Kind}|{_ctx.Settings.Background.BundledName}|{_ctx.Settings.Background.FilePath}";

    private string VerseSignature() =>
        $"{_ctx.Settings.ShowTranslation}|{_ctx.Settings.TranslationId}|{_ctx.Settings.ReciterIds.FirstOrDefault()}|{_ctx.Settings.Panel.WordHighlight}";

    private void RefreshVerse()
    {
        PreviewPanel.ApplyStyle(_ctx.Settings.Panel, PreviewBackground.SuggestedTint);
        PreviewPanel.Show(SampleVerse());
        PreviewPanel.HighlightWord(2);
        PreviewPanel.SetProgress(0.38);
    }

    private void LoadControls()
    {
        var p = _ctx.Settings.Panel;
        _loading = true;
        Blur.Value = p.Blur;
        Grain.Value = p.Grain * 100;
        TintOpacity.Value = p.TintOpacity * 100;
        TintMode.SelectedIndex = p.TintColor is null ? 0 : 1;
        TintHex.Text = p.TintColor ?? ToHex(PreviewBackground.SuggestedTint);
        TintHex.IsEnabled = p.TintColor is not null;
        ArabicHex.Text = p.ArabicColor;
        TranslationHex.Text = p.TranslationColor;
        WordHighlight.IsChecked = p.WordHighlight;
        _loading = false;

        BlurValue.Text = $"{p.Blur:0} px";
        GrainValue.Text = $"{p.Grain * 100:0}%";
        TintOpacityValue.Text = $"{p.TintOpacity * 100:0}%";
        UpdateSwatches();
        ApplyPreview();
    }

    private VerseDisplay SampleVerse()
    {
        var text = _ctx.Cache.ReadSurahText(1)?.FirstOrDefault(v => v.Key.Ayah == 2);
        var translation = _ctx.Settings.ShowTranslation
            ? _ctx.Cache.ReadSurahTranslation(_ctx.Settings.TranslationId, 1)?.Texts.ElementAtOrDefault(1)
            : null;
        var chapter = _ctx.Chapter(1);
        return new VerseDisplay(
            chapter?.NameArabic ?? "الفاتحة",
            chapter?.NameSimple ?? "Al-Fatihah",
            2, 7,
            text?.TextUthmani ?? "ٱلْحَمْدُ لِلَّهِ رَبِّ ٱلْعَـٰلَمِينَ",
            text?.Words ?? ["ٱلْحَمْدُ", "لِلَّهِ", "رَبِّ", "ٱلْعَـٰلَمِينَ"],
            translation,
            _ctx.ReciterName(_ctx.Settings.ReciterIds.FirstOrDefault(7)),
            IsBismillah: false);
    }

    /// <summary>Restyles the preview in place — no crossfade, so slider drags feel live.</summary>
    private void ApplyPreview()
    {
        PreviewPanel.ApplyStyle(_ctx.Settings.Panel, PreviewBackground.SuggestedTint);
        PreviewPanel.HighlightWord(2);
        if (_ctx.Settings.Panel.TintColor is null && !_loading)
        {
            _loading = true;
            TintHex.Text = ToHex(PreviewBackground.SuggestedTint);
            _loading = false;
            UpdateSwatches();
        }
    }

    private void Commit()
    {
        if (_loading) return;
        ApplyPreview();
        _ctx.Changed();
    }

    private void Blur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        BlurValue.Text = $"{Blur.Value:0} px";
        if (_loading) return;
        _ctx.Settings.Panel.Blur = Blur.Value;
        Commit();
    }

    private void Grain_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        GrainValue.Text = $"{Grain.Value:0}%";
        if (_loading) return;
        _ctx.Settings.Panel.Grain = Grain.Value / 100;
        Commit();
    }

    private void TintOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        TintOpacityValue.Text = $"{TintOpacity.Value:0}%";
        if (_loading) return;
        _ctx.Settings.Panel.TintOpacity = TintOpacity.Value / 100;
        Commit();
    }

    private void TintMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var custom = TintMode.SelectedIndex == 1;
        TintHex.IsEnabled = custom;
        _ctx.Settings.Panel.TintColor = custom ? (ParseHex(TintHex.Text) is { } c ? ToHex(c) : ToHex(PreviewBackground.SuggestedTint)) : null;
        if (custom && ParseHex(TintHex.Text) is null)
        {
            _loading = true;
            TintHex.Text = _ctx.Settings.Panel.TintColor;
            _loading = false;
        }
        Commit();
    }

    private void TintHex_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateSwatches();
        if (_loading || TintMode.SelectedIndex != 1 || ParseHex(TintHex.Text) is not { } c) return;
        _ctx.Settings.Panel.TintColor = ToHex(c);
        Commit();
    }

    private void ArabicHex_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateSwatches();
        if (_loading || ParseHex(ArabicHex.Text) is not { } c) return;
        _ctx.Settings.Panel.ArabicColor = ToHex(c);
        Commit();
    }

    private void TranslationHex_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateSwatches();
        if (_loading || ParseHex(TranslationHex.Text) is not { } c) return;
        _ctx.Settings.Panel.TranslationColor = ToHex(c);
        Commit();
    }

    private void WordHighlight_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.Panel.WordHighlight = WordHighlight.IsChecked == true;
        Commit();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.Panel = new PanelSettings();
        LoadControls();
        _ctx.Changed();
    }

    private void UpdateSwatches()
    {
        TintSwatch.Background = SwatchBrush(TintHex.Text);
        ArabicSwatch.Background = SwatchBrush(ArabicHex.Text);
        TranslationSwatch.Background = SwatchBrush(TranslationHex.Text);
    }

    private static Brush SwatchBrush(string hex) =>
        ParseHex(hex) is { } c ? new SolidColorBrush(c) : Brushes.Transparent;

    private static Color? ParseHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.Trim();
        if (!t.StartsWith('#'))
            t = "#" + t;
        if (t.Length != 7)
            return null;
        try
        {
            return (Color)ColorConverter.ConvertFromString(t);
        }
        catch
        {
            return null;
        }
    }

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
