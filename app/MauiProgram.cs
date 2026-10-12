using Microsoft.Maui.Hosting;

namespace FindMyRidingApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Android.Util.Log.Info("FindMyRiding", "CreateMauiApp BEGIN");
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        var app = builder.Build();
        Android.Util.Log.Info("FindMyRiding", "CreateMauiApp END (built)");
        return app;
    }
}
