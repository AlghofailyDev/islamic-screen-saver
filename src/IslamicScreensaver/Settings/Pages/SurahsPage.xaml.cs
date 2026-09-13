using System.Windows;
using System.Windows.Controls;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Settings.Pages;

public partial class SurahsPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly List<SurahTile> _tiles = [];
    private bool _loading;
    private bool _bulkUpdate;

    internal SurahsPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        _loading = true;
        Order.SelectedIndex = _ctx.Settings.Order == PlayOrder.Shuffled ? 1 : 0;
        Resume.IsChecked = _ctx.Settings.ResumeWhereLeftOff;
        _loading = false;

        _ctx.CatalogChanged += Rebuild;
        Rebuild();
    }

    private void Rebuild()
    {
        _tiles.Clear();
        var selected = _ctx.Settings.SelectedSurahs is { } s ? new HashSet<int>(s) : null;
        foreach (var chapter in _ctx.Chapters)
            _tiles.Add(new SurahTile(chapter, selected is null || selected.Contains(chapter.Number), OnTileChanged));

        Tiles.ItemsSource = _tiles;
        EmptyText.Visibility = _tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCount();
    }

    private void OnTileChanged()
    {
        if (_bulkUpdate)
            return;
        Persist();
    }

    private void Persist()
    {
        var chosen = _tiles.Where(t => t.IsSelected).Select(t => t.Number).ToList();
        // "Everything" is stored as null so newly added content is included automatically.
        _ctx.Settings.SelectedSurahs = chosen.Count == _tiles.Count ? null : chosen;
        _ctx.Changed();
        UpdateCount();
    }

    private void UpdateCount()
    {
        var count = _tiles.Count(t => t.IsSelected);
        CountText.Text = _tiles.Count == 0 ? string.Empty : count == _tiles.Count ? "All 114 selected" : $"{count} of {_tiles.Count} selected";
    }

    private void SetAll(bool value)
    {
        _bulkUpdate = true;
        foreach (var tile in _tiles)
            tile.IsSelected = value;
        _bulkUpdate = false;
        Persist();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);

    private void Clear_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        var query = Search.Text.Trim();
        SearchHint.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tile in _tiles)
            tile.IsVisible = tile.Matches(query);
    }

    private void Order_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _ctx.Settings.Order = Order.SelectedIndex == 1 ? PlayOrder.Shuffled : PlayOrder.Sequential;
        _ctx.Changed();
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.ResumeWhereLeftOff = Resume.IsChecked == true;
        _ctx.Changed();
    }
}

internal sealed class SurahTile : ObservableObject
{
    private readonly Action _changed;
    private bool _isSelected;
    private bool _isVisible = true;

    public SurahTile(Chapter chapter, bool isSelected, Action changed)
    {
        _changed = changed;
        Number = chapter.Number;
        NameSimple = chapter.NameSimple;
        NameArabic = chapter.NameArabic;
        Meta = $"{chapter.VersesCount} verses";
        _isSelected = isSelected;
    }

    public int Number { get; }
    public string NameSimple { get; }
    public string NameArabic { get; }
    public string Meta { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
                _changed();
        }
    }

    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    public bool Matches(string query) =>
        query.Length == 0
        || NameSimple.Contains(query, StringComparison.OrdinalIgnoreCase)
        || NameArabic.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Number.ToString() == query;
}
