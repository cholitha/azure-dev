using Crawler.Core.Infrastructure;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WorkerRole1.Extensions;

namespace WorkerRole1
{
    public class RunService : SpiderTest
    {
        private readonly ILogger _logger = EngineContext.Current.Resolve<ILogger>();
        public RunService(string startPointCode) : base(startPointCode) { }
        public async Task RunAsync(string fnStatus,string fnClass)
        {
            var PreProcess = (dynamic) null;
            var PostProcess = (dynamic) null;
            if (int.Parse(fnStatus) == 1)
            {
                await Given(GetBaseConfig()).ThenAsync(d => { _logger.Information("complete"); });
            }
                else if (int.Parse(fnStatus) == 2)
            {
                PreProcess = MagicallyCreateInstance(fnClass);
                await (PreProcess.RunAsync(GetBaseConfig().Structure));
                await Given(GetBaseConfig()).ThenAsync(d => { _logger.Information("complete"); });
            }
            else if (int.Parse(fnStatus) == 3)
            {
                await Given(GetBaseConfig()).ThenAsync(d => { });
                await new PostProcess.CAPOST().RunAsync(GetBaseConfig().Structure);
                await Given(GetBaseConfig()).ThenAsync(d => { _logger.Information("complete"); });
            }
            else
            {
                return;
            }

        }
        private static object MagicallyCreateInstance(string className)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetTypes()
                .First(t => t.Name == className);
            return Activator.CreateInstance(type);
        }
    }
}
