using CanadaRidingTagger;
using Microsoft.Maui.Devices.Sensors;

namespace FindMyRidingApp;

public partial class IndexPage : ContentPage
{
    static RidingTagger? tagger;

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

    async void OnRefreshClicked(object? sender, EventArgs e) => await RefreshAsync();

    async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = false;
        Android.Util.Log.Info("FindMyRiding", "RefreshAsync start");
        try
        {
            StatusLabel.Text = "Requesting location…";
            var geo = Geolocation.Default;
            var position = await geo.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High));
            double lat = position.Latitude;
            double lon = position.Longitude;
            Android.Util.Log.Info("FindMyRiding", $"Location: {lat}, {lon}");

            LocationLabel.Text = $"Location: {lat:0.0000}, {lon:0.0000}";
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
        catch (Exception ex)
        {
            Android.Util.Log.Error("FindMyRiding", "RefreshAsync error: " + ex);
            StatusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }
}
