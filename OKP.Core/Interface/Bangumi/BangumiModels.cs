// ReSharper disable InconsistentNaming, IdentifierTypo
namespace OKP.Core.Interface.Bangumi
{
#pragma warning disable CS8618
#pragma warning disable IDE1006
    internal class BangumiModels
    {
        public class UploadResponse
        {
            public bool success { get; set; }
            public string file_id { get; set; }
            public string[][] content { get; set; }
            public object[] torrents { get; set; }
        }

        public class AddRequest
        {
            public string category_tag_id { get; set; }
            public string title { get; set; }
            public string introduction { get; set; }
            public string?[] tag_ids { get; set; }
            public string team_id { get; set; }
            public bool teamsync { get; set; }
            public string file_id { get; set; }
        }

        public class AddResponse
        {
            public bool success { get; set; }
            public Torrent torrent { get; set; }
        }

        public class Torrent
        {
            public string category_tag_id { get; set; }
            public string title { get; set; }
            public string introduction { get; set; }
            public string[] tag_ids { get; set; }
            public int comments { get; set; }
            public int downloads { get; set; }
            public int finished { get; set; }
            public int leechers { get; set; }
            public int seeders { get; set; }
            public string uploader_id { get; set; }
            public string team_id { get; set; }
            public DateTime publish_time { get; set; }
            public string magnet { get; set; }
            public string infoHash { get; set; }
            public string file_id { get; set; }
            public bool teamsync { get; set; }
            public string[][] content { get; set; }
            public string[] titleIndex { get; set; }
            public string size { get; set; }
            public string btskey { get; set; }
            public string _id { get; set; }
        }

        public class TeamList
        {
            public TeamInfo[] Teams { get; set; }
        }

        public class TeamInfo
        {
            public string _id { get; set; }
            public string name { get; set; }
            public string tag_id { get; set; }
            public string signature { get; set; }
            public string icon { get; set; }
            public string admin_id { get; set; }
            public string[] admin_ids { get; set; }
            public string[] editor_ids { get; set; }
            public string[] member_ids { get; set; }
            public object[] auditing_ids { get; set; }
            public DateTime regDate { get; set; }
            public bool approved { get; set; }
        }

    }
}
