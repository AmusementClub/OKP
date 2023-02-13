namespace OKP.Core.Utils;

public static class Constants
{
    public const string DefaultSettingFileName = "setting.toml";
    public const string DefaultLogFileName = "log.txt";
    public const string UserPropertiesFileName = "OKP_userprop.toml";
    public static readonly string[] SupportSiteName = { "nyaa", "dmhy", "acgrip", "acgnx_asia", "acgnx_global", "bangumi" };
    public const string DefaultCookiePath = "okp_cookies";
    public const string DefauttCookieFile = "cookies";

    public enum categoryAcgnxAsia : ushort
    {
        Anime = 1,
        AnimeCollection = 2,
        Music = 6,
        JapaneseFilm = 10,
        Raw = 11,
        Tokusatsu = 18,
        Others = 19
    }
    public enum categoryAcgnxGlobal : ushort
    {
        AnimeRaw = 3,
        Anime = 4,
        AnimeMv = 5,
        MusicLossless = 7,
        MusicLossy = 8,
        LiveActionRaw = 15,
        LiveAction = 16,
        LiveIdol = 17,
        Photos = 22,
        Graphics = 23,
        Other = 25
    }
}
