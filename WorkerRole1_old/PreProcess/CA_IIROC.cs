using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Crawler.Core.Infrastructure;
using Crawler.Services.Security;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Node = Crawler.Core.Domain.Node;

namespace WorkerRole1.PreProcess
{
    public class CA_IIROC : IPreProcess
        {
        private readonly IHashService _hashService = EngineContext.Current.Resolve<IHashService>();
        public async Task RunAsync(Node d)
        {
            await GetCache(d);
        }

        private async Task GetCache(Node baseDocument)
        {
            string Urllink = string.Empty;
            List<IElement> results = new List<IElement>();
            Urllink = baseDocument.Uri.ToString();
            var hash = _hashService.GetHash(Urllink);
            var path = baseDocument.Path + "\\" + hash;
            var client = new RestClient(Urllink);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Cookie", "__cfduid=dcc07df9f840845c96d23a1b4df1c34dc1568810562");
            request.AddHeader("Content-Length", "3744");
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            request.AddHeader("Host", "www.iiroc.ca");
            request.AddHeader("Postman-Token", "f9601dc2-aa1e-4f10-83dc-bfc709c90f1c,6a5737ce-b7f6-474e-ac0e-81f0cf1d06de");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("User-Agent", "PostmanRuntime/7.17.1");
            request.AddHeader("Content-Type", "text/plain");

            IRestResponse response = client.Execute(request);
            var content = response.Content;
            File.WriteAllText(path, content);

            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var mainDoc = await context.OpenAsync(req => req.Content(content));

            var NextBtn = mainDoc.Body.QuerySelectorAll("a:has(img[alt='Next'])").OfType<IHtmlAnchorElement>();
            var EVENTTARGET = string.Empty;
            var EVENTARGUMENT = string.Empty;
            string ptnFirst = @"(!?)ct.*.+?(?=','dvt_firstro)";
            string ptnStartPos = @"(!?)dvt.*}";

            foreach (var menuLink in NextBtn)
            {
                string input = menuLink.Href.ToString();
                Match m1 = Regex.Match(input, ptnFirst);
                Match m2 = Regex.Match(input, ptnStartPos);
                EVENTTARGET = m1.Value;
                EVENTARGUMENT = m2.Value;
            }

            var VIEWSTATE = mainDoc.QuerySelector("[name='__VIEWSTATE']").Attributes["value"].Value;
            var REQUESTDIGEST = mainDoc.QuerySelector("[name='__REQUESTDIGEST']").Attributes["value"].Value;
            var VIEWSTATEGENERATOR = mainDoc.QuerySelector("[name='__VIEWSTATEGENERATOR']").Attributes["value"].Value;
            var EVENTVALIDATION = mainDoc.QuerySelector("[name='__EVENTVALIDATION']").Attributes["value"].Value;

            request.AddHeader("Content-Type", "application/x-www-form-urlencodedn");
            request.AddParameter("__EVENTTARGET", EVENTTARGET);
            request.AddParameter("__EVENTARGUMENT", EVENTARGUMENT);
            request.AddParameter("__REQUESTDIGEST", REQUESTDIGEST);
            request.AddParameter("__VIEWSTATE", VIEWSTATE);
            request.AddParameter("__VIEWSTATEGENERATOR", VIEWSTATEGENERATOR);
            request.AddParameter("__EVENTVALIDATION", EVENTVALIDATION);


            IRestResponse res = client.Execute(request);
            await GetContentsAsync(res, context, new RestClient(Urllink), results);//COLLECT ALL TRS

            var tbody = mainDoc.QuerySelector("div[webpartid] > table > tbody");
            mainDoc.QuerySelector("div[webpartid] > table:nth-child(2) > tbody").TextContent = "";
            foreach (var allTr in results)
            {
                tbody.AppendChild(allTr);
            }
            File.WriteAllText(path, mainDoc.DocumentElement.OuterHtml);
            Console.WriteLine("Pre-process Completed");
        }
        private async Task GetContentsAsync(IRestResponse res, IBrowsingContext context, RestClient client, List<IElement> results)
        {
            var content = res.Content;
            var document = await context.OpenAsync(req => req.Content(content));
            var trCollections = document.Body.QuerySelectorAll("div[webpartid] > table > tbody > tr:not(:has(img[alt='Next'],th))");
            var releasedDate = document.Body.QuerySelectorAll("div[webpartid] > table > tbody > tr:not(:has(img[alt='Next'],th)) > td:first-of-type");

            foreach (var date in releasedDate)
            {
                if (DateTime.Parse(date.TextContent) < DateTime.Today.AddYears(-1))//12 MONTHS PREVIOUS TO THE DATE
                {
                    return;
                }
            }

            var NextBtn = document.Body.QuerySelectorAll("a:has(img[alt='Next'])").OfType<IHtmlAnchorElement>();
            if (document.QuerySelector("a:has(img[alt='Next'])") == null)//NEXT BUTTON FINISHED
            {
                return;
            }
            var EVENTTARGET = string.Empty;
            var EVENTARGUMENT = string.Empty;
            string ptnFirst = @"(!?)ct.*.+?(?=','dvt_firstro)";
            string ptnStartPos = @"(!?)dvt.*}";

            foreach (var menuLink in NextBtn)
            {
                string input = menuLink.Href.ToString();
                Match m1 = Regex.Match(input, ptnFirst);
                Match m2 = Regex.Match(input, ptnStartPos);
                EVENTTARGET = m1.Value;
                EVENTARGUMENT = m2.Value;
            }

            var request = new RestRequest(Method.POST);

            var VIEWSTATE = document.QuerySelector("[name='__VIEWSTATE']").Attributes["value"].Value;
            var REQUESTDIGEST = document.QuerySelector("[name='__REQUESTDIGEST']").Attributes["value"].Value;
            var VIEWSTATEGENERATOR = document.QuerySelector("[name='__VIEWSTATEGENERATOR']").Attributes["value"].Value;
            var EVENTVALIDATION = document.QuerySelector("[name='__EVENTVALIDATION']").Attributes["value"].Value;

            request.AddHeader("Content-Type", "application/x-www-form-urlencodedn");
            request.AddParameter("__EVENTTARGET", EVENTTARGET);
            request.AddParameter("__EVENTARGUMENT", EVENTARGUMENT);
            request.AddParameter("__REQUESTDIGEST", REQUESTDIGEST);
            request.AddParameter("__VIEWSTATE", VIEWSTATE);
            request.AddParameter("__VIEWSTATEGENERATOR", VIEWSTATEGENERATOR);
            request.AddParameter("__EVENTVALIDATION", EVENTVALIDATION);
            IRestResponse Newres = client.Execute(request);

            //Console.WriteLine(Newres.Content.ToString());

            var Newcontent = Newres.Content;
            var documentNew = await context.OpenAsync(req => req.Content(Newcontent));

            var trCollection = documentNew.Body.QuerySelectorAll("div[webpartid] > table > tbody > tr:not(:has(img[alt='Next'],th))");

            foreach (var tr in trCollection)
            {
                results.Add(tr);
            }
            //VIEW RESULTS
            //foreach (var a in results)
            //{
            //    Console.WriteLine(a.TextContent);
            //}
            await GetContentsAsync(Newres, context, client, results);
        }
    }
}
