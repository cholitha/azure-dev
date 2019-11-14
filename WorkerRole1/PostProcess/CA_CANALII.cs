using AngleSharp;
using Crawler.Core.Domain.Crawling;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WorkerRole1.PostProcess
{
    public class CA_CANALII : IPostProcess
    {

        public async Task RunAsync(BaseDocument d)
        {
            await GetCache(d);
        }
        private async Task GetCache(BaseDocument baseDocument)
        {
            try
            {
                string path = string.Empty;
                path = baseDocument.Path.ToString();
                string fulltreepath = path + "\\tree.json";

                JObject parsed;
                using (StreamReader r = new StreamReader(fulltreepath))
                {
                    string json = r.ReadToEnd();
                    parsed = JObject.Parse(json);
                }

                var x = (JArray)parsed["children"];

                foreach (JObject Cache in x)
                {
                    var urlpath = Cache.GetValue("uri").ToString();
                    var value = Cache.GetValue("cachedUri").ToString();
                    string Cachepath = path + "\\" + value;
                    string Cachestring;
                    using (StreamReader r = new StreamReader(Cachepath))
                    {
                        Cachestring = r.ReadToEnd();
                    }

                    var config = Configuration.Default;
                    var context = BrowsingContext.New(config);
                    var document = await context.OpenAsync(req => req.Content(Cachestring));
                    var client = new RestClient(urlpath + "items");
                    //var blueListItemsLinq = document.All.Where(m => m.LocalName == "div" && m.Id == "decisionsListing");
                    var blueListItemsCssSelector = document.QuerySelector("#decisionsListing");
                    var count = Int32.Parse(blueListItemsCssSelector.ChildElementCount.ToString());

                    if (count > 0)
                    {
                        foreach (var chil in blueListItemsCssSelector.Children)
                        {
                            blueListItemsCssSelector.RemoveChild(chil);
                        }
                    }

                    var request = new RestRequest(Method.GET);
                    request.AddHeader("cache-control", "no-cache");
                    request.AddHeader("Connection", "keep-alive");
                    request.AddHeader("Accept-Encoding", "gzip, deflate");
                    request.AddHeader("Host", "www.canlii.org");
                    request.AddHeader("Postman-Token", "d2cdd749-89e4-4fd2-9edb-cd7b972a9bec,27b9adad-9423-4103-8dbc-deccc0e4239e");
                    request.AddHeader("Cache-Control", "no-cache");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("User-Agent", "PostmanRuntime/7.15.2");
                    IRestResponse response = client.Execute(request);
                    var content = response.Content;
                    // var finalresponse = JObject.Parse(content);
                    var json = JArray.Parse(content);

                    AngleSharp.Dom.IElement results = document.CreateElement("div");

                    foreach (JObject item in json)
                    {
                        string urlname = item.GetValue("url").ToString();
                        string style = item.GetValue("styleOfCause").ToString();
                        string citation = item.GetValue("citation").ToString();

                        var finalstring = "<div class='row row-stripped py-1'><div class='col-3 col-md-2 text-nowrap decisionDate'>2018-12-31</div><div class='col'><a class='canlii' href=" + urlname + ">" + style + "</a>," + citation + "</div></div>";
                        var linkcache = await context.OpenAsync(req => req.Content(finalstring));
                        document.QuerySelector("#decisionsListing").Append(linkcache.QuerySelector(".row"));
                    }

                    var FinalContent = document.DocumentElement.OuterHtml;
                    File.WriteAllText(Cachepath, FinalContent);
                }

            }
            catch (Exception)
            {
                return;
            }

        }
    }
}
