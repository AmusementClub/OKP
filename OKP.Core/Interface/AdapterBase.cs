namespace OKP.Core.Interface
{
    internal abstract class AdapterBase
    {
        public abstract Task<HttpResult> PingAsync();
        public abstract Task<HttpResult> PostAsync();
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
}
