using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Interface
{
    internal abstract class AdapterBase
    {
        abstract public Task<int> PingAsync();
        abstract public Task<int> PostAsync();

    }

    internal interface IAdapter
    {
        

    }
}
