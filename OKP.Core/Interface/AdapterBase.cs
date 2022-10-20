using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OKP.Core.Interface.TorrentContent;

namespace OKP.Core.Interface
{
    internal abstract class AdapterBase
    {
        internal Template? template;
        internal TorrentContent? torrent;
        abstract public Task<HttpResult> PingAsync();
        abstract public Task<HttpResult> PostAsync();
        internal string CompileTemplate()
        {
            if (torrent is null || template is null)
            {
                throw new NotImplementedException();
            }
            return template.Content??"";
        }
    }
    public class HttpResult
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
        public HttpResult(int code, string message, bool isSuccess)
        {
            Code = code;
            Message = message;
            IsSuccess = isSuccess;
        }
    }

    internal interface IAdapter
    {
        

    }
}
