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
                StatusLabel.Text = "Requesting location…";
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
            StatusLabel.Text = $"Error: {ex.Message}";
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
            StatusLabel.Text = $"Error: {ex.Message}";
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
        StatusLabel.Text = "Loading riding data…";
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

        FederalLabel.Text = $"Federal riding: {fed}";
        ProvincialLabel.Text = $"Provincial/Territorial riding: {prov}";
        StatusLabel.Text = "Done.";
        Android.Util.Log.Info("FindMyRiding", $"Labels set fed={fed} prov={prov}");
    }
}
