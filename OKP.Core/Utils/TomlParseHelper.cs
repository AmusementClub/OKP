using CsToml.Extensions;
using OKP.Core.Interface;

namespace OKP.Core.Utils;

internal static class TomlParseHelper
{
    internal static TorrentContent DeserializeTorrentContent(string filePath)
    {
        var torrentC = CsTomlFileSerializer.Deserialize<TorrentContent>(filePath);
        return torrentC;
    }

    internal static UserProperties DeserializeUserProperties(string filePath)
    {
        var userProp = CsTomlFileSerializer.Deserialize<UserProperties>(filePath);
        return userProp;
    }
}
