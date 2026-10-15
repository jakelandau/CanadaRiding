using CanadaRidingTagger;
using System.Globalization;
using Microsoft.Maui.Devices.Sensors;

namespace FindMyRidingApp;

public partial class IndexPage : ContentPage
{
    static RidingTagger? tagger;
    double? _manualLat, _manualLon;

    public IndexPage()
    {
        InitializeComponent();
        Android.Util.Log.Info("FindMyRiding", "IndexPage ctor");
        Loaded += async (_, _) =>
        {
            Android.Util.Log.Info("FindMyRiding", "IndexPage Loaded");
            await RefreshAsync();
        };
    }

protected override void OnAppearing()
{
	base.OnAppearing();
	Android.Util.Log.Info("FindMyRiding", "IndexPage OnAppearing");
	StatusBarBehavior.UpdateForCurrentTheme();
}

    async Task RefreshAsync()
    {
        UseGpsButton.IsEnabled = false;
        Android.Util.Log.Info("FindMyRiding", "RefreshAsync start");
        try
        {
            double? lat = _manualLat, lon = _manualLon;
            if (lat is null || lon is null)
            {
                SetStatus("Requesting location…");
                var geo = Geolocation.Default;
                var position = await geo.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High));
                lat = position.Latitude;
                lon = position.Longitude;
                Android.Util.Log.Info("FindMyRiding", $"Location: {lat}, {lon}");
            }
            await LoadRidingsAsync(lat.Value, lon.Value, $"Location: {lat:0.0000}, {lon:0.0000}");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("FindMyRiding", "RefreshAsync error: " + ex);
            SetStatus($"Error: {ex.Message}", error: true);
        }
        finally
        {
            UseGpsButton.IsEnabled = true;
        }
    }

    async void OnApplyClicked(object? sender, EventArgs e)
    {
        ApplyButton.IsEnabled = false;
        try
        {
            if (!TryParseCoordinate(LatEntry.Text, out double lat, 90))
                throw new FormatException("Invalid latitude: expected a number between -90 and 90.");
            if (!TryParseCoordinate(LngEntry.Text, out double lon, 180))
                throw new FormatException("Invalid longitude: expected a number between -180 and 180.");

            _manualLat = lat;
            _manualLon = lon;
            await LoadRidingsAsync(lat, lon, $"Location (manual): {lat:0.0000}, {lon:0.0000}");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("FindMyRiding", "OnApplyClicked error: " + ex);
            SetStatus($"Error: {ex.Message}", error: true);
        }
        finally
        {
            ApplyButton.IsEnabled = true;
        }
    }

    async void OnUseGpsClicked(object? sender, EventArgs e)
    {
        _manualLat = _manualLon = null;
        await RefreshAsync();
    }

    static bool TryParseCoordinate(string? text, out double value, double maxAbsolute)
    {
        value = 0;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;
        return !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= maxAbsolute;
    }

    async Task LoadRidingsAsync(double lat, double lon, string locationText)
    {
        LocationLabel.Text = locationText;
        SetStatus("Loading riding data…");
        var (fed, prov) = await Task.Run(() =>
        {
            if (tagger is null)
            {
                Android.Util.Log.Info("FindMyRiding", "Ensuring data root");
                string dataRoot = RidingData.EnsureDataRoot();
                tagger = new RidingTagger();
                foreach (var source in StandardDatasets.All(dataRoot))
                {
                    tagger.Load(source);
                }
                Android.Util.Log.Info("FindMyRiding", "Tagger loaded");
            }
            var result = tagger.FindRidings(lat, lon);
            Android.Util.Log.Info("FindMyRiding", $"FindRidings fed={result.Federal?.NameEn} prov={result.Provincial?.NameEn}");
            return (result.Federal?.NameEn ?? "unknown", result.Provincial?.NameEn ?? "unknown");
        });

        SetRidingValue(FederalLabel, fed);
        SetRidingValue(ProvincialLabel, prov);
        SetStatus("Done.");
        Android.Util.Log.Info("FindMyRiding", $"Labels set fed={fed} prov={prov}");
    }

    // Mirrors the palette in App.xaml, set imperatively so status/result color
    // tracks meaning at runtime (error vs. ok; found vs. unknown).
    static readonly Color LightMuted = Color.FromHex("#6F767E");
    static readonly Color DarkMuted = Color.FromHex("#98A0A8");
    static readonly Color LightError = Color.FromHex("#C40E2B");
    static readonly Color DarkError = Color.FromHex("#FF8A94");
    static readonly Color LightValue = Color.FromHex("#1F2328");
    static readonly Color DarkValue = Color.FromHex("#F2F4F6");

    void SetStatus(string text, bool error = false)
    {
        bool dark = ThemedStatusBarBehavior.IsSystemDarkMode();
        StatusLabel.Text = text;
        StatusLabel.TextColor = error ? (dark ? DarkError : LightError) : (dark ? DarkMuted : LightMuted);
    }

    static void SetRidingValue(Label label, string value)
    {
        bool dark = ThemedStatusBarBehavior.IsSystemDarkMode();
        label.Text = value;
        label.TextColor = value == "unknown" ? (dark ? DarkMuted : LightMuted) : (dark ? DarkValue : LightValue);
    }
}
