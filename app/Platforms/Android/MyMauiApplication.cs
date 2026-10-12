using Android.Runtime;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace FindMyRidingApp;

public class MyMauiApplication : MauiApplication
{
    public MyMauiApplication(nint handle, JniHandleOwnership ownership) : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
