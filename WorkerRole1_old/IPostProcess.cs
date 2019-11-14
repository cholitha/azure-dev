using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    interface IPostProcess
    {
        Task RunAsync(Crawler.Core.Domain.Crawling.BaseDocument d);
    }
}
