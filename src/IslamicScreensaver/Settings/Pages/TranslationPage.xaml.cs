using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;

namespace IslamicScreensaver.Settings.Pages;

public partial class TranslationPage : UserControl
{
    private readonly SettingsContext _ctx;
    private bool _loading;

    internal TranslationPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        _ctx.CatalogChanged += Rebuild;
        _ctx.SettingsChanged += UpdateStatus;
        _ctx.Downloads.Changed += (key, progress) =>
        {
            if (key == JobKey)
                OnJob(progress);
        };
        _ctx.Downloads.Failed += (key, message) =>
        {
            if (key == JobKey)
                MessageBox.Show(message, Strings.Get("dialog.downloadFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        Show.IsChecked = _ctx.Settings.ShowTranslation;
        Rebuild();
    }

    private string JobKey => $"translation:{_ctx.Settings.TranslationId}";

    private void Rebuild()
    {
        _loading = true;
        LanguageBox.Items.Clear();
        var languages = _ctx.Translations
            .Select(t => t.LanguageName)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var language in languages)
            LanguageBox.Items.Add(new ComboBoxItem { Content = Title(language), Tag = language });

        var current = _ctx.Translations.FirstOrDefault(t => t.Id == _ctx.Settings.TranslationId);
        var currentLanguage = current?.LanguageName ?? "english";
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => string.Equals((string)i.Tag, currentLanguage, StringComparison.OrdinalIgnoreCase))
                                ?? LanguageBox.Items.Cast<ComboBoxItem>().FirstOrDefault();
        FillTranslations();
        _loading = false;

        EmptyText.Visibility = languages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatus();
        OnJob(_ctx.Downloads.ProgressOf(JobKey));
    }

    private void FillTranslations()
    {
        Translation.Items.Clear();
        if (LanguageBox.SelectedItem is not ComboBoxItem { Tag: string language })
            return;

        foreach (var t in _ctx.Translations.Where(t => string.Equals(t.LanguageName, language, StringComparison.OrdinalIgnoreCase)).OrderBy(t => t.Name))
            Translation.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t });

        Translation.SelectedItem = Translation.Items.Cast<ComboBoxItem>().FirstOrDefault(i => ((TranslationResource)i.Tag).Id == _ctx.Settings.TranslationId)
                                   ?? Translation.Items.Cast<ComboBoxItem>().FirstOrDefault();
    }

    private void UpdateStatus()
    {
        if (_ctx.Downloads.IsBusy(JobKey))
            return;

        var wanted = _ctx.EffectiveSurahs;
        var have = wanted.Count(s => _ctx.Cache.HasSurahTranslation(_ctx.Settings.TranslationId, s));
        var name = _ctx.Translations.FirstOrDefault(t => t.Id == _ctx.Settings.TranslationId)?.Name ?? $"Translation {_ctx.Settings.TranslationId}";

        StatusText.Text = name;
        StatusDetail.Text = have == wanted.Count
            ? Strings.Get("status.allDownloaded")
            : have == 0 ? Strings.Get("status.nothingDownloadedYet")
            : Strings.Get("status.downloadedOf", have, wanted.Count);
        DownloadButton.Content = Strings.Get(have == wanted.Count ? "common.downloaded" : "common.download");
        DownloadButton.IsEnabled = have != wanted.Count;
        Progress.Visibility = Visibility.Collapsed;
    }

    private void OnJob(JobProgress? progress)
    {
        if (progress is null)
        {
            UpdateStatus();
            return;
        }
        Progress.Visibility = Visibility.Visible;
        Progress.Value = progress.Fraction;
        StatusDetail.Text = progress.CurrentItem is null
            ? Strings.Get("status.finishing")
            : Strings.Get("status.downloadingOf", progress.CurrentItem, progress.Done, progress.Total);
        DownloadButton.Content = Strings.Get("common.cancel");
        DownloadButton.IsEnabled = true;
    }

    private void Show_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.ShowTranslation = Show.IsChecked == true;
        _ctx.Changed();
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _loading = true;
        FillTranslations();
        _loading = false;
        Translation_Changed(sender, e);
    }

    private void Translation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Translation.SelectedItem is not ComboBoxItem { Tag: TranslationResource resource }) return;
        _ctx.Settings.TranslationId = resource.Id;
        _ctx.Changed();
        OnJob(_ctx.Downloads.ProgressOf(JobKey));
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx.Downloads.IsBusy(JobKey))
        {
            _ctx.Downloads.CancelAll();
            return;
        }
        _ctx.Downloads.EnqueueTranslation(_ctx.Settings.TranslationId, _ctx.EffectiveSurahs);
    }

    private static string Title(string language) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(language.ToLowerInvariant());
}
