using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using Microsoft.Maui.Controls;

using MauiPlatform = Microsoft.Maui.ApplicationModel.Platform;
namespace FindMyRidingApp;

/// <summary>
/// A <see cref="StatusBarBehavior"/> that derives the status bar color and glyph
/// style from the system theme: a dark bar with light glyphs in dark mode,
/// a light bar with dark glyphs in light mode.
/// The values are recomputed on every (re)attach, so activity recreation on a
/// system theme change is covered.
/// </summary>
public class ThemedStatusBarBehavior : StatusBarBehavior
{
    static readonly Color DarkBar = Colors.Black;
    static readonly Color LightBar = Colors.White;

    protected override void OnAttachedTo(Page page, Android.Views.View platformView)
    {
        UpdateForCurrentTheme();
        base.OnAttachedTo(page, platformView);
    }

    public void UpdateForCurrentTheme()
    {
        bool isDark = IsSystemDarkMode();
        Android.Util.Log.Info("FindMyRiding", $"StatusBarBehavior theme update isDark={isDark}");
        StatusBarColor = isDark ? DarkBar : LightBar;
        StatusBarStyle = isDark ? StatusBarStyle.LightContent : StatusBarStyle.DarkContent;
    }

    static bool IsSystemDarkMode()
    {
        var activity = MauiPlatform.CurrentActivity;
        return activity is not null
            && (activity.Resources.Configuration.UiMode & Android.Content.Res.UiMode.NightMask)
            == Android.Content.Res.UiMode.NightYes;
    }
}
