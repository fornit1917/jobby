using MudBlazor;

namespace Jobby.Dashboard.Client.Theme;

public static class JobbyTheme
{
    public static readonly MudTheme Instance = new()
    {
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["IBM Plex Sans", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "sans-serif"],
                FontSize = "13.5px",
                LineHeight = "1.5",
                FontWeight = "400",
            },
            H6 = new H6Typography { FontSize = "15px", FontWeight = "600", LetterSpacing = "-0.01em" },
            Subtitle2 = new Subtitle2Typography { FontSize = "11px", FontWeight = "600", LetterSpacing = "0.06em" },
            Button = new ButtonTypography { FontWeight = "500", TextTransform = "none", FontSize = "12.5px" },
            Body1 = new Body1Typography { FontSize = "13.5px" },
            Body2 = new Body2Typography { FontSize = "13px" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "4px",
            DrawerWidthLeft = "240px",
            DrawerMiniWidthLeft = "60px",
            AppbarHeight = "56px",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#a07cf5",
            Secondary = "#a07cf5",
            Background = "#0d0f13",
            BackgroundGray = "#1a1e25",
            Surface = "#14171c",
            DrawerBackground = "#14171c",
            DrawerText = "#99a0ab",
            DrawerIcon = "#99a0ab",
            AppbarBackground = "#0d0f13",
            AppbarText = "#e7e9ee",
            TextPrimary = "#e7e9ee",
            TextSecondary = "#99a0ab",
            TextDisabled = "#6b7280",
            ActionDefault = "#99a0ab",
            ActionDisabled = "#6b728088",
            Divider = "rgba(255,255,255,0.07)",
            DividerLight = "rgba(255,255,255,0.12)",
            LinesDefault = "rgba(255,255,255,0.07)",
            LinesInputs = "rgba(255,255,255,0.12)",
            TableLines = "rgba(255,255,255,0.07)",
            TableStriped = "#1a1e25",
            TableHover = "#1a1e25",
            Success = "#46c08a",
            Info = "#4ea6f5",
            Warning = "#e8a13c",
            Error = "#ef6b6b",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#a07cf5",
            Secondary = "#a07cf5",
            Background = "#f4f5f7",
            BackgroundGray = "#f7f8fa",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#5b626e",
            DrawerIcon = "#5b626e",
            AppbarBackground = "#ffffff",
            AppbarText = "#1a1d23",
            TextPrimary = "#1a1d23",
            TextSecondary = "#5b626e",
            TextDisabled = "#8b93a1",
            ActionDefault = "#5b626e",
            ActionDisabled = "#8b93a188",
            Divider = "rgba(17,24,39,0.09)",
            DividerLight = "rgba(17,24,39,0.16)",
            LinesDefault = "rgba(17,24,39,0.09)",
            LinesInputs = "rgba(17,24,39,0.16)",
            TableLines = "rgba(17,24,39,0.09)",
            TableStriped = "#f7f8fa",
            TableHover = "#f7f8fa",
            Success = "#1f9d63",
            Info = "#2b86d6",
            Warning = "#c2810f",
            Error = "#d64545",
        },
    };
}