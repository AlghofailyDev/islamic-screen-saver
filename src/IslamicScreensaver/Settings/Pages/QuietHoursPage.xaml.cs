using System.Windows;
using System.Windows.Controls;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Settings.Pages;

/// <summary>
/// When the screensaver should keep out of the way. Windows owns the idle timer, so the choice here
/// is what happens once it has already launched — not whether it launches.
/// </summary>
public partial class QuietHoursPage : UserControl
{
    /// <summary>In <see cref="QuietHoursBehaviour"/> order, so the combo's index is the enum value.</summary>
    private static readonly string[] BehaviourKeys = ["quiet.dark", "quiet.dontStart", "quiet.silent"];

    private static readonly string[] BehaviourDescriptions = ["quiet.dark.desc", "quiet.dontStart.desc", "quiet.silent.desc"];

    private readonly SettingsContext _ctx;
    private bool _loading;

    internal QuietHoursPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        var quiet = _ctx.Settings.Quiet;
        _loading = true;

        // Quarter-hour steps: fine enough for a bedtime, and short enough a list to scan.
        for (var hour = 0; hour < 24; hour++)
        {
            FromHour.Items.Add(hour.ToString("00"));
            ToHour.Items.Add(hour.ToString("00"));
        }
        foreach (var minute in new[] { 0, 15, 30, 45 })
        {
            FromMinute.Items.Add(minute.ToString("00"));
            ToMinute.Items.Add(minute.ToString("00"));
        }

        FromHour.SelectedIndex = quiet.From.Hour;
        FromMinute.SelectedIndex = NearestQuarter(quiet.From.Minute);
        ToHour.SelectedIndex = quiet.To.Hour;
        ToMinute.SelectedIndex = NearestQuarter(quiet.To.Minute);

        foreach (var key in BehaviourKeys)
            Behaviour.Items.Add(Strings.Get(key));
        Behaviour.SelectedIndex = (int)quiet.Behaviour;

        Enabled.IsChecked = quiet.Enabled;
        _loading = false;

        Refresh();
    }

    private static int NearestQuarter(int minute) => Math.Clamp(minute / 15, 0, 3);

    private void Enabled_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.Quiet.Enabled = Enabled.IsChecked == true;
        _ctx.Changed();
        Refresh();
    }

    private void Time_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FromHour.SelectedIndex < 0 || ToHour.SelectedIndex < 0
            || FromMinute.SelectedIndex < 0 || ToMinute.SelectedIndex < 0)
            return;

        _ctx.Settings.Quiet.From = new TimeOnly(FromHour.SelectedIndex, FromMinute.SelectedIndex * 15);
        _ctx.Settings.Quiet.To = new TimeOnly(ToHour.SelectedIndex, ToMinute.SelectedIndex * 15);
        _ctx.Changed();
        Refresh();
    }

    private void Behaviour_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Behaviour.SelectedIndex < 0)
            return;

        _ctx.Settings.Quiet.Behaviour = (QuietHoursBehaviour)Behaviour.SelectedIndex;
        _ctx.Changed();
        Refresh();
    }

    private void Refresh()
    {
        var quiet = _ctx.Settings.Quiet;
        Details.Visibility = quiet.Enabled ? Visibility.Visible : Visibility.Collapsed;
        if (!quiet.Enabled)
            return;

        BehaviourNote.Text = Strings.Get(BehaviourDescriptions[(int)quiet.Behaviour]);

        var from = $"{quiet.From:HH\\:mm}";
        var to = $"{quiet.To:HH\\:mm}";
        // A window whose end is before its start runs through midnight, which is how a night is written.
        WindowText.Text = Strings.Get(quiet.To < quiet.From ? "quiet.overnight" : "quiet.window", from, to);
        NowText.Text = Strings.Get(quiet.CoversNow() ? "quiet.nowInside" : "quiet.nowOutside");

        var sameTime = quiet.From == quiet.To;
        WarningText.Visibility = sameTime ? Visibility.Visible : Visibility.Collapsed;
        WarningText.Text = sameTime ? Strings.Get("quiet.sameTime") : string.Empty;
    }
}
