using Crawler.Core.Domain;
using Crawler.Core.Domain.Crawling;
using Crawler.Core.Infrastructure;
using Crawler.Services.Crawling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WorkerRole1.Extensions
{
    public static class CrawlExtensions
    {
        public static CrawlConfig At(this CrawlConfig config, Node node)
        {
            config.Structure = node;
            RemoveRealChildNodes(node.Tree);
            return config;
        }

        public static CrawlConfig On(this CrawlConfig config, Uri uri)
        {
            config.Uri = uri;
            return config;
        }

        public static async Task ThenAsync(this CrawlConfig config, Action<BaseDocument> complete)
        {
            complete(await EngineContext.Current.Resolve<ICrawlService>()
                .CrawlAsync(config, new CancellationTokenSource()));
        }

        private static void RemoveRealChildNodes(ICollection<Node> tree)
        {
            if (tree == null) return;

            foreach (var n in tree.Where(n => n.Type == NodeType.Real).ToList())
                tree.Remove(n);

            foreach (var n in tree.ToList())
                RemoveRealChildNodes(n.Tree);
        }
    }
}
