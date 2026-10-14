using Microsoft.Maui.Hosting;
using CommunityToolkit.Maui;

namespace FindMyRidingApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Android.Util.Log.Info("FindMyRiding", "CreateMauiApp BEGIN");
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseMauiCommunityToolkit();
        var app = builder.Build();
        Android.Util.Log.Info("FindMyRiding", "CreateMauiApp END (built)");
        return app;
    }
}
