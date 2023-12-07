using OKP.Core.Interface;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OKP.Core.Utils;

public class Tag
{
    [JsonConverter(typeof(JsonStringEnumConverter<TorrentContent.ContentTypes>))]
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
        if (Key == null || types.Contains((TorrentContent.ContentTypes)Key))
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

    public List<string?> FindTagAll(List<TorrentContent.ContentTypes>? types, bool isRoot = true)
    {
        var tags = new List<string?>();
        // if no types specified, return the value of the current tag
        if (types == null || types.Count == 0)
        {
            tags.Add(Value);
        }
        // If no key is specified for the current tag, search for sub-tags
        if (Key == null || (types != null && types.Contains((TorrentContent.ContentTypes)Key)))
        {
            if (SubTags.Count > 0)
            {
                foreach (var child in SubTags)
                {
                    var tmp = child.FindTag(types, false);
                    if (tmp != null)
                    {
                        tags.Add(tmp);
                    }
                }
            }
            if (Value != null)
            {
                tags.Add(Value);
            }
        }
        tags.RemoveAll(string.IsNullOrEmpty);
        return tags;
    }
}

[JsonSerializable(typeof(Tag))]
internal partial class TagSourceGenerationContext : JsonSerializerContext;

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
            var ret = JsonSerializer.Deserialize(content, TagSourceGenerationContext.Default.Tag);
            return ret ?? new Tag();
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to load tag config");
        }
        return new Tag();
    }
}
