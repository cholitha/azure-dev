using AngleSharp;
using Crawler.Core.Domain;
using Crawler.Core.Infrastructure;
using Crawler.Services.Security;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorkerRole1.PreProcess
{
    public class US__NFA__INS__ENFORCE_PRE_PROCESS
    {

        private readonly IHashService _hashService = EngineContext.Current.Resolve<IHashService>();
        string info = string.Empty;
        private string GetRootCache()
        {
            var client = new RestClient("https://www.nfa.futures.org/news/EnforceRegActionsSimple.aspx");
            var request = new RestRequest(Method.GET);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Cookie", "NSC_QSPEXFC_TTM_MCWTFSWFS=5ccba3d8afb531abd7c4778dcffb2695a4c80ebc5bad760565df5ba62e557b8a3ccafb19; ASPSESSIONIDQEQRACQS=CGOALKNACEFNFLIEMFFPHBBF");
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            request.AddHeader("Host", "www.nfa.futures.org");
            request.AddHeader("Postman-Token", "1d16a91b-d041-4695-9a70-22c3c4019e3a,746f9943-2a0c-4765-9382-8f407aafc400");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("User-Agent", "PostmanRuntime/7.17.1");
            IRestResponse response = client.Execute(request);
            var result = response.Content.ToString();
            return result;
        }

        private JArray GetResult(List<int> Years)
        {
            var client = new RestClient("https://www.nfa.futures.org/api/DataHandlerEnforcementReg.ashx");
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Cookie", "NSC_QSPEXFC_TTM_MCWTFSWFS=14b5a3d9e25bfab18b1889d9ebc3a6895e3027a2e71b25997739578d24366b1c82e1b815");
            request.AddHeader("Content-Length", "59");
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            request.AddHeader("Host", "www.nfa.futures.org");
            request.AddHeader("Postman-Token", "ab079af2-8b7c-4161-a3c2-1d622667befe,5b78ed56-6822-4b20-9553-399d8c47859f");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("User-Agent", "PostmanRuntime/7.17.1");
            request.AddHeader("Content-Type", "text/plain");
            request.AddParameter("undefined", "{id: 1, method: \"getEnforcementRegs\", params: [" + Years[0] + "," + Years[1] + "]}", ParameterType.RequestBody);
            //request.AddParameter("undefined", "{id: 1, method: \"getEnforcementRegs\", params: [2018,2019]}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            var content = response.Content;
            var finalresponse = JObject.Parse(content);
            var results = finalresponse["result"]["result"].ToString();
            JArray resultsArray = JArray.Parse(results);
            // Response has unsorted results,so had to do sorting before writing to cache.
            JArray sortedResults = new JArray(resultsArray.OrderByDescending(obj => (string)obj["CONTENT_DATE_SORT"]));
            return sortedResults;
        }

        public async Task createCache(Node basedocument)
        {
            var logPath = basedocument.Path.ToString();
            info = "[" + DateTime.Now.ToString("hh:mm:ss") + "] [Information] " + "Starting Pre Process.\n";
            File.AppendAllText(logPath + "\\log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", info);

            try
            {
                var rootUri = string.Empty;
                string path = string.Empty;
                path = basedocument.Path.ToString();
                rootUri = basedocument.Uri.AbsoluteUri;
                var rootCache = _hashService.GetHash(rootUri);
                string Cachepath = path + "\\" + rootCache;

                var cacheFile = GetRootCache().ToString();
                if (File.Exists(Cachepath))
                {
                    File.Delete(Cachepath);
                }

                info = "[" + DateTime.Now.ToString("hh:mm:ss") + "] [Information] " + "Getting root cache.\n";
                File.AppendAllText(path + "\\log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", info);

                File.WriteAllText(path + "\\" + rootCache, cacheFile);

                string Cachestring;
                using (StreamReader r = new StreamReader(Cachepath))
                {
                    Cachestring = r.ReadToEnd();
                }

                var config = Configuration.Default;
                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(req => req.Content(Cachestring));

                List<int> Years = new List<int>();
                int previousYear = DateTime.Now.Year - 1;

                Years.Add(previousYear);
                Years.Add(DateTime.Now.Year);
                var tbody = document.QuerySelector("#searchResultTableReg tbody");
                tbody.TextContent = "\n";

                info = "[" + DateTime.Now.ToString("hh:mm:ss") + "] [Information] " + "Getting results for " + previousYear + " and " + DateTime.Now.Year + "\n";
                File.AppendAllText(path + "\\log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", info);

                foreach (JObject item in GetResult(Years))
                {
                    var datePattern = @"(?i)^(\d{4}-\d{2}-\d{2}).*$"; //https://regex101.com/r/oD5ufu
                    var date = Regex.Match(item["CONTENT_DATE_SORT"].ToString(), datePattern).Groups[1];
                    var finalstring = "\t\t\t\t\t\t<tr role='row' class='row'><td class='sorting_1'>" + date + "</td><td><span class='glyphicon glyphicon-file rule-tag' aria-hidden='true'></span>" + item["HEADLINE_TEXT"].ToString().Trim() + "</td><td>" + item["ACTION_CATEGORY_CODE"].ToString() + "</td><td>" + item["RULE_SECTION_NAME"].ToString() + "</td><td>" + item["RULE_ID"].ToString() + "</td><td><span class='glyphicon glyphicon-file rule-tag' aria-hidden='true'></span> <span data-rule-id=\"" + item["RULE_ID"].ToString() + "\"   data-rule-section=" + item["RULE_SECTION_ID"].ToString() + " data-rule-sub-section=" + item["SORTORDER"].ToString() + " class='btn-add-rule'>Rule</span></td></tr>\n";
                    tbody.Insert(AngleSharp.Dom.AdjacentPosition.BeforeEnd, finalstring);
                }
                var FinalContent = document.DocumentElement.OuterHtml;

                info = "[" + DateTime.Now.ToString("hh:mm:ss") + "] [Information] " + "Writing results to cache file. \n";
                File.AppendAllText(path + "\\log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", info);

                //Writing results to cache
                File.WriteAllText(Cachepath, FinalContent);

                info = "[" + DateTime.Now.ToString("hh:mm:ss") + "] [Information] " + "Pre Process Completed. \n";
                File.AppendAllText(path + "\\log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", info);

                info = "[" + DateTime.Now.ToString("hh:mm:ss") + "] [Information] " + "Starting Crawler Service. \n";
                File.AppendAllText(path + "\\log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", info);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }
    }
}
