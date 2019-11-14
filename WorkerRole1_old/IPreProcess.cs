using Crawler.Core.Domain;
using System.Threading.Tasks;

namespace WorkerRole1
{
    interface IPreProcess
    {
        Task RunAsync(Node d);
    }
}
