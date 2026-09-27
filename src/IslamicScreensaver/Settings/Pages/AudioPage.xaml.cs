using System.Windows;
using System.Windows.Controls;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Player;

namespace IslamicScreensaver.Settings.Pages;

/// <summary>Volume, which speakers to use, and the verse-timing trim.</summary>
public partial class AudioPage : UserControl
{
    private const string TestKey = "audio-test";

    private readonly SettingsContext _ctx;
    private bool _loading;

    internal AudioPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        _loading = true;
        Volume.Value = _ctx.Settings.Volume * 100;
        SyncOffset.Value = _ctx.Settings.SyncOffsetMs;
        OverrideVolume.IsChecked = _ctx.Settings.OverrideSystemVolume;
        VolumeFloor.Value = _ctx.Settings.SystemVolumeFloor * 100;
        _loading = false;
        VolumeValue.Text = $"{Volume.Value:0}%";
        VolumeFloorValue.Text = $"{VolumeFloor.Value:0}%";
        SyncOffsetValue.Text = $"{SyncOffset.Value:+0;-0;0} ms";
        UpdateFloorCard();

        _ctx.Samples.PlayingChanged += _ => UpdateTestButton();
        LoadDevices();
        UpdateTestButton();
    }

    private void LoadDevices()
    {
        var devices = AudioDevices.All().ToList();
        var savedId = _ctx.Settings.OutputDeviceId;

        // A device that is saved but unplugged still belongs in the list, so the choice is not silently lost.
        if (savedId is not null && devices.All(d => d.Id != savedId))
            devices.Add(new AudioDevice(savedId, Strings.Get("audio.notConnected", _ctx.Settings.OutputDeviceName ?? Strings.Get("status.savedDevice"))));

        _loading = true;
        Device.ItemsSource = devices;
        Device.SelectedItem = devices.FirstOrDefault(d => d.Id == savedId) ?? devices[0];
        _loading = false;

        var missing = savedId is not null && AudioDevices.Resolve(savedId) is null;
        DeviceNote.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        DeviceNote.Text = missing
            ? Strings.Get("status.deviceNotConnected", _ctx.Settings.OutputDeviceName ?? Strings.Get("status.savedDevice"))
            : string.Empty;
    }

    private void Device_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Device.SelectedItem is not AudioDevice device)
            return;

        _ctx.Samples.Stop();
        _ctx.Settings.OutputDeviceId = device.Id;
        _ctx.Settings.OutputDeviceName = device.IsSystemDefault ? null : device.Name;
        _ctx.Changed();
        DeviceNote.Visibility = Visibility.Collapsed;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadDevices();

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx.Samples.PlayingKey == TestKey)
        {
            _ctx.Samples.Stop();
            return;
        }

        var cached = _ctx.Cache.CachedRecitations();
        var recitation = cached.Count > 0 ? cached.First() : (RecitationId?)null;
        var surah = recitation is { } r ? _ctx.Cache.CachedSurahsFor(r).OrderByDescending(s => s).FirstOrDefault() : 0;
        if (recitation is null || surah == 0)
        {
            MessageBox.Show(Strings.Get("dialog.downloadFirstTest"), Strings.Get("dialog.nothingToPlay"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _ctx.Samples.Toggle(TestKey, _ctx.Cache.AudioPath(recitation.Value, surah), (float)_ctx.Settings.Volume);
    }

    private void UpdateTestButton()
    {
        var playing = _ctx.Samples.PlayingKey == TestKey;
        TestGlyph.Text = playing ? "" : "";
        TestLabel.Text = Strings.Get(playing ? "common.stop" : "common.test");
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        VolumeValue.Text = $"{Volume.Value:0}%";
        if (_loading) return;
        _ctx.Settings.Volume = Volume.Value / 100;
        _ctx.Changed();
    }

    private void OverrideVolume_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.OverrideSystemVolume = OverrideVolume.IsChecked == true;
        _ctx.Changed();
        UpdateFloorCard();
    }

    private void VolumeFloor_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // This slider's Minimum is 5, so WPF coerces its default value of 0 up to 5 while the XAML is
        // still being parsed — raising ValueChanged before the label further down the file exists.
        // The other sliders start at 0 and never hit this.
        if (VolumeFloorValue is null)
            return;

        VolumeFloorValue.Text = $"{VolumeFloor.Value:0}%";
        if (_loading) return;
        _ctx.Settings.SystemVolumeFloor = VolumeFloor.Value / 100;
        _ctx.Changed();
    }

    /// <summary>The floor only means anything while the override is on, so it hides with it.</summary>
    private void UpdateFloorCard() =>
        FloorCard.Visibility = OverrideVolume.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void SyncOffset_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SyncOffsetValue.Text = $"{SyncOffset.Value:+0;-0;0} ms";
        if (_loading) return;
        _ctx.Settings.SyncOffsetMs = (int)SyncOffset.Value;
        _ctx.Changed();
    }
}
