using OKP.Core.Interface;
using Serilog;
using static OKP.Core.Utils.Constants;

namespace OKP.Core.Utils
{
    internal class CategoryHelper
    {
        public static string SelectCategory(TorrentContent torrent, string site)
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
                    "acgnx_asia" => SelectAcgnxAsia(torrent),
                    "acgnx_global" => SelectAcgnxGlobal(torrent),
                    _ => throw new NotImplementedException()
                };
            }
            return category;
        }
        // Now only Anime
        internal static string SelectAcgnxAsia(TorrentContent torrent)
        {
            ushort sortId;
            if (torrent.HasSubtitle)
            {
                if (torrent.IsFinished)
                {
                    sortId = (ushort)categoryAcgnxAsia.AnimeCollection;
                }
                else
                {
                    sortId = (ushort)categoryAcgnxAsia.Anime;
                }
            }
            else
            {
                sortId = (ushort)categoryAcgnxAsia.Raw;
            }
            return sortId.ToString();
        }
        internal static string SelectAcgnxGlobal(TorrentContent torrent)
        {
            ushort sortId;
            if (torrent.HasSubtitle)
            {
                sortId = (ushort)categoryAcgnxGlobal.Anime;
            }
            else
            {
                sortId = (ushort)categoryAcgnxGlobal.AnimeRaw;
            }
            return sortId.ToString();
        }
    }
}
