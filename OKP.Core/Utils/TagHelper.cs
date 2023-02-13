using System.Text.Json;
using System.Text.Json.Serialization;
using OKP.Core.Interface;
using Serilog;

namespace OKP.Core.Utils;

public class Tag
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TorrentContent.ContentTypes? Key { get; set; }

    public string? Value { get; set; }

    public List<Tag> SubTags { get; set; }

    public Tag()
    {
        SubTags = new List<Tag>();
    }

    public string? FindTag(List<TorrentContent.ContentTypes>? types, bool isRoot = true)
    {
        // if no types specified, return the value of the current tag
        if (types == null || types.Count == 0)
        {
            return Value;
        }
        // If no key is specified for the current tag, search for sub-tags
        if (Key == null || types.Contains((TorrentContent.ContentTypes) Key))
        {
            if (SubTags.Count > 0)
            {
                foreach (var child in SubTags)
                {
                    var tmp = child.FindTag(types, false);
                    if (tmp != null)
                    {
                        return tmp;
                    }
                }
            }
            if (Value != null)
            {
                return Value;
            }
        }
        return null;
    }
}

public static class TagHelper
{
    public static Tag LoadTagConfig(string configName)
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "tags", configName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Tag config file not found");
        }
        var content = File.ReadAllText(configPath);
        try
        {
            var ret = JsonSerializer.Deserialize<Tag>(content);
            return ret;
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to load tag config");
        }
        return new Tag();
    }
}
