using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Abot.Core;
using Abot.Poco;
using Autofac;
using Autofac.Features.AttributeFilters;
using AutofacSerilogIntegration;
using AutoMapper;
using Crawler.Core.Domain;
using Crawler.Core.Domain.Crawling;
using Crawler.Core.Infrastructure;
using Crawler.Core.Infrastructure.DI;
using Crawler.Core.Repository;
using Crawler.Services.Configuration;
using Crawler.Services.Crawling;
using Crawler.Services.Security;
using Crawler.Services.Storage;
using Crawler.Services.TextProcessing;
using log4net;
using Serilog;
using Level = log4net.Core.Level;


namespace WorkerRole1.Infrastructure
{

    public class DependencyRegistrar : IDependencyRegistrar
    {
        private const string OutputTemplate = "[{Timestamp:hh:mm:ss.fff}] [{Level}] [{ThreadId}] {Message}{NewLine}{Exception}";
        private const string AppSettingsInjectionKey = "AppSettings";
        public void Register(ContainerBuilder builder, ITypeFinder typeFinder)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true; 
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.Trace(outputTemplate: OutputTemplate)
                .WriteTo.RollingFile(@"\\automatedcrawlersto.file.core.windows.net\logfiles\log\log-{Date}.txt", fileSizeLimitBytes: 100000000, retainedFileCountLimit: 5,
                    outputTemplate: OutputTemplate)
                .WriteTo.ColoredConsole()
                .CreateLogger();
            LogManager.GetRepository().Threshold = Level.Warn;
            Log4net.Appender.Serilog.Configuration.Configure();

            builder.RegisterLogger();

            builder.RegisterType<AppSettingService>().Keyed<ISettingService>(AppSettingsInjectionKey).SingleInstance();
            builder.RegisterType<DocumentService>().As<IDocumentService>().WithAttributeFiltering();
            builder.RegisterType<CrawlService>().As<ICrawlService>().WithAttributeFiltering();
            builder.RegisterType<FileService>().As<IFileService>();
            builder.RegisterType<HashService>().As<IHashService>();
            builder.RegisterType<HyperLinkParserService>().As<IHyperLinkParserService>();
            builder.RegisterType<HyperLinkParserService>().As<IHyperLinkParser>();
            builder.RegisterType<WebContentExtractor>().As<IWebContentExtractor>();
            builder.RegisterType<DocumentRequesterService>().As<IDocumentRequesterService>().WithAttributeFiltering();
            builder.RegisterType<DocumentRequesterService>().As<IPageRequester>().WithAttributeFiltering();
            builder.RegisterType<StartPointsDbRepository>().As<IStartPointsDbRepository>();

            builder.Register(
                c =>
                    new MapperConfiguration(cf =>
                    {
                        cf.AllowNullCollections = true;
                        cf.AllowNullDestinationValues = true;
                        cf.CreateMap<BaseDocument, Document>();
                        cf.CreateMap<Content, PageContent>()
                            .ForMember(pc => pc.Text,
                                ce =>
                                    ce.ResolveUsing(
                                        cc =>
                                        {
                                            using (var sr = new StreamReader(
                                                new MemoryStream(cc.Bytes ?? Encoding.UTF8.GetBytes(string.Empty)),
                                                cc.Encoding ?? Encoding.UTF8))
                                            {
                                                return sr.ReadToEnd();
                                            }
                                        }
                                    ));
                        cf.CreateMap<PageContent, Content>();
                        cf.CreateMap<CrawlConfig, CrawlConfiguration>()
                            .ForMember(ac => ac.CrawlTimeoutSeconds,
                                ce => ce.ResolveUsing(cc => (int)cc.Duration.TotalSeconds))
                            .ForMember(ac => ac.DownloadableContentTypes,
                                ce => ce.ResolveUsing(cc => cc.ContentTypes?.Aggregate((x, y) => $"{x}, {y}")))
                            .ForMember(ac => ac.IsSendingCookiesEnabled, ce => ce.UseValue(true))
                            .ForMember(ac => ac.MaxCrawlDepth, ce => ce.ResolveUsing(cc => cc.Depth))
                            .ForMember(ac => ac.MinCrawlDelayPerDomainMilliSeconds,
                                ce => ce.ResolveUsing(cc => (int)cc.RandomDelayRange.Item1.TotalMilliseconds))
                            .ForMember(ac => ac.IsHttpRequestAutoRedirectsEnabled, ce => ce.UseValue(true))
                            .ForMember(ac => ac.IsExternalPageCrawlingEnabled, ce => ce.UseValue(true))
                            .ForMember(ac => ac.IsUriRecrawlingEnabled, ce => ce.UseValue(false))
                            .ForMember(ac => ac.MaxPagesToCrawl, ce => ce.ResolveUsing(cc => cc.MaxPages))
                            .ForMember(ac => ac.IsHttpRequestAutomaticDecompressionEnabled, ce => ce.UseValue(true))
                            .ForMember(ac => ac.IsExternalPageLinksCrawlingEnabled, ce => ce.UseValue(true))
                            .ForMember(ac => ac.MaxConcurrentThreads, ce => ce.UseValue(1));
                        cf.CreateMap<Document, CrawledPage>()
                            .ForMember(cp => cp.Content, ce =>
                            {
                                ce.NullSubstitute(new Content());
                                ce.MapFrom(d => d.Content);
                            })
                            .ForMember(cp => cp.HttpWebRequest,
                                ce => ce.ResolveUsing(d => d.WebRequest as HttpWebRequest))
                            .ForMember(cp => cp.HttpWebResponse,
                                ce => ce.ResolveUsing(d => new HttpWebResponseWrapper(d.WebResponse as System.Net.HttpWebResponse)));
                        cf.CreateMap<PageToCrawl, BaseDocument>();
                        cf.CreateMap<HttpWebResponseWrapper, Crawler.Core.Domain.Crawling.HttpWebResponse>();
                        cf.CreateMap<BaseDocument, BaseDocumentModel>()
                            .ForMember(dm => dm.Title, ce => ce.ResolveUsing(bd => ((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("Title") ?? false ? bd.DocumentBag?.Title : null))
                            .ForMember(dm => dm.TitleHtml, ce => ce.ResolveUsing(bd => ((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("TitleHtml") ?? false ? bd.DocumentBag?.TitleHtml : null))
                            .ForMember(dm => dm.Type, ce => ce.ResolveUsing(bd => ((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("Type") ?? false ? bd.DocumentBag?.Type : null))
                            .ForMember(dm => dm.TypeHtml, ce => ce.ResolveUsing(bd => ((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("TypeHtml") ?? false ? bd.DocumentBag?.TypeHtml : null))
                            .ForMember(dm => dm.EffectiveDate, ce => ce.ResolveUsing(bd => ((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("EffectiveDate") ?? false ? bd.DocumentBag?.EffectiveDate : null))
                            .ForMember(dm => dm.PublishedDate, ce => ce.ResolveUsing(bd => ((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("PublishedDate") ?? false ? bd.DocumentBag?.PublishedDate : null))
                            .ForMember(dm => dm.Uri, ce => ce.ResolveUsing(bd => bd.Uri ?? (((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("Uri") ?? false ? bd.DocumentBag?.Uri : null)))
                            .ForMember(dm => dm.Html, ce => ce.ResolveUsing(bd => ((IDictionary<string, object>)bd.DocumentBag)?.ContainsKey("Html") ?? false ? bd.DocumentBag?.Html : null));
                    }).CreateMapper()
            );

            builder.RegisterType<HtmlToTextParserService>().As<ITextParserService>();
        }

        public int Order => 1;
    }
}