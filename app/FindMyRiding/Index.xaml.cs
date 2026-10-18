using CanadaRidingTagger;
using System.Globalization;
using Microsoft.Maui.Devices.Sensors;

namespace FindMyRidingApp;

public partial class IndexPage : ContentPage
{
    static RidingTagger? tagger;
    double? _manualLat, _manualLon;
    bool _fsaMode;

    // Last displayed result: coordinates are retained so the vintage pickers can
    // re-resolve the same point against a past distribution.
    double? _lastLat, _lastLon;
    string _lastLocationText = "";
    int? _federalYear, _provincialYear;
    bool _updatingPickers;

    // Picker items are "Current (year)" plus past years, newest first; these hold
    // the year per picker-item index (index 0 = current map).
    readonly List<int> _federalYears = [], _provincialYears = [];

    public IndexPage()
    {
        InitializeComponent();
        SetMode(false);
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
                _manualLat = lat;
                _manualLon = lon;
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
            if (_fsaMode)
            {
                await LoadFsaAsync(FsaEntry.Text ?? "");
            }
            else
            {
                if (!TryParseCoordinate(LatEntry.Text, out double lat, 90))
                    throw new FormatException("Invalid latitude: expected a number between -90 and 90.");
                if (!TryParseCoordinate(LngEntry.Text, out double lon, 180))
                    throw new FormatException("Invalid longitude: expected a number between -180 and 180.");

                _manualLat = lat;
                _manualLon = lon;
                await LoadRidingsAsync(lat, lon, $"Location (manual): {lat:0.0000}, {lon:0.0000}");
            }
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

    void OnModeLatLongClicked(object? sender, EventArgs e) => SetMode(false);

    void OnModeFsaClicked(object? sender, EventArgs e) => SetMode(true);

    // Switches the manual search input between coordinates and postal code;
    // the active chip takes the teal chip color, the inactive chip stays neutral.
    void SetMode(bool fsa)
    {
        _fsaMode = fsa;
        CoordsGrid.IsVisible = !fsa;
        FsaEntry.IsVisible = fsa;
        bool dark = ThemedStatusBarBehavior.IsSystemDarkMode();
        (Button active, Button inactive) = fsa ? (ModeFsaButton, ModeLatLongButton) : (ModeLatLongButton, ModeFsaButton);
        active.BackgroundColor = dark ? AccentTealDark : AccentTeal;
        active.TextColor = Colors.White;
        inactive.BackgroundColor = dark ? DarkChip : LightChip;
        inactive.TextColor = dark ? DarkValue : LightValue;
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
        SetStatus("Loading riding data…");
        // A fresh lookup starts on the current maps; vintage selections only
        // apply while re-resolving the last displayed point.
        _federalYear = _provincialYear = null;
        RidingResult result = await Task.Run(() =>
        {
            var found = EnsureTagger().FindRidings(lat, lon);
            Android.Util.Log.Info("FindMyRiding", $"FindRidings fed={found.Federal?.NameEn} prov={found.Provincial?.NameEn}");
            return found;
        });
        ShowResult(result, lat, lon, locationText);
    }

    async Task LoadFsaAsync(string fsaText)
    {
        SetStatus("Loading riding data…");
        try
        {
            (RidingResult result, double lat, double lon) = await Task.Run(() =>
            {
                var tagger = EnsureTagger();
                (double lat, double lon) = FsaCentroids.Get(fsaText);
                var found = tagger.FindRidings(lat, lon);
                Android.Util.Log.Info("FindMyRiding", $"FindRidingsByFsa({fsaText.Trim()}) fed={found.Federal?.NameEn} prov={found.Provincial?.NameEn}");
                return (found, lat, lon);
            });
            _manualLat = lat;
            _manualLon = lon;
            ShowResult(result, lat, lon, $"Postal code: {fsaText.Trim()}");
        }
        catch (FormatException)
        {
            SetStatus("Error: invalid postal code — e.g. K1A or K1A 0B1.", error: true);
        }
        catch (KeyNotFoundException)
        {
            SetStatus("Error: FSA not in the 2021 census table.", error: true);
        }
    }

    // Lazily loads the shared tagger (all datasets).
    static RidingTagger EnsureTagger()
    {
        if (tagger is not null)
            return tagger;
        Android.Util.Log.Info("FindMyRiding", "Ensuring data root");
        string dataRoot = RidingData.EnsureDataRoot();
        // Publish only once fully loaded; a partially loaded tagger would
        // miss datasets and report wrong "unknown" results.
        var fresh = new RidingTagger();
        foreach (var source in StandardDatasets.AllVintages(dataRoot))
        {
            fresh.Load(source);
        }
        tagger = fresh;
        Android.Util.Log.Info("FindMyRiding", "Tagger loaded");
        return fresh;
    }

    void ShowResult(RidingResult result, double lat, double lon, string locationText)
    {
        _lastLat = lat;
        _lastLon = lon;
        _lastLocationText = locationText;
        LocationLabel.Text = locationText;
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
        UpdateVintagePickers(result.Jurisdiction);
        Android.Util.Log.Info("FindMyRiding", $"Labels set fed={fed} prov={prov}");
    }

    // Rebuilds the vintage pickers with the map years loaded for the result's
    // jurisdiction, keeping a selection that is still available.
    void UpdateVintagePickers(Jurisdiction? jurisdiction)
    {
        var t = EnsureTagger();
        _updatingPickers = true;
        try
        {
            SetVintagePicker(FederalYearPicker, _federalYears, t.Vintages(null), ref _federalYear);
            SetVintagePicker(ProvincialYearPicker, _provincialYears,
                jurisdiction is null ? Array.Empty<int>() : t.Vintages(jurisdiction.Value), ref _provincialYear);
            VintageGrid.IsVisible = true;
        }
        finally
        {
            _updatingPickers = false;
        }
    }

    static void SetVintagePicker(Picker picker, List<int> years, IReadOnlyList<int> vintages, ref int? selection)
    {
        years.Clear();
        var items = new List<string>();
        if (vintages.Count == 0)
        {
            items.Add("Current");
            years.Add(0);
        }
        else
        {
            int current = vintages[^1];
            items.Add($"Current ({current})");
            years.Add(current);
            for (int i = vintages.Count - 2; i >= 0; i--)
            {
                items.Add(vintages[i].ToString());
                years.Add(vintages[i]);
            }
        }
        int index = selection is null ? 0 : Math.Max(0, years.IndexOf(selection.Value));
        picker.ItemsSource = items;
        picker.SelectedIndex = index;
        picker.Title = items[index];
    }

    // Re-resolves the last displayed point against the picked map years.
    async void OnVintagePickerChanged(object? sender, EventArgs e)
    {
        if (_updatingPickers || _lastLat is null || _lastLon is null)
            return;
        int? federalYear = PickerYear(FederalYearPicker, _federalYears);
        int? provincialYear = PickerYear(ProvincialYearPicker, _provincialYears);
        _federalYear = federalYear;
        _provincialYear = provincialYear;
        double lat = _lastLat.Value, lon = _lastLon.Value;
        SetStatus("Loading riding data…");
        try
        {
            RidingResult result = await Task.Run(() => EnsureTagger().FindRidings(lat, lon, federalYear, provincialYear));
            ShowResult(result, lat, lon, _lastLocationText);
        }
        catch (ArgumentException ex)
        {
            Android.Util.Log.Error("FindMyRiding", "Vintage re-resolve error: " + ex);
            SetStatus($"Error: {ex.Message}", error: true);
        }
    }

    static int? PickerYear(Picker picker, List<int> years)
    {
        int index = picker.SelectedIndex;
        return index > 0 && index < years.Count ? years[index] : null;
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
    static readonly Color LightChip = Color.FromHex("#E4E7EA");
    static readonly Color DarkChip = Color.FromHex("#2A3037");
    static readonly Color AccentTeal = Color.FromHex("#0F766E");
    static readonly Color AccentTealDark = Color.FromHex("#14B8A6");

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
