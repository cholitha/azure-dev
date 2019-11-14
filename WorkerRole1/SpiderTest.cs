using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Crawler.Core;
using Crawler.Core.Domain;
using Newtonsoft.Json;
using WorkerRole1.Extensions;
using Node = Crawler.Core.Domain.Node;

namespace WorkerRole1
{
    public class SpiderTest
    {
       public static string file_path = string.Empty;
        private readonly string _code;
        public SpiderTest(string code)
        {
            _code = code;
        }

        protected CrawlConfig GetBaseConfig()
        {
            var templateBasePath = file_path;

            var structureFilePath = Path.Combine(templateBasePath, $"{_code}.structure.json");
            var behaviourFilePath = Path.Combine(templateBasePath, $"{_code}.behaviour.json");

            Trace.TraceInformation(structureFilePath);

            var structure = JsonConvert.DeserializeObject<Node>(File.ReadAllText(structureFilePath));
            var baseConfig = JsonConvert.DeserializeObject<CrawlConfig>(File.ReadAllText(behaviourFilePath));

            baseConfig.Uri = structure.Uri;
            baseConfig.Structure = structure.BuildTree();

            return baseConfig;
        }

        protected CrawlConfig GetConfig()
        {
            var config = GetBaseConfig();

            var testDataPath = GetTestDataPath();
            config.Structure.Path = testDataPath;

            foreach (var node in config.Structure.Tree?.Flatten(n => n.Tree) ?? Enumerable.Empty<Node>())
                node.Path = testDataPath;

            return config;
        }

        protected CrawlConfig Given(CrawlConfig config)
        {
            return config;
        }

        private string GetCodePath()
        {
            var segments = _code.Split(new[] { "--" }, StringSplitOptions.None).ToList();
            segments = segments.Take(segments.Count - 1).ToList();
            segments.Add(_code);

            return Path.Combine(segments.ToArray());
        }

        private string GetTemplatePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", GetCodePath());
        }

        protected string GetTestDataPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tests", GetCodePath(), "Data");
        }
    }
}
