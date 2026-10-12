namespace FindMyRidingApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new IndexPage();
        Android.Util.Log.Info("FindMyRiding", "App ctor: MainPage set");
    }
}
