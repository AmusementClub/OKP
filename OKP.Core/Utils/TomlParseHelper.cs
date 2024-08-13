using OKP.Core.Interface;
using static OKP.Core.Interface.TorrentContent;
using Tommy;

namespace OKP.Core.Utils;

internal static class TomlParseHelper
{
    internal static TorrentContent DeserializeTorrentContent(string filePath)
    {
        using var reader = File.OpenText(filePath);
        var table = TOML.Parse(reader);

        var torrenC = new TorrentContent()
        {
            DisplayName = ConvertToString(table["display_name"]),
            GroupName = ConvertToString(table["group_name"]),
            Poster = ConvertToString(table["poster"]),
            About = ConvertToString(table["about"]),
            FilenameRegex = ConvertToString(table["filename_regex"]),
            ResolutionRegex = ConvertToString(table["resolution_regex"]),
            SettingPath = ConvertToString(table["setting_path"]),
            CookiePath = ConvertToString(table["cookie_path"]),
        };

        var tags = table["tags"];
        if (tags.HasValue)
        {
            torrenC.Tags = new List<ContentTypes>(tags.ChildrenCount);
            foreach (var tag in tags)
            {
                if (Enum.TryParse(tag.ToString(), out ContentTypes tagEnum))
                {
                    torrenC.Tags.Add(tagEnum);
                }
            }
        }

        var torrentFlags = table["torrent_flags"];
        if (torrentFlags.HasValue)
        {
            torrenC.TorrentFlags = new List<NyaaTorrentFlags>(torrentFlags.ChildrenCount);
            foreach (var tag in tags)
            {
                if (Enum.TryParse(tag.ToString(), out NyaaTorrentFlags tagEnum))
                {
                    torrenC.TorrentFlags.Add(tagEnum);
                }
            }
        }

        var introTemplates = table["intro_template"];
        if (introTemplates.HasValue)
        {
            torrenC.IntroTemplate = new List<Template>(introTemplates.ChildrenCount);
            GetTemplates(introTemplates, torrenC.IntroTemplate);
        }

        return torrenC;
    }

    internal static UserProperties DeserializeUserProperties(string filePath)
    {
        using var reader = File.OpenText(filePath);
        var table = TOML.Parse(reader);

        var userProp = new UserProperties();
        var userProps = table["user_prop"];
        if (userProps.HasValue)
        {
            userProp.UserProp = new List<Template>(userProps.ChildrenCount);
            GetTemplates(userProps, userProp.UserProp);
        }

        return userProp;
    }

    private static string? ConvertToString(TomlNode v) => v.HasValue ? v.ToString() : null;

    private static void GetTemplates(TomlNode node, List<Template> templates)
    {
        foreach (TomlTable introTemplate in node)
        {
            templates.Add(
                new Template()
                {
                    Site = ConvertToString(introTemplate["site"]),
                    Name = ConvertToString(introTemplate["name"]),
                    Content = ConvertToString(introTemplate["content"]),
                    Cookie = ConvertToString(introTemplate["cookie"]),
                    UserAgent = ConvertToString(introTemplate["user_agent"]),
                    Proxy = ConvertToString(introTemplate["proxy"]),
                    DisplayName = ConvertToString(introTemplate["display_name"]),
                });
        }
    }
}
