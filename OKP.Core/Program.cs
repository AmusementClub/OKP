using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using OKP.Core.Interface;
using OKP.Core.Interface.Bangumi;
using OKP.Core.Interface.Dmhy;
using OKP.Core.Interface.Nyaa;
using System.Text.RegularExpressions;

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
            if (!File.Exists(args[0]))
            {
                Console.WriteLine("文件不存在！{0}", args[0]);
                Console.ReadLine();
                return;
            }
            var torrent = TorrentContent.Build(args[0]);
            if (torrent.IsV2())
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
            Console.WriteLine("即将发布");
            foreach (var site in torrent.IntroTemplate)
            {
                if(site.Site is null)
                {
                    Console.WriteLine("没有配置发布站你发个啥？");
                    Console.ReadLine();
                    return;
                }
                Console.WriteLine(site.Site);
                AdapterBase adapter = site.Site.ToLower() switch
                {
                    "dmhy" => new DmhyAdapter(torrent, site),
                    "bangumi" => new BangumiAdapter(),
                    "nyaa"=>new NyaaAdapter(torrent, site),
                    _ => throw new NotImplementedException()
                };
                adapterList.Add(adapter);
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
            Console.WriteLine("登录成功，继续发布？");
            Console.ReadKey();
            foreach (var item in adapterList)
            {
                var result = item.PostAsync().Result;
                if (result.IsSuccess)
                {
                    Console.WriteLine("发布成功");
                }
                else
                {
                    Console.WriteLine("发布失败");
                }
            }
            Console.WriteLine("登录完成");
            Console.ReadKey();
        }
    }
}