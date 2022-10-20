using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using OKP.Core.Interface;
using OKP.Core.Interface.Bangumi;
using OKP.Core.Interface.Dmhy;

namespace OKP
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args[0] is null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            var torrent = new TorrentContent(args[0]);
            Console.WriteLine(torrent.DisplayName);
            if (torrent.isV2())
            {
                Console.WriteLine("V2达咩！回去换！");
                Console.ReadLine();
                return;
            }
            torrent.DisplayFiles();
            List<AdapterBase> adapterList = new ();
            if(torrent.IntroTemplate is null)
            {
                Console.WriteLine("没有配置发布站你发个啥？");
                Console.ReadLine();
                return;
            }
            foreach (var site in torrent.IntroTemplate)
            {
                if(site.Site is null)
                {
                    Console.WriteLine("没有配置发布站你发个啥？");
                    Console.ReadLine();
                    return;
                }
                switch (site.Site.ToLower())
                {
                    case "dmhy":
                        adapterList.Add(new DmhyAdapter(torrent, site));
                        break;
                    case "bangumi":
                        adapterList.Add(new BangumiAdapter());
                        break;
                    default:
                        break;
                }
            }
            List<Task<HttpResult>> PingTask = new();
            adapterList.ForEach(p=>PingTask.Add(p.PingAsync()));
            var PingRes = Task.WhenAll(PingTask).Result;
            foreach (var res in PingRes)
            {
                if (!res.IsSuccess)
                {
                    Console.WriteLine(res.Code+"\t"+res.Message);
                    Console.ReadLine();
                    return;
                }
            }
        }
    }
}