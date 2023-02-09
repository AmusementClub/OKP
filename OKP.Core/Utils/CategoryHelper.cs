using OKP.Core.Interface;
using Serilog;
using static OKP.Core.Utils.Constants;

namespace OKP.Core.Utils
{
    internal class CategoryHelper
    {
        public static string SelectCategory(List<TorrentContent.ContentTypes>? tags, string site)
        {
            var category = "";
            if (!SupportSiteName.Contains(site))
            {
                Log.Error("未受支持或输入错误的站点");
            }
            else
            {
                category = site switch
                {
                    "nyaa" => CastCategory(tags, "nyaa.json") ?? "3_2",
                    "dmhy" => CastCategory(tags, "dmhy.json") ?? "1",
                    "acgrip" => CastCategory(tags, "acgrip.json") ?? "9",
                    "acgnx_asia" => CastCategory(tags, "acgnx_asia.json") ?? "19-1",
                    "acgnx_global" => CastCategory(tags, "acgnx_global.json") ?? "12-1",
                    _ => throw new NotImplementedException()
                };
            }
            return category;
        }

        private static string? CastCategory(List<TorrentContent.ContentTypes>? tags, string mapFile)
        {
            var tagConfig = TagHelper.LoadTagConfig(mapFile);
            return tagConfig.FindTag(tags);
        }
    }
}
