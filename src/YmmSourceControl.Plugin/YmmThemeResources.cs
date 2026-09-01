using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin;

namespace YmmSourceControl.Plugin;

internal static class YmmThemeResources
{
    internal const string BackgroundBrush = "YmmSourceControl.BackgroundBrush";
    internal const string BackgroundColor = "YmmSourceControl.BackgroundColor";
    internal const string ForegroundBrush = "YmmSourceControl.ForegroundBrush";
    internal const string ForegroundColor = "YmmSourceControl.ForegroundColor";
    internal const string MutedBrush = "YmmSourceControl.MutedBrush";
    internal const string MutedColor = "YmmSourceControl.MutedColor";
    internal const string BorderBrush = "YmmSourceControl.BorderBrush";
    internal const string BorderColor = "YmmSourceControl.BorderColor";
    internal const string AccentBrush = "YmmSourceControl.AccentBrush";
    internal const string AccentColor = "YmmSourceControl.AccentColor";
    internal const string AccentBackgroundBrush = "YmmSourceControl.AccentBackgroundBrush";
    internal const string AccentBackgroundColor = "YmmSourceControl.AccentBackgroundColor";
    internal const string AccentForegroundBrush = "YmmSourceControl.AccentForegroundBrush";
    internal const string AccentForegroundColor = "YmmSourceControl.AccentForegroundColor";
    internal const string DangerBrush = "YmmSourceControl.DangerBrush";
    internal const string DangerColor = "YmmSourceControl.DangerColor";
    internal const string PanelBrush = "YmmSourceControl.PanelBrush";
    internal const string PanelColor = "YmmSourceControl.PanelColor";
    internal const string InputBrush = "YmmSourceControl.InputBrush";
    internal const string InputColor = "YmmSourceControl.InputColor";
    internal const string HoverBrush = "YmmSourceControl.HoverBrush";
    internal const string HoverColor = "YmmSourceControl.HoverColor";
    internal const string HoverBorderBrush = "YmmSourceControl.HoverBorderBrush";
    internal const string HoverBorderColor = "YmmSourceControl.HoverBorderColor";
    internal const string WarningBackgroundBrush = "YmmSourceControl.WarningBackgroundBrush";
    internal const string WarningBackgroundColor = "YmmSourceControl.WarningBackgroundColor";
    internal const string WarningBorderBrush = "YmmSourceControl.WarningBorderBrush";
    internal const string WarningBorderColor = "YmmSourceControl.WarningBorderColor";
    internal const string WarningForegroundBrush = "YmmSourceControl.WarningForegroundBrush";
    internal const string WarningForegroundColor = "YmmSourceControl.WarningForegroundColor";
    internal const string SuccessBrush = "YmmSourceControl.SuccessBrush";
    internal const string SuccessColor = "YmmSourceControl.SuccessColor";

    private static readonly object SyncRoot = new();
    private static bool isInitialized;

    internal static void Initialize()
    {
        lock (SyncRoot)
        {
            if (isInitialized)
            {
                return;
            }

            ThemeManager.Instance.ThemeChanged += ThemeChanged;
            isInitialized = true;
        }

        ApplyTheme(ThemeManager.Instance.CurrentTheme);
    }

    private static void ThemeChanged(object? sender, ThemeManager.ThemeChangedEventArgs e)
    {
        ApplyTheme(e.Theme);
    }

    private static void ApplyTheme(string? theme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        var isDark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)
            || string.Equals(theme, "Black", StringComparison.OrdinalIgnoreCase);
        var palette = isDark ? DarkPalette : LightPalette;

        foreach (var (key, color) in palette)
        {
            resources[key] = key.EndsWith("Color", StringComparison.Ordinal)
                ? (object)(Color)ColorConverter.ConvertFromString(color)!
                : CreateBrush(color);
        }
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
        brush.Freeze();
        return brush;
    }

    private static IReadOnlyDictionary<string, string> LightPalette { get; } = new Dictionary<string, string>
    {
        [BackgroundBrush] = "#FFFFFFFF",
        [BackgroundColor] = "#FFFFFFFF",
        [ForegroundBrush] = "#FF333333",
        [ForegroundColor] = "#FF333333",
        [MutedBrush] = "#FF777777",
        [MutedColor] = "#FF777777",
        [BorderBrush] = "#FFD7D7D7",
        [BorderColor] = "#FFD7D7D7",
        [AccentBrush] = "#FF2B6CB0",
        [AccentColor] = "#FF2B6CB0",
        [AccentBackgroundBrush] = "#FF2F75B5",
        [AccentBackgroundColor] = "#FF2F75B5",
        [AccentForegroundBrush] = "#FFFFFFFF",
        [AccentForegroundColor] = "#FFFFFFFF",
        [DangerBrush] = "#FFB42318",
        [DangerColor] = "#FFB42318",
        [PanelBrush] = "#FFFAFAFA",
        [PanelColor] = "#FFFAFAFA",
        [InputBrush] = "#FFFFFFFF",
        [InputColor] = "#FFFFFFFF",
        [HoverBrush] = "#FFF2F6FA",
        [HoverColor] = "#FFF2F6FA",
        [HoverBorderBrush] = "#FFAFC8E0",
        [HoverBorderColor] = "#FFAFC8E0",
        [WarningBackgroundBrush] = "#FFFFF9E6",
        [WarningBackgroundColor] = "#FFFFF9E6",
        [WarningBorderBrush] = "#FFE6A700",
        [WarningBorderColor] = "#FFE6A700",
        [WarningForegroundBrush] = "#FF6B4F00",
        [WarningForegroundColor] = "#FF6B4F00",
        [SuccessBrush] = "#FF3A8F3A",
        [SuccessColor] = "#FF3A8F3A",
    };

    private static IReadOnlyDictionary<string, string> DarkPalette { get; } = new Dictionary<string, string>
    {
        [BackgroundBrush] = "#FF202124",
        [BackgroundColor] = "#FF202124",
        [ForegroundBrush] = "#FFF1F3F4",
        [ForegroundColor] = "#FFF1F3F4",
        [MutedBrush] = "#FFB8B9BA",
        [MutedColor] = "#FFB8B9BA",
        [BorderBrush] = "#FF4A4D51",
        [BorderColor] = "#FF4A4D51",
        [AccentBrush] = "#FF8AB4F8",
        [AccentColor] = "#FF8AB4F8",
        [AccentBackgroundBrush] = "#FF3C6EA8",
        [AccentBackgroundColor] = "#FF3C6EA8",
        [AccentForegroundBrush] = "#FFFFFFFF",
        [AccentForegroundColor] = "#FFFFFFFF",
        [DangerBrush] = "#FFFF8A80",
        [DangerColor] = "#FFFF8A80",
        [PanelBrush] = "#FF2B2D30",
        [PanelColor] = "#FF2B2D30",
        [InputBrush] = "#FF2B2D30",
        [InputColor] = "#FF2B2D30",
        [HoverBrush] = "#FF34373B",
        [HoverColor] = "#FF34373B",
        [HoverBorderBrush] = "#FF5E83B3",
        [HoverBorderColor] = "#FF5E83B3",
        [WarningBackgroundBrush] = "#FF3A321E",
        [WarningBackgroundColor] = "#FF3A321E",
        [WarningBorderBrush] = "#FFE6A700",
        [WarningBorderColor] = "#FFE6A700",
        [WarningForegroundBrush] = "#FFFFD978",
        [WarningForegroundColor] = "#FFFFD978",
        [SuccessBrush] = "#FF81C995",
        [SuccessColor] = "#FF81C995",
    };
}
