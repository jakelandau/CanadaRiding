using Android.App;
using Android.OS;
using Microsoft.Maui;

namespace FindMyRidingApp;

[Activity(MainLauncher = true, Theme = "@style/MauiTheme")]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		Android.Util.Log.Info("FindMyRiding", "MainActivity.OnCreate BEGIN");
		base.OnCreate(savedInstanceState);
		Android.Util.Log.Info("FindMyRiding", "MainActivity.OnCreate END after base");
	}
}
