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
        RidingResult result = await Task.Run(() =>
        {
            if (tagger is null)
            {
                Android.Util.Log.Info("FindMyRiding", "Ensuring data root");
                string dataRoot = RidingData.EnsureDataRoot();
                // Publish only once fully loaded; a partially loaded tagger would
                // miss datasets and report wrong "unknown" results.
                var fresh = new RidingTagger();
                foreach (var source in StandardDatasets.All(dataRoot))
                {
                    fresh.Load(source);
                }
                tagger = fresh;
                Android.Util.Log.Info("FindMyRiding", "Tagger loaded");
            }
            var found = tagger.FindRidings(lat, lon);
            Android.Util.Log.Info("FindMyRiding", $"FindRidings fed={found.Federal?.NameEn} prov={found.Provincial?.NameEn}");
            return found;
        });

        string fed = FormatRidingName(result.Federal), prov = FormatRidingName(result.Provincial);
        FederalIdLabel.Text = FormatRidingId(result.Federal);
        ProvincialIdLabel.Text = FormatRidingId(result.Provincial);
        SetRidingValue(FederalLabel, fed);
        SetRidingValue(ProvincialLabel, prov);
        ProvincialHeading.Text = result.Jurisdiction is null
            ? "PROVINCIAL / TERRITORIAL RIDING"
            : $"{result.Jurisdiction.Value.DisplayName()} RIDING".ToUpperInvariant();
        double km = DistanceKm(lat, lon, CentreBlockLat, CentreBlockLon);
        SetStatus($"{FormatDistanceKm(km)} from Parliament Hill");
        Android.Util.Log.Info("FindMyRiding", $"Labels set fed={fed} prov={prov}");
    }

    // Dataset id number shown beside the section heading ("No. N/A" for jurisdictions
    // whose datasets carry no id field — the three territories).
    static string FormatRidingId(District? d) => d is null ? "" : $"No. {(d.Id == d.NameEn ? "N/A" : d.Id)}";

    static string FormatRidingName(District? d) => d is null ? "unknown" : d.NameEn;

    // Centre Block of the Parliament Buildings, Ottawa: 45°25′30″N 75°42′00″W.
    static readonly double CentreBlockLat = 45.4250;
    static readonly double CentreBlockLon = -75.7000;

    // Great-circle distance in km (haversine, mean Earth radius).
    static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0088;
        double p1 = lat1 * Math.PI / 180, p2 = lat2 * Math.PI / 180;
        double dP = (lat2 - lat1) * Math.PI / 180, dL = (lon2 - lon1) * Math.PI / 180;
        double h = Math.Sin(dP / 2) * Math.Sin(dP / 2)
                 + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dL / 2) * Math.Sin(dL / 2);
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }

    // m below 1 km, one decimal below 100 km, rounded km with grouping beyond.
    static string FormatDistanceKm(double km)
    {
        if (km < 1) return $"{(long)Math.Round(km * 1000):#,0} m";
        if (km < 100) return km.ToString("0.0", CultureInfo.InvariantCulture) + " km";
        return ((long)Math.Round(km)).ToString("#,0", CultureInfo.InvariantCulture) + " km";
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
