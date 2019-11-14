using System;
using System.Threading.Tasks;
using Crawler.Core.Domain;
using Crawler.Services.Crawling;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using RestSharp;
using AngleSharp;
using System.Web;
using System.Net;
using System.Diagnostics;
using System.Linq;

namespace WorkerRole1.PostProcess
{
    public class CAPOST : IPreProcess
    {
        public Node BasceDoc;
        public BaseDocumentModel structure;

        public async Task RunAsync(Node d)
        {
            BasceDoc = d;

            //get the cache path 
            var cachePath = d.Path;
            //get tree.json file to
            var treeJsonFile = Path.Combine(cachePath, "tree.json");
            //tree.json in to an json object
            structure = JsonConvert.DeserializeObject<BaseDocumentModel>(File.ReadAllText(treeJsonFile));

            getResults();

        }


        public void getResults()
        {
            string ToDate = DateTime.Now.ToString("MM/dd/yyyy");
            string FromDate = DateTime.Now.AddMonths(-12).ToString("MM/dd/yyyy");

            int pages = 1;
            bool complete = true;
            List<AngleSharp.Dom.IElement> results = new List<AngleSharp.Dom.IElement>();
            var cachedHtml = getCachedHtml().Result;




            Dictionary<string, string> FormData = new Dictionary<string, string>() {
                {"VIEWSTATE",  "" },
                {"EVENTVALIDATION",  ""},
                {"VIEWSTATEGENERATOR",  ""},
                {"EktronClientManager",  ""},
            };

            var list = cachedHtml.CreateElement("article");

            while (complete)
            {
                string RequestParams = getPaginationParams(FromDate, ToDate, FormData, pages + "");
                var doc = MakeCURLReqest(RequestParams).Result;



                var rows = doc.QuerySelectorAll("#ctl00_bodyContent_gv_list tbody tr:not(tr:first-of-type)");

                if (pages == 3)
                {
                    Debug.WriteLine('a');
                }

                FormData["VIEWSTATE"] = doc.QuerySelector("input[name='__VIEWSTATE']").Attributes["value"].Value;
                FormData["EVENTVALIDATION"] = doc.QuerySelector("input[name='__EVENTVALIDATION']").Attributes["value"].Value;
                FormData["VIEWSTATEGENERATOR"] = doc.QuerySelector("input[name='__VIEWSTATEGENERATOR']").Attributes["value"].Value;
                FormData["EktronClientManager"] = doc.QuerySelector("input[name='EktronClientManager']").Attributes["value"].Value;

                foreach (var row in rows)
                {
                    var has = results.Any(result => result.QuerySelector("td:first-of-type span").InnerHtml == row.QuerySelector("td:first-of-type span").InnerHtml);
                    if (!has)
                    {
                        AngleSharp.Dom.IElement leaf = getLeafNode(row, FormData).Result;
                        var div = cachedHtml.CreateElement("div");

                        if (row.QuerySelector("td:first-of-type span").InnerHtml == leaf.QuerySelector("#ctl00_bodyContent_lbl_name").InnerHtml)
                        {
                            div.SetAttribute("data-internal-node", "");
                            leaf.Append(row);
                            div.AppendChild(leaf);

                            //cachedHtml.QuerySelector("#ctl00_bodyContent_pnl_list").Append(div);
                        }

                        File.AppendAllText(Path.Combine(BasceDoc.Path, "test.txt"), div.InnerHtml);

                        list.AppendChild(div);
                        results.Add(row);
                    }
                    else
                    {
                        complete = false;
                        break;
                    }
                }

                Debug.WriteLine(pages);
                pages++;

            }

            var page = MakeCURLReqest("").Result;
            string mainTitle = "<h1>" + page.QuerySelector("#e9edebdf9_23_82 > a").InnerHtml + "</h1>";

            File.WriteAllText(Path.Combine(BasceDoc.Path, structure.CachedUri.ToString()), mainTitle + list.OuterHtml.ToString());
        }

        private async Task<AngleSharp.Dom.IDocument> getCachedHtml()
        {
            string cachedHtmlPath = Path.Combine(BasceDoc.Path, structure.CachedUri.ToString());

            var config = Configuration.Default;

            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync(req => req.Content(File.ReadAllText(cachedHtmlPath)));

            var oldResults = document.QuerySelectorAll("#ctl00_bodyContent_gv_list tbody tr:not(tr:first-of-type)");

            foreach (var reslutRaw in oldResults)
            {
                reslutRaw.Remove();
            }

            //document.Body.QuerySelector("#ctl00_bodyContent_gv_list tbody").Remove();

            return document;
        }


        private async Task<AngleSharp.Dom.IDocument> MakeCURLReqest(string RequestParams)
        {
            var client = new RestClient("https://www.securities-administrators.ca/disciplinedpersons.aspx?id=74");
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Referer", "https://www.securities-administrators.ca/disciplinedpersons.aspx?id=74");
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("application/x-www-form-urlencoded", RequestParams, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            HttpStatusCode statusCode = response.StatusCode;
            int numericStatusCode = (int)statusCode;


            if (numericStatusCode != 200)
            {
                Debug.WriteLine(RequestParams);
                throw new Exception("Response not successfull");
            }


            var config = Configuration.Default;

            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync(req => req.Content(response.Content));

            return document;
        }

        private async Task<AngleSharp.Dom.IElement> getLeafNode(AngleSharp.Dom.IElement row, Dictionary<string, string> FromData)
        {
            string ToDate = DateTime.Now.ToString("MM/dd/yyyy");
            string FromDate = DateTime.Now.AddMonths(-12).ToString("MM/dd/yyyy");

            var LeafId = Regex.Match(row.QuerySelector("td:first-of-type a").Attributes["href"].Value, @"(ctl00\$bodyContent\$gv_list\$ctl\d{1,2}\$lbtn_personname)");
            string RequestParams = getLeafPageParam(LeafId.Groups[1].Value, FromData);
            var LeafPage = MakeCURLReqest(RequestParams).Result;
            var LeafPageDetails = LeafPage.QuerySelector("#ctl00_bodyContent_pnl_detail");


            return LeafPageDetails;
        }


        private string getLeafPageParam(string page, Dictionary<string, string> FormData)
        {
            string localViewState = "%2FwEPDwUKMTQ0NDE2NDAyNg9kFgJmD2QWAgIDEGRkFgwCAw8PFgIeB1Zpc2libGVoZGQCBQ9kFgICAQ8PFgIeD0NvbW1hbmRBcmd1bWVudAVIaHR0cDovL3d3dy5hdXRvcml0ZXMtdmFsZXVycy1tb2JpbGllcmVzLmNhL2Rpc2NpcGxpbmVkcGVyc29ucy5hc3B4P0lEPTc0FgQeC29uTW91c2VPdmVyBUhNTV9zd2FwSW1hZ2UoJ2N0bDAwX2ltZ2J0bl9lbmdsaXNoJywnJywnL2ltYWdlcy91dGlsL2ZyZW5jaC1vdmVyLnBuZycsMSkeCm9uTW91c2VPdXQFE01NX3N3YXBJbWdSZXN0b3JlKClkAgcPDxYCHwBoZBYCAgEPDxYCHwEFSGh0dHA6Ly93d3cuYXV0b3JpdGVzLXZhbGV1cnMtbW9iaWxpZXJlcy5jYS9kaXNjaXBsaW5lZHBlcnNvbnMuYXNweD9JRD03NBYEHwIFSE1NX3N3YXBJbWFnZSgnY3RsMDBfaW1nYnRuX2ZyZW5jaCcsJycsJy9pbWFnZXMvdXRpbC9lbmdsaXNoLW92ZXIucG5nJywxKR8DBRNNTV9zd2FwSW1nUmVzdG9yZSgpZAIJD2QWAmYPZBYGAgEPFgIeBFRleHQFuQI8c2NyaXB0IGxhbmd1YWdlPSJqYXZhc2NyaXB0Ij4NCmZ1bmN0aW9uIG5vZW50ZXIoKSB7DQogICAgaWYgKHdpbmRvdy5ldmVudCAmJiB3aW5kb3cuZXZlbnQua2V5Q29kZSA9PSAxMykgew0KICAgICAgICB3aW5kb3cubG9jYXRpb24uaHJlZiA9ICdzZWFyY2guYXNweD9zZWFyY2h0ZXh0PScgKyBkb2N1bWVudC5nZXRFbGVtZW50QnlJZCgnY3RsMDBfc2VhcmNoQm94X3NlYXJjaFR4dCcpLnZhbHVlOyANCiAgICAgICAgcmV0dXJuIGZhbHNlOyANCiAgICB9IGVsc2Ugew0KICAgICAgICByZXR1cm4gdHJ1ZTsgDQogICAgfSANCn0NCjwvc2NyaXB0Pg0KZAIDDw9kFgIeCm9ua2V5cHJlc3MFHGphdmFzY3JpcHQ6cmV0dXJuIG5vZW50ZXIoKTtkAgUPD2QWBB4Lb25tb3VzZW92ZXIFLmphdmFzY3JpcHQ6dGhpcy5zcmM9Jy9pbWFnZXMvdXRpbC9nby1vdmVyLmdpZiceCm9ubW91c2VvdXQFKWphdmFzY3JpcHQ6dGhpcy5zcmM9Jy9pbWFnZXMvdXRpbC9nby5naWYnZAILD2QWAgIDDw8WAh8AaGRkAg0PZBYMZg8PFgIfBAULRW5mb3JjZW1lbnRkZAIGDw8WAh8EBQExZGQCBw8PFgIfBAUCMTlkZAIIDw8WAh8EZWRkAgsPZBYIAgEPZBYEAgEPDxYCHwQFTjxpbWcgYm9yZGVyPScwJyBhbGlnbj0nYWJzbWlkZGxlJyBzcmM9Jy9pbWFnZXMvdXRpbC9yc3MtaWNvbi5qcGcnIC8%2BIFJTUyBGZWVkc2RkAgMPDxYCHwQFGEJyb3dzZSBBbHBoYWJldGljYWwgTGlzdGRkAgMPZBYUAgEPDxYCHwQFF0Rpc2NpcGxpbmVkIExpc3QgU2VhcmNoZGQCAw8PFgIfBAUETmFtZWRkAgcPDxYCHwQFC1J1bGluZyBCb2R5ZGQCCQ9kFgJmDw8WBB4QTk9ORUxpc3RJdGVtVGV4dAUHQW55L0FsbB4LRGlzcGxheU1vZGULKU5VSUwuRGlzcGxheU1vZGUsIENTQVdlYiwgVmVyc2lvbj0xLjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPW51bGwBZBYEAgEPDxYCHwBoZGQCAw8QDxYCHwBnZBAVFQdBbnkvQWxsI0FsYmVydGEgU2VjdXJpdGllcyBDb21taXNzaW9uIChBU0MpJ0F1dG9yaXTDqSBkZXMgbWFyY2jDqXMgZmluYW5jaWVycyAoQU1GKS1Ccml0aXNoIENvbHVtYmlhIFNlY3VyaXRpZXMgQ29tbWlzc2lvbiAoQkNTQyk5Q2FuYWRpYW4gU2VjdXJpdGllcyBBZG1pbmlzdHJhdG9ycyAoSW52ZXN0b3IgQWxlcnQpIChDU0EpKkNoYW1icmUgZGUgbGEgc8OpY3VyaXTDqSBmaW5hbmNpw6hyZSAoQ1NGKRxDb3VydCAoQWxiZXJ0YSkgKENvdXJ0IChBQikpHUNvdXJ0IChNYW5pdG9iYSkgKENvdXJ0IChNQikpHENvdXJ0IChPbnRhcmlvKSAoQ291cnQgKE9OKSkcQ291cnQgKFF1w6liZWMpIChDb3VydCAoUUMpKT9GaW5hbmNpYWwgYW5kIENvbnN1bWVyIEFmZmFpcnMgQXV0aG9yaXR5IG9mIFNhc2thdGNoZXdhbiAoRkNBQSkxRmluYW5jaWFsIGFuZCBDb25zdW1lciBTZXJ2aWNlcyBDb21taXNzaW9uIChGQ05CKVVHb3Zlcm5tZW50IG9mIE5ld2ZvdW5kbGFuZCBhbmQgTGFicmFkb3IgLSBGaW5hbmNpYWwgU2VydmljZXMgUmVndWxhdGlvbiBEaXZpc2lvbiAoTkwpPUludmVzdG1lbnQgSW5kdXN0cnkgUmVndWxhdG9yeSBPcmdhbml6YXRpb24gb2YgQ2FuYWRhIChJSVJPQykkTWFuaXRvYmEgU2VjdXJpdGllcyBDb21taXNzaW9uIChNU0MpME11dHVhbCBGdW5kIERlYWxlcnMgQXNzb2NpYXRpb24gb2YgQ2FuYWRhIChNRkRBKShOb3ZhIFNjb3RpYSBTZWN1cml0aWVzIENvbW1pc3Npb24gKE5TU0MpO09mZmljZSBvZiB0aGUgU3VwZXJpbnRlbmRlbnQgb2YgU2VjdXJpdGllcyBmb3IgTnVuYXZ1dCAoTlUpI09udGFyaW8gU2VjdXJpdGllcyBDb21taXNzaW9uIChPU0MpNFRyaWJ1bmFsIGFkbWluaXN0cmF0aWYgZGVzIG1hcmNow6lzIGZpbmFuY2llcnMgKFRNRikfWXVrb24gU2VjdXJpdGllcyBPZmZpY2UgKFl1a29uKRUVAS0CMTgCMTIBNwIyOQIyMwIzMAIyNAIyNQIxNAIxMwIxMQIyNgIyMgIxMAIyMQIxNgIyNwIxOQIxNQIyOBQrAxVnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2cWAWZkAgsPZBYEZg9kFgICAQ8PFgIfBAUZVmlvbGF0aW9ucyBhbmQvb3IgY29uZHVjdGRkAgEPZBYCAgEPZBYCZg8PFgQfCAUHQW55L0FsbB8JCysEAWQWBAIBDw8WAh8AaGRkAgMPEA8WAh8AZ2QQFR0HQW55L0FsbCZBY3RpbmcgY29udHJhcnkgdG8gdGhlIHB1YmxpYyBpbnRlcmVzdDBBdXRob3JpemVkLCBwZXJtaXR0ZWQgb3IgYWNxdWllc2NlZCBpbiB2aW9sYXRpb24PQnJlYWNoIG9mIG9yZGVyEkNhcGl0YWwgZGVmaWNpZW5jeTpDb25mbGljdCBvZiBpbnRlcmVzdCBhbmQvb3IgYWN0aW5nIGFnYWluc3QgY2xpZW50IGludGVyZXN0FURpc2Nsb3N1cmUgdmlvbGF0aW9ucytGYWlsdXJlIHRvIGNvbXBseSB3aXRoIHRlcm1zIGFuZCBjb25kaXRpb25zFEZhaWx1cmUgdG8gY29vcGVyYXRlQUZhaWx1cmUgdG8gZGlzY2hhcmdlIGtub3cgeW91ciBjbGllbnQgYW5kIHN1aXRhYmlsaXR5IG9ibGlnYXRpb25zIEZhaWx1cmUgdG8gZmlsZSBpbnNpZGVyJ3MgcmVwb3J0KEZhaWx1cmUgdG8gZnVsZmlsbCBzcGVjaWZpYyB1bmRlcnRha2luZ3M2RmFpbHVyZSB0byBtZWV0IGZpbmFuY2lhbCBhbmQgb3BlcmF0aW9uYWwgcmVxdWlyZW1lbnRzKUZyYXVkIGFuZC9vciBGb3JnZXJ5IGFuZC9vciBGYWxzaWZpY2F0aW9uC0dhdGVrZWVwaW5nF0lsbGVnYWwgaW5zaWRlciB0cmFkaW5nJUlsbGVnYWwgb3IgdW5yZWdpc3RlcmVkIGRpc3RyaWJ1dGlvbnMqSW1wcm9wZXIgcmVsaWFuY2Ugb24gcHJvc3BlY3R1cyBleGVtcHRpb25zHEluYWRlcXVhdGUgY29tcGxpYW5jZSBzeXN0ZW0bSW50ZXJuYWwgY29udHJvbCB2aW9sYXRpb25zJU1ha2luZyBmYWxzZSBvciBtaXNsZWFkaW5nIHN0YXRlbWVudHMTTWFya2V0IG1hbmlwdWxhdGlvbhJNaXNyZXByZXNlbnRhdGlvbnMFT3RoZXIZUHJvaGliaXRlZCByZXByZXNlbnRhdGlvbjRVbnJlZ2lzdGVyZWQgYWN0aXZpdGllcyBhbmQvb3IgdW5hcHByb3ZlZCBhY3Rpdml0aWVzMlVucmVnaXN0ZXJlZCwgdW5hdXRob3JpemVkIGFuZC9vciBpbXByb3BlciB0cmFkaW5nHVVucmVwb3J0ZWQgY2FwaXRhbCBkZWZpY2llbmN5LVVuc3VpdGFibGUgaW52ZXN0bWVudHMgYW5kL29yIHJlY29tbWVuZGF0aW9ucxUdAS0BMQIxOAEyAjI1AjE2AjEwAjI3AjEyAjI0ATMCMjgCMjYBNAIxNQE5ATUCMjMCMjECMTMCMTkCMTEBNgIxNwIyMAE3ATgCMjICMTQUKwMdZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2cWAWZkAg0PZBYEZg9kFgICAQ8PFgIfBAUIU2FuY3Rpb25kZAIBD2QWAgIBD2QWAmYPDxYEHwgFB0FueS9BbGwfCQsrBAFkFgQCAQ8PFgIfAGhkZAIDDxAPFgIfAGdkEBURB0FueS9BbGwrQWNxdWlzaXRpb25zIGJhbiB3aXRoIG9yIHdpdGhvdXQgZXhjZXB0aW9ucxtBZG1pbmlzdHJhdGl2ZSBQZW5hbHR5L0ZpbmUWQ29tcGxldGlvbiBvZiB0cmFpbmluZ0JEaXJlY3RvciwgT2ZmaWNlciBhbmQvb3IgU3VwZXJ2aXNvciBCYW4gd2l0aCBvciB3aXRob3V0IGV4Y2VwdGlvbnMZRGlzZ29yZ2VtZW50IC9SZXN0aXR1dGlvbjFFeGVtcHRpb24gaW5hcHBsaWNhYmxlIHdpdGggb3Igd2l0aG91dCBleGNlcHRpb25zDEltcHJpc29ubWVudDFJbnZlc3RvciByZWxhdGlvbnMgYmFuIHdpdGggb3Igd2l0aG91dCBleGNlcHRpb25zBU90aGVyQVJlZ2lzdHJhdGlvbiBiYW4gKHdpdGggb3Igd2l0aG91dCBleGNlcHRpb25zKSBhbmQvb3IgcmVzdHJpY3Rpb25zFFJlZ2lzdHJhdGlvbiByZWZ1c2FsCVJlcHJpbWFuZBVTcGVjaWZpYyB1bmRlcnRha2luZ3MYU3VzcGVuc2lvbiBvZiBNZW1iZXJzaGlwGVRlcm1pbmF0aW9uIG9mIE1lbWJlcnNoaXAmVHJhZGluZyBiYW4gd2l0aCBvciB3aXRob3V0IGNvbmRpdGlvbnMVEQEtAjEyATUBMwExAjEwAjEzATgBNAIxMQE3AjE0ATkBNgIxNQIxNgEyFCsDEWdnZ2dnZ2dnZ2dnZ2dnZ2dnFgFmZAIPDw8WAh8EBQRGcm9tZGQCEw8PFgIfBAUCVG9kZAIXDw8WAh4ISW1hZ2VVcmwFI34vaW1hZ2VzL2Rpc2NpcGxpbmVkbGlzdC9zZWFyY2guZ2lmZGQCGQ8PFgIfCgUvfi9pbWFnZXMvZGlzY2lwbGluZWRsaXN0L2V4cG9ydF9lbnRpcmVfbGlzdC5naWZkZAIFDw8WAh8EZWRkAgcPZBYOAgEPDxYEHwQFGkxhdGVzdCAxMCBEaXNjaXBsaW5lZCBMaXN0HwBoZGQCAw8PFgIfAGdkFgQCAw8PFgIfBAVgPElNRyBTUkM9Jy9pbWFnZXMvZGlzY2lwbGluZWRsaXN0L2Fycm93LWxlZnQucG5nJyBzdHlsZT0nYm9yZGVyOjA7IHBhZGRpbmctcmlnaHQ6NHB4Jy8%2BUHJldiBwYWdlZGQCBw8PFgIfBAVgTmV4dCBwYWdlPElNRyBTUkM9Jy9pbWFnZXMvZGlzY2lwbGluZWRsaXN0L2Fycm93LXJpZ2h0LnBuZycgc3R5bGU9J2JvcmRlcjowOyBwYWRkaW5nLWxlZnQ6NHB4Jy8%2BZGQCBQ88KwARAwAPFgQeC18hRGF0YUJvdW5kZx4LXyFJdGVtQ291bnQCFGQBEBYAFgAWAAwUKwAAFgJmD2QWKgIBD2QWBmYPZBYCAgEPDxYCHwEFBTE1NjYzZBYCZg8VAREwOTg1ODEyIEIuQy4gTHRkLmQCAQ9kFgJmDxUBEUZlYnJ1YXJ5IDE5LCAyMDE5ZAICD2QWAmYPFQEHQ29tcGFueWQCAg9kFgZmD2QWAgIBDw8WAh8BBQQ3NjkyZBYCZg8VARQxMzAwMzAyIEFsYmVydGEgSW5jLmQCAQ9kFgJmDxUBEU5vdmVtYmVyIDA2LCAyMDE4ZAICD2QWAmYPFQEHQ29tcGFueWQCAw9kFgZmD2QWAgIBDw8WAh8BBQQ3NjYwZBYCZg8VARQxODI2NDg3IEFsYmVydGEgTHRkLmQCAQ9kFgJmDxUBEE9jdG9iZXIgMDMsIDIwMThkAgIPZBYCZg8VAQdDb21wYW55ZAIED2QWBmYPZBYCAgEPDxYCHwEFBTE3OTU0ZBYCZg8VARIzVyBHaWFudCBNYXJ0IEluYy5kAgEPZBYCZg8VAQxNYXkgMTYsIDIwMTlkAgIPZBYCZg8VAQdDb21wYW55ZAIFD2QWBmYPZBYCAgEPDxYCHwEFBTE1NzQ3ZBYCZg8VARM0MjQxODk0IENhbmFkYSBpbmMuZAIBD2QWAmYPFQEQT2N0b2JlciAzMSwgMjAxOGQCAg9kFgJmDxUBB0NvbXBhbnlkAgYPZBYGZg9kFgICAQ8PFgIfAQUFMTU2NjJkFgJmDxUBEzgwMjIyNzUgQ2FuYWRhIEluYy5kAgEPZBYCZg8VARFGZWJydWFyeSAxOSwgMjAxOWQCAg9kFgJmDxUBB0NvbXBhbnlkAgcPZBYGZg9kFgICAQ8PFgIfAQUFMTc5MTlkFgJmDxUBFjkxMTAtOTA1OCBRdcOpYmVjIGluYy5kAgEPZBYCZg8VAQ5BcHJpbCAyNiwgMjAxOWQCAg9kFgJmDxUBB0NvbXBhbnlkAggPZBYGZg9kFgICAQ8PFgIfAQUENjQwMGQWAmYPFQEWOTI0OC04NTQzIFF1w6liZWMgSW5jLmQCAQ9kFgJmDxUBEUZlYnJ1YXJ5IDA2LCAyMDE5ZAICD2QWAmYPFQEHQ29tcGFueWQCCQ9kFgZmD2QWAgIBDw8WAh8BBQUxODk3MWQWAmYPFQEWOTI2MS0zODAxIFF1w6liZWMgaW5jLmQCAQ9kFgJmDxUBElNlcHRlbWJlciAxOSwgMjAxOGQCAg9kFgJmDxUBB0NvbXBhbnlkAgoPZBYGZg9kFgICAQ8PFgIfAQUFMTY4NjNkFgJmDxUBFUFkYW0sIE1JdGNoZWxsIEdvcmRvbmQCAQ9kFgJmDxUBDk1hcmNoIDA0LCAyMDE5ZAICD2QWAmYPFQEGUGVyc29uZAILD2QWBmYPZBYCAgEPDxYCHwEFBDU5MTdkFgJmDxUBF0FkZGlzb24sIEJsYWlyIEhhcmNvdXJ0ZAIBD2QWAmYPFQENSnVuZSAwNSwgMjAxOWQCAg9kFgJmDxUBBlBlcnNvbmQCDA9kFgZmD2QWAgIBDw8WAh8BBQUxMzU1NWQWAmYPFQENQWlkYW4gVHJhZGluZ2QCAQ9kFgJmDxUBEU5vdmVtYmVyIDMwLCAyMDE4ZAICD2QWAmYPFQEHQ29tcGFueWQCDQ9kFgZmD2QWAgIBDw8WAh8BBQUxNjg0NGQWAmYPFQESQWxhdGhhbW5hLCBHaGFzc2FuZAIBD2QWAmYPFQEQSmFudWFyeSAzMSwgMjAxOWQCAg9kFgJmDxUBBlBlcnNvbmQCDg9kFgZmD2QWAgIBDw8WAh8BBQUxNjg1NWQWAmYPFQEbQWxwaGFOb3J0aCBBc3NldCBNYW5hZ2VtZW50ZAIBD2QWAmYPFQERRmVicnVhcnkgMTksIDIwMTlkAgIPZBYCZg8VAQdDb21wYW55ZAIPD2QWBmYPZBYCAgEPDxYCHwEFBTE2ODcxZBYCZg8VARZBbmRlcnNvbiwgTm9ybWFuIERhdmlkZAIBD2QWAmYPFQEOTWFyY2ggMDgsIDIwMTlkAgIPZBYCZg8VAQZQZXJzb25kAhAPZBYGZg9kFgICAQ8PFgIfAQUFMTY3NzJkFgJmDxUBEkFuZGVyc29uLCBSaXNhIERlZWQCAQ9kFgJmDxUBEURlY2VtYmVyIDEwLCAyMDE4ZAICD2QWAmYPFQEGUGVyc29uZAIRD2QWBmYPZBYCAgEPDxYCHwEFBTE3OTQ1ZBYCZg8VAQpBb3VuLCBKaXZlZAIBD2QWAmYPFQEOTWFyY2ggMjYsIDIwMTlkAgIPZBYCZg8VAQZQZXJzb25kAhIPZBYGZg9kFgICAQ8PFgIfAQUFMTY4ODZkFgJmDxUBFEFyY2FuZCwgSG93YXJkIFBldGVyZAIBD2QWAmYPFQEOTWFyY2ggMjgsIDIwMTlkAgIPZBYCZg8VAQZQZXJzb25kAhMPZBYGZg9kFgICAQ8PFgIfAQUFMTY4NDdkFgJmDxUBFUFyY2hlciwgQnJhZGxleSBXYXluZWQCAQ9kFgJmDxUBEEphbnVhcnkgMzEsIDIwMTlkAgIPZBYCZg8VAQZQZXJzb25kAhQPZBYGZg9kFgICAQ8PFgIfAQUFMTU2NjZkFgJmDxUBDUFTQkNGaW5hbmNpYWxkAgEPZBYCZg8VARBPY3RvYmVyIDI1LCAyMDE4ZAICD2QWAmYPFQEHQ29tcGFueWQCFQ8PFgIfAGhkZAIHDw8WAh8AZ2QWBAIDDw8WAh8EBWA8SU1HIFNSQz0nL2ltYWdlcy9kaXNjaXBsaW5lZGxpc3QvYXJyb3ctbGVmdC5wbmcnIHN0eWxlPSdib3JkZXI6MDsgcGFkZGluZy1yaWdodDo0cHgnLz5QcmV2IHBhZ2VkZAIHDw8WAh8EBWBOZXh0IHBhZ2U8SU1HIFNSQz0nL2ltYWdlcy9kaXNjaXBsaW5lZGxpc3QvYXJyb3ctcmlnaHQucG5nJyBzdHlsZT0nYm9yZGVyOjA7IHBhZGRpbmctbGVmdDo0cHgnLz5kZAIJDw8WAh8KBSt%2BL2ltYWdlcy9kaXNjaXBsaW5lZGxpc3QvZXhwb3J0X3Jlc3VsdHMuZ2lmZGQCDQ88KwARAgEQFgAWABYADBQrAABkAg8PPCsAEQIBEBYAFgAWAAwUKwAAZAIMD2QWBgIBDw8WAh8EBQdEZXRhaWxzZGQCBQ8PFgIfBAUETmFtZWRkAg0PPCsACQBkGAQFHl9fQ29udHJvbHNSZXF1aXJlUG9zdEJhY2tLZXlfXxYDBRRjdGwwMCRpbWdidG5fZW5nbGlzaAUcY3RsMDAkc2VhcmNoQm94JEltYWdlQnV0dG9uMQUdY3RsMDAkYm9keUNvbnRlbnQkaWJ0bl9zZWFyY2gFHmN0bDAwJGJvZHlDb250ZW50JGd2X2V4cG9ydGFsbA9nZAUbY3RsMDAkYm9keUNvbnRlbnQkZ3ZfZXhwb3J0D2dkBRljdGwwMCRib2R5Q29udGVudCRndl9saXN0DzwrAAwBCAIBZD0zjqy7Lx8DNZv9d1IfNnVyBiPC";
            string localEktronClientManager = "-1759591071%2C-569449246%2C-1939951303%2C-1080527330%2C-1687560804%2C-1388997516%2C2009761168%2C27274999%2C1979897163%2C-422906301%2C-1818005853%2C-1008700845%2C-845549574%2C-1619052588";
            string localViewGenerator = "B921F6E5";
            string localEventValidation = "%2FwEdAIoBDse5sZd961MyjYnOIzdXLPdQ7%2BSNE5tidFDF1oIQ63RClNF3RcsvXI1g0xzmTm0B3Spt4Ux3dSX7l2zHTTitF3vQqKIhyMomRydueASUTZ5J0xOflFQG25xkU6CuDg%2BES5USQYThYoTxyAKc3MZ4wsob71msGHSPICzI9Kkkb24Wtr%2Fg2H%2FdU8K4Mx9QX%2BXHii8LXhA8gVI%2FMfRX0JrFqCqkY3Lpy5UYyhs%2FnNt5%2BAENoRQuXY7wwDkO4ti3kJUx9IVbkXhCyENywNC%2FqWW8fbIM2xtCIwiuTp255ZmFN6aBc85Rk5fKYL%2BzkNhe0C2eG3ha%2Bw8YZe%2BAL6jQu8eVhHzUV53vIDrIBGinlEy3PuQg0JUG%2B147FiG4oEi%2B7pKJ43XwOnytaVB1MHPrHaMVwYCRYzNFAnWHe3Jw91TX5Lky2ztBoomlQ0igoDQgEFrzhsPA6XOYMMvWvrlD9AKsBQUSAONP0FWQ304JIOQc%2F9XaGzw%2B%2Fgrp9NZLB%2FusSp4M39KxEVcjBzwthy3vC1dL2cKcPocFpdigmLAbq144leAEruzv5PswhDBGI9np30zGEg%2BRyg9TAUPTa428vwLoH2ptrnV6O5oHrwX16hs%2BquyKLs%2BoeH%2BYQxlXF0tj2ed6ZwE62kWQdT5DZ82vp9Gdb2QcVBdj%2F%2BuKxo8Yo81XAk%2BMA3OMCUxipzZzGJ5abU%2Fb%2FV6xhK87q0ig%2B9nPILleqEDLr%2BiQR4K80RM1cNpyyZj2R%2F9qfiAtgpSCWje4aXNVvp1492fXJsm4TZoR00XqDs5dMHUtT1qpr9WR3WV3hQq%2FaJWMkQNXc5noy1gDnnCluB4c3YAHoArsbegPx5HhdT3UKb6tQhDL4iQRU7PTcS7Vx8WFgkmC%2Bc%2FHD0oQnnLpwQhU1%2B8WCwCWaXDC3MPpEELVbdx4iKSY8oWn5ae7uUJjXa2DD0oIrH%2FOyIrINdJZ4Tn4VqR7j3ZSWo5cNGhQk4wWX1Dt5RBMoLmYFT3DDK1PwSCa2j%2FWJJ3Rlsfa3EZ1c2V3aS%2B9soS5Vr4sXXi78wVtiluxL89JQsW%2BoacRfPfLSyc8Y02jQOGci50Bry8WZAQuX9lD2LdEAbXPKqMcoGo5c04pNm4HWEaAwgVn2P8%2F3P9wAaCQnZ9NyrfeThA8DMrDtWUg8VAEl7e%2Bq07CVK%2FNtlo38QJyWjAAQNhbDzQj1QBfI1imnej7sf8ILlDvtMmit0AFsdoF36Gp4KqRbhzUHaDK%2F1KDINGSzMbDHa6hFCITrLL1mhMLf1oMehU6GBTLN%2BG53BrPTzoYYeL7S1zVs%2BO%2FjrSXhxANQT%2BazYndzUncBr5Tc8QzLeApXN2HuZojN0npufZnhvZNVIiIpmMLMsaI383IS6squfw3JeYVsd7JRkm%2Bh6zGid0fn%2Bw3bC6h7BNMwlReluklzBCtdJCRWDSd2u%2FgmpEp7Y0QMQqKq7A987dHFwokaDh4C%2F0NvLxlJcsevt%2FoeaCEJmavOrKvgXpvVEDUXRyeisQQQ7jvBjGHx1slcTwnTx7mny7MZ10dq%2FjFQY3qPhNqM5q9C4Wt01ig8gcY6tnsYY5mnmpDh1jOn8%2BqESNoshe8%2BGUq%2FstLUPCYrZ53kfzTKT7btJ9G%2FgqjFlnYqSu%2BnIWg%2Fq1pA%2BhAKdIjLO091MB8gta4D8GNGuArCD5Ikp0bic5ixWNUJPxIC8olLBnm4W0u8csbnGPGVzxkQsU1th2xPoCw74crp%2FO%2B7Lgfh7rgBemolWIEFzzKfQFtvDlqJeWydSUZ%2FaHWPd1rK997CPW5VGpNO5KeVgpn8lbbt5ujSfEvh5NtyypZyGlDonw09AWAkErTF%2F9GiDrVul0CiWezs28Sxil0DGZ1BOQLyx01zUib8B1RjKfp4rJrORB1fZQ3wveP5ZlOezLWMckY0%2FvsK3iZT3RmlBBZp8IfFpLcfA69SHCeNDS0yIqzNNv79mJ26LheH%2BadBTBD7cHiSsz0LB%2BEdVCoFD5ZVnKEwwmRZVBXE30xyebtCRzlFODJOj%2BlJtY4DlpLbuqz4IajzMMfAOFudfdmqJqGas5Fl95OaYeXmx8EUneUr2bPvJ75beC7oSXsp6xBUdZyQpixhGQePyoeDgYa%2BIn6LpXd7hGmVN2aXL4U6TA9wg0e%2B%2F6BFR40YzZrtushWvpwXj%2FPi%2FYboqqE6m1AfJRDgUGDp4QrV1Tg9XeGtNZiQtC2jwpe9YATPMKIw9eNPfbvXNc0CXG0%2BgCnjaeAco4nMzlmt%2BQ0CrRT459b3E9wXLoAAnq%2Fvf2RZInpm6fnbKqGqMdW7POVdjqxWMJq%2FlyN3nPkAXiPCMBHq6hJA0zXTtOWVWErAh%2Bq3c%2BXkF0qYpSI0xT5JePqHmTLnntImaECTtpCvVQhyy40GxiUhzBxZXyfbIfmW%2FV1zT15XXVM9z%2BUE3FvNQRkOKinpMdTFT8%2FUTfhUK2qb0Vpx0q4Ci2K6roCXPvqSHT3G6SshXAk4ZrfUni%2FlIRWXm1pTkQ3fv0hUcT%2BuytUba45ytjt%2FMxOAETKNQ6QbpldeN2lTaCACkxGViEqduxLEsTFwHFKfXuKzeWF79YBxQheouGxcnfVDbG%2FZDEj%2F7Iu4PJSt%2FNE7IzLL0DG9U9jkQ9cb2zWWsCsXLQlpwhItQX8nlzP0hPhbsm7pzH5zYqIUoRZHgtdlGmLq5LN8Oi%2BNqq66wNu9AsLSv%2FNiQhau9h6EQ5iDl7kDKbD%2F1%2BipUouhfaQarFSgpd35HLZloXL4FkY5TQ2FVm5SFM4aDj9yqKxO85w51N3vkJrKVQQKCo0fsXD3LU6LvrKpnuatkvHtF61hIEFyJWIuuZ4GWxlaJt%2Bk8EX9g90yERabIDIqFIx96VMno79Vz3Lm9L4aR2iuQHJexEzHXCC6Z3%2FY7N8FCTjmwaAHAvoRqBBiU79CDOgnGSi6xXRmhMNQB8sDiJbyspRRTPif8g8EK0s0NQvGsnj%2FpwVaik%3D";

            if (FormData["VIEWSTATE"] != "")
            {
                localViewState = HttpUtility.UrlEncode(FormData["VIEWSTATE"]);
                localEktronClientManager = HttpUtility.UrlEncode(FormData["EktronClientManager"]);
                localViewGenerator = HttpUtility.UrlEncode(FormData["VIEWSTATEGENERATOR"]);
                localEventValidation = HttpUtility.UrlEncode(FormData["EVENTVALIDATION"]);
            }

            var pageUrlEncoded = HttpUtility.UrlEncode(page);
            return $"__EVENTTARGET={pageUrlEncoded}&__EVENTARGUMENT=&EktronClientManager={localEktronClientManager}-&__VIEWSTATE={localViewState}&__VIEWSTATEGENERATOR={localViewGenerator}&__SCROLLPOSITIONX=0&__SCROLLPOSITIONY=1450&__EVENTVALIDATION={localEventValidation}&ctl00%24searchBox%24searchTxt=&ctl00%24bodyContent%24datepickerLanguage=en&ctl00%24bodyContent%24tx_name=&ctl00%24bodyContent%24RulingBodyDropDownListControl1%24RulingDropDownListControl%24DropDownList1=-&ctl00%24bodyContent%24ViolationDropDownListControl1%24DropDownListControl1%24DropDownList1=-&ctl00%24bodyContent%24SanctionDropDownListControl1%24DropDownListControl1%24DropDownList1=-&ctl00%24bodyContent%24txtFromDate=&ctl00%24bodyContent%24txtToDate=";
        }

        private string getPaginationParams(string FromDate, string ToDate, Dictionary<string, string> FormData, string Page = "1")
        {
            string localViewState = "%2FwEPDwUKMTQ0NDE2NDAyNg9kFgJmD2QWAgIDEGRkFgwCAw8PFgIeB1Zpc2libGVoZGQCBQ9kFgICAQ8PFgIeD0NvbW1hbmRBcmd1bWVudAVIaHR0cDovL3d3dy5hdXRvcml0ZXMtdmFsZXVycy1tb2JpbGllcmVzLmNhL2Rpc2NpcGxpbmVkcGVyc29ucy5hc3B4P0lEPTc0FgQeC29uTW91c2VPdmVyBUhNTV9zd2FwSW1hZ2UoJ2N0bDAwX2ltZ2J0bl9lbmdsaXNoJywnJywnL2ltYWdlcy91dGlsL2ZyZW5jaC1vdmVyLnBuZycsMSkeCm9uTW91c2VPdXQFE01NX3N3YXBJbWdSZXN0b3JlKClkAgcPDxYCHwBoZBYCAgEPDxYCHwEFSGh0dHA6Ly93d3cuYXV0b3JpdGVzLXZhbGV1cnMtbW9iaWxpZXJlcy5jYS9kaXNjaXBsaW5lZHBlcnNvbnMuYXNweD9JRD03NBYEHwIFSE1NX3N3YXBJbWFnZSgnY3RsMDBfaW1nYnRuX2ZyZW5jaCcsJycsJy9pbWFnZXMvdXRpbC9lbmdsaXNoLW92ZXIucG5nJywxKR8DBRNNTV9zd2FwSW1nUmVzdG9yZSgpZAIJD2QWAmYPZBYGAgEPFgIeBFRleHQFuQI8c2NyaXB0IGxhbmd1YWdlPSJqYXZhc2NyaXB0Ij4NCmZ1bmN0aW9uIG5vZW50ZXIoKSB7DQogICAgaWYgKHdpbmRvdy5ldmVudCAmJiB3aW5kb3cuZXZlbnQua2V5Q29kZSA9PSAxMykgew0KICAgICAgICB3aW5kb3cubG9jYXRpb24uaHJlZiA9ICdzZWFyY2guYXNweD9zZWFyY2h0ZXh0PScgKyBkb2N1bWVudC5nZXRFbGVtZW50QnlJZCgnY3RsMDBfc2VhcmNoQm94X3NlYXJjaFR4dCcpLnZhbHVlOyANCiAgICAgICAgcmV0dXJuIGZhbHNlOyANCiAgICB9IGVsc2Ugew0KICAgICAgICByZXR1cm4gdHJ1ZTsgDQogICAgfSANCn0NCjwvc2NyaXB0Pg0KZAIDDw9kFgIeCm9ua2V5cHJlc3MFHGphdmFzY3JpcHQ6cmV0dXJuIG5vZW50ZXIoKTtkAgUPD2QWBB4Lb25tb3VzZW92ZXIFLmphdmFzY3JpcHQ6dGhpcy5zcmM9Jy9pbWFnZXMvdXRpbC9nby1vdmVyLmdpZiceCm9ubW91c2VvdXQFKWphdmFzY3JpcHQ6dGhpcy5zcmM9Jy9pbWFnZXMvdXRpbC9nby5naWYnZAILD2QWAgIDDw8WAh8AaGRkAg0PZBYMZg8PFgIfBAULRW5mb3JjZW1lbnRkZAIGDw8WAh8EBQEyZGQCBw8PFgIfBAUCMTlkZAIIDw8WAh8EZWRkAgsPZBYIAgEPZBYEAgEPDxYCHwQFTjxpbWcgYm9yZGVyPScwJyBhbGlnbj0nYWJzbWlkZGxlJyBzcmM9Jy9pbWFnZXMvdXRpbC9yc3MtaWNvbi5qcGcnIC8%2BIFJTUyBGZWVkc2RkAgMPDxYCHwQFGEJyb3dzZSBBbHBoYWJldGljYWwgTGlzdGRkAgMPZBYUAgEPDxYCHwQFF0Rpc2NpcGxpbmVkIExpc3QgU2VhcmNoZGQCAw8PFgIfBAUETmFtZWRkAgcPDxYCHwQFC1J1bGluZyBCb2R5ZGQCCQ9kFgJmDw8WBB4QTk9ORUxpc3RJdGVtVGV4dAUHQW55L0FsbB4LRGlzcGxheU1vZGULKU5VSUwuRGlzcGxheU1vZGUsIENTQVdlYiwgVmVyc2lvbj0xLjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPW51bGwBZBYEAgEPDxYCHwBoZGQCAw8QDxYCHwBnZBAVFQdBbnkvQWxsI0FsYmVydGEgU2VjdXJpdGllcyBDb21taXNzaW9uIChBU0MpJ0F1dG9yaXTDqSBkZXMgbWFyY2jDqXMgZmluYW5jaWVycyAoQU1GKS1Ccml0aXNoIENvbHVtYmlhIFNlY3VyaXRpZXMgQ29tbWlzc2lvbiAoQkNTQyk5Q2FuYWRpYW4gU2VjdXJpdGllcyBBZG1pbmlzdHJhdG9ycyAoSW52ZXN0b3IgQWxlcnQpIChDU0EpKkNoYW1icmUgZGUgbGEgc8OpY3VyaXTDqSBmaW5hbmNpw6hyZSAoQ1NGKRxDb3VydCAoQWxiZXJ0YSkgKENvdXJ0IChBQikpHUNvdXJ0IChNYW5pdG9iYSkgKENvdXJ0IChNQikpHENvdXJ0IChPbnRhcmlvKSAoQ291cnQgKE9OKSkcQ291cnQgKFF1w6liZWMpIChDb3VydCAoUUMpKT9GaW5hbmNpYWwgYW5kIENvbnN1bWVyIEFmZmFpcnMgQXV0aG9yaXR5IG9mIFNhc2thdGNoZXdhbiAoRkNBQSkxRmluYW5jaWFsIGFuZCBDb25zdW1lciBTZXJ2aWNlcyBDb21taXNzaW9uIChGQ05CKVVHb3Zlcm5tZW50IG9mIE5ld2ZvdW5kbGFuZCBhbmQgTGFicmFkb3IgLSBGaW5hbmNpYWwgU2VydmljZXMgUmVndWxhdGlvbiBEaXZpc2lvbiAoTkwpPUludmVzdG1lbnQgSW5kdXN0cnkgUmVndWxhdG9yeSBPcmdhbml6YXRpb24gb2YgQ2FuYWRhIChJSVJPQykkTWFuaXRvYmEgU2VjdXJpdGllcyBDb21taXNzaW9uIChNU0MpME11dHVhbCBGdW5kIERlYWxlcnMgQXNzb2NpYXRpb24gb2YgQ2FuYWRhIChNRkRBKShOb3ZhIFNjb3RpYSBTZWN1cml0aWVzIENvbW1pc3Npb24gKE5TU0MpO09mZmljZSBvZiB0aGUgU3VwZXJpbnRlbmRlbnQgb2YgU2VjdXJpdGllcyBmb3IgTnVuYXZ1dCAoTlUpI09udGFyaW8gU2VjdXJpdGllcyBDb21taXNzaW9uIChPU0MpNFRyaWJ1bmFsIGFkbWluaXN0cmF0aWYgZGVzIG1hcmNow6lzIGZpbmFuY2llcnMgKFRNRikfWXVrb24gU2VjdXJpdGllcyBPZmZpY2UgKFl1a29uKRUVAS0CMTgCMTIBNwIyOQIyMwIzMAIyNAIyNQIxNAIxMwIxMQIyNgIyMgIxMAIyMQIxNgIyNwIxOQIxNQIyOBQrAxVnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2cWAWZkAgsPZBYEZg9kFgICAQ8PFgIfBAUZVmlvbGF0aW9ucyBhbmQvb3IgY29uZHVjdGRkAgEPZBYCAgEPZBYCZg8PFgQfCAUHQW55L0FsbB8JCysEAWQWBAIBDw8WAh8AaGRkAgMPEA8WAh8AZ2QQFR0HQW55L0FsbCZBY3RpbmcgY29udHJhcnkgdG8gdGhlIHB1YmxpYyBpbnRlcmVzdDBBdXRob3JpemVkLCBwZXJtaXR0ZWQgb3IgYWNxdWllc2NlZCBpbiB2aW9sYXRpb24PQnJlYWNoIG9mIG9yZGVyEkNhcGl0YWwgZGVmaWNpZW5jeTpDb25mbGljdCBvZiBpbnRlcmVzdCBhbmQvb3IgYWN0aW5nIGFnYWluc3QgY2xpZW50IGludGVyZXN0FURpc2Nsb3N1cmUgdmlvbGF0aW9ucytGYWlsdXJlIHRvIGNvbXBseSB3aXRoIHRlcm1zIGFuZCBjb25kaXRpb25zFEZhaWx1cmUgdG8gY29vcGVyYXRlQUZhaWx1cmUgdG8gZGlzY2hhcmdlIGtub3cgeW91ciBjbGllbnQgYW5kIHN1aXRhYmlsaXR5IG9ibGlnYXRpb25zIEZhaWx1cmUgdG8gZmlsZSBpbnNpZGVyJ3MgcmVwb3J0KEZhaWx1cmUgdG8gZnVsZmlsbCBzcGVjaWZpYyB1bmRlcnRha2luZ3M2RmFpbHVyZSB0byBtZWV0IGZpbmFuY2lhbCBhbmQgb3BlcmF0aW9uYWwgcmVxdWlyZW1lbnRzKUZyYXVkIGFuZC9vciBGb3JnZXJ5IGFuZC9vciBGYWxzaWZpY2F0aW9uC0dhdGVrZWVwaW5nF0lsbGVnYWwgaW5zaWRlciB0cmFkaW5nJUlsbGVnYWwgb3IgdW5yZWdpc3RlcmVkIGRpc3RyaWJ1dGlvbnMqSW1wcm9wZXIgcmVsaWFuY2Ugb24gcHJvc3BlY3R1cyBleGVtcHRpb25zHEluYWRlcXVhdGUgY29tcGxpYW5jZSBzeXN0ZW0bSW50ZXJuYWwgY29udHJvbCB2aW9sYXRpb25zJU1ha2luZyBmYWxzZSBvciBtaXNsZWFkaW5nIHN0YXRlbWVudHMTTWFya2V0IG1hbmlwdWxhdGlvbhJNaXNyZXByZXNlbnRhdGlvbnMFT3RoZXIZUHJvaGliaXRlZCByZXByZXNlbnRhdGlvbjRVbnJlZ2lzdGVyZWQgYWN0aXZpdGllcyBhbmQvb3IgdW5hcHByb3ZlZCBhY3Rpdml0aWVzMlVucmVnaXN0ZXJlZCwgdW5hdXRob3JpemVkIGFuZC9vciBpbXByb3BlciB0cmFkaW5nHVVucmVwb3J0ZWQgY2FwaXRhbCBkZWZpY2llbmN5LVVuc3VpdGFibGUgaW52ZXN0bWVudHMgYW5kL29yIHJlY29tbWVuZGF0aW9ucxUdAS0BMQIxOAEyAjI1AjE2AjEwAjI3AjEyAjI0ATMCMjgCMjYBNAIxNQE5ATUCMjMCMjECMTMCMTkCMTEBNgIxNwIyMAE3ATgCMjICMTQUKwMdZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2cWAWZkAg0PZBYEZg9kFgICAQ8PFgIfBAUIU2FuY3Rpb25kZAIBD2QWAgIBD2QWAmYPDxYEHwgFB0FueS9BbGwfCQsrBAFkFgQCAQ8PFgIfAGhkZAIDDxAPFgIfAGdkEBURB0FueS9BbGwrQWNxdWlzaXRpb25zIGJhbiB3aXRoIG9yIHdpdGhvdXQgZXhjZXB0aW9ucxtBZG1pbmlzdHJhdGl2ZSBQZW5hbHR5L0ZpbmUWQ29tcGxldGlvbiBvZiB0cmFpbmluZ0JEaXJlY3RvciwgT2ZmaWNlciBhbmQvb3IgU3VwZXJ2aXNvciBCYW4gd2l0aCBvciB3aXRob3V0IGV4Y2VwdGlvbnMZRGlzZ29yZ2VtZW50IC9SZXN0aXR1dGlvbjFFeGVtcHRpb24gaW5hcHBsaWNhYmxlIHdpdGggb3Igd2l0aG91dCBleGNlcHRpb25zDEltcHJpc29ubWVudDFJbnZlc3RvciByZWxhdGlvbnMgYmFuIHdpdGggb3Igd2l0aG91dCBleGNlcHRpb25zBU90aGVyQVJlZ2lzdHJhdGlvbiBiYW4gKHdpdGggb3Igd2l0aG91dCBleGNlcHRpb25zKSBhbmQvb3IgcmVzdHJpY3Rpb25zFFJlZ2lzdHJhdGlvbiByZWZ1c2FsCVJlcHJpbWFuZBVTcGVjaWZpYyB1bmRlcnRha2luZ3MYU3VzcGVuc2lvbiBvZiBNZW1iZXJzaGlwGVRlcm1pbmF0aW9uIG9mIE1lbWJlcnNoaXAmVHJhZGluZyBiYW4gd2l0aCBvciB3aXRob3V0IGNvbmRpdGlvbnMVEQEtAjEyATUBMwExAjEwAjEzATgBNAIxMQE3AjE0ATkBNgIxNQIxNgEyFCsDEWdnZ2dnZ2dnZ2dnZ2dnZ2dnFgFmZAIPDw8WAh8EBQRGcm9tZGQCEw8PFgIfBAUCVG9kZAIXDw8WAh4ISW1hZ2VVcmwFI34vaW1hZ2VzL2Rpc2NpcGxpbmVkbGlzdC9zZWFyY2guZ2lmZGQCGQ8PFgIfCgUvfi9pbWFnZXMvZGlzY2lwbGluZWRsaXN0L2V4cG9ydF9lbnRpcmVfbGlzdC5naWZkZAIFDw8WAh8EZWRkAgcPZBYOAgEPDxYEHwQFGkxhdGVzdCAxMCBEaXNjaXBsaW5lZCBMaXN0HwBoZGQCAw8PFgIfAGdkFgQCAw8PFgIfBAVgPElNRyBTUkM9Jy9pbWFnZXMvZGlzY2lwbGluZWRsaXN0L2Fycm93LWxlZnQucG5nJyBzdHlsZT0nYm9yZGVyOjA7IHBhZGRpbmctcmlnaHQ6NHB4Jy8%2BUHJldiBwYWdlZGQCBw8PFgIfBAVgTmV4dCBwYWdlPElNRyBTUkM9Jy9pbWFnZXMvZGlzY2lwbGluZWRsaXN0L2Fycm93LXJpZ2h0LnBuZycgc3R5bGU9J2JvcmRlcjowOyBwYWRkaW5nLWxlZnQ6NHB4Jy8%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YPFQEcQm9hc3NhbHksIFNjb3R0IEFsbGFuIEdlb3JnZWQCAQ9kFgJmDxUBDk1hcmNoIDI4LCAyMDE5ZAICD2QWAmYPFQEGUGVyc29uZAITD2QWBmYPZBYCAgEPDxYCHwEFBTE2ODMxZBYCZg8VARhCb2RuYXJjaHVrLCBFZHdhcmQgUGV0ZXJkAgEPZBYCZg8VARBPY3RvYmVyIDAyLCAyMDE4ZAICD2QWAmYPFQEGUGVyc29uZAIUD2QWBmYPZBYCAgEPDxYCHwEFBTE2Nzk1ZBYCZg8VARpCb2Rzb24sIE1hcmMgSm9zZXBoIFJvYmVydGQCAQ9kFgJmDxUBEURlY2VtYmVyIDE5LCAyMDE4ZAICD2QWAmYPFQEGUGVyc29uZAIVDw8WAh8AaGRkAgcPDxYCHwBnZBYEAgMPDxYCHwQFYDxJTUcgU1JDPScvaW1hZ2VzL2Rpc2NpcGxpbmVkbGlzdC9hcnJvdy1sZWZ0LnBuZycgc3R5bGU9J2JvcmRlcjowOyBwYWRkaW5nLXJpZ2h0OjRweCcvPlByZXYgcGFnZWRkAgcPDxYCHwQFYE5leHQgcGFnZTxJTUcgU1JDPScvaW1hZ2VzL2Rpc2NpcGxpbmVkbGlzdC9hcnJvdy1yaWdodC5wbmcnIHN0eWxlPSdib3JkZXI6MDsgcGFkZGluZy1sZWZ0OjRweCcvPmRkAgkPDxYCHwoFK34vaW1hZ2VzL2Rpc2NpcGxpbmVkbGlzdC9leHBvcnRfcmVzdWx0cy5naWZkZAINDzwrABECARAWABYAFgAMFCsAAGQCDw88KwARAgEQFgAWABYADBQrAABkAgwPZBYGAgEPDxYCHwQFB0RldGFpbHNkZAIFDw8WAh8EBQROYW1lZGQCDQ88KwAJAGQYBAUeX19Db250cm9sc1JlcXVpcmVQb3N0QmFja0tleV9fFgMFFGN0bDAwJGltZ2J0bl9lbmdsaXNoBRxjdGwwMCRzZWFyY2hCb3gkSW1hZ2VCdXR0b24xBR1jdGwwMCRib2R5Q29udGVudCRpYnRuX3NlYXJjaAUeY3RsMDAkYm9keUNvbnRlbnQkZ3ZfZXhwb3J0YWxsD2dkBRtjdGwwMCRib2R5Q29udGVudCRndl9leHBvcnQPZ2QFGWN0bDAwJGJvZHlDb250ZW50JGd2X2xpc3QPPCsADAEIAgFk5cV5huRpOuEmI6plMxVOmcz%2BsG8%3D";
            string localEktronClientManager = "-1759591071%2C-569449246%2C-1939951303%2C-1080527330%2C-1687560804%2C-1388997516%2C2009761168%2C27274999%2C1979897163%2C-422906301%2C-1818005853%2C-1008700845%2C-845549574%2C-1619052588";
            string localViewGenerator = "B921F6E5";
            string localEventValidation = "%2FwEdAIoBabpgcVTUErWUpSBsrCQ1vvdQ7%2BSNE5tidFDF1oIQ63RClNF3RcsvXI1g0xzmTm0B3Spt4Ux3dSX7l2zHTTitF3vQqKIhyMomRydueASUTZ5J0xOflFQG25xkU6CuDg%2BES5USQYThYoTxyAKc3MZ4wsob71msGHSPICzI9Kkkb24Wtr%2Fg2H%2FdU8K4Mx9QX%2BXHii8LXhA8gVI%2FMfRX0JrFqCqkY3Lpy5UYyhs%2FnNt5%2BAENoRQuXY7wwDkO4ti3kJUx9IVbkXhCyENywNC%2FqWW8fbIM2xtCIwiuTp255ZmFN6aBc85Rk5fKYL%2BzkNhe0C2eG3ha%2Bw8YZe%2BAL6jQu8eVhHzUV53vIDrIBGinlEy3PuQg0JUG%2B147FiG4oEi%2B7pKJ43XwOnytaVB1MHPrHaMVwYCRYzNFAnWHe3Jw91TX5Lky2ztBoomlQ0igoDQgEFrzhsPA6XOYMMvWvrlD9AKsBQUSAONP0FWQ304JIOQc%2F9XaGzw%2B%2Fgrp9NZLB%2FusSp4M39KxEVcjBzwthy3vC1dL2cKcPocFpdigmLAbq144leAEruzv5PswhDBGI9np30zGEg%2BRyg9TAUPTa428vwLoH2ptrnV6O5oHrwX16hs%2BquyKLs%2BoeH%2BYQxlXF0tj2ed6ZwE62kWQdT5DZ82vp9Gdb2QcVBdj%2F%2BuKxo8Yo81XAk%2BMA3OMCUxipzZzGJ5abU%2Fb%2FV6xhK87q0ig%2B9nPILleqEDLr%2BiQR4K80RM1cNpyyZj2R%2F9qfiAtgpSCWje4aXNVvp1492fXJsm4TZoR00XqDs5dMHUtT1qpr9WR3WV3hQq%2FaJWMkQNXc5noy1gDnnCluB4c3YAHoArsbegPx5HhdT3UKb6tQhDL4iQRU7PTcS7Vx8WFgkmC%2Bc%2FHD0oQnnLpwQhU1%2B8WCwCWaXDC3MPpEELVbdx4iKSY8oWn5ae7uUJjXa2DD0oIrH%2FOyIrINdJZ4Tn4VqR7j3ZSWo5cNGhQk4wWX1Dt5RBMoLmYFT3DDK1PwSCa2j%2FWJJ3Rlsfa3EZ1c2V3aS%2B9soS5Vr4sXXi78wVtiluxL89JQsW%2BoacRfPfLSyc8Y02jQOGci50Bry8WZAQuX9lD2LdEAbXPKqMcoGo5c04pNm4HWEaAwgVn2P8%2F3P9wAaCQnZ9NyrfeThA8DMrDtWUg8VAEl7e%2Bq07CVK%2FNtlo38QJyWjAAQNhbDzQj1QBfI1imnej7sf8ILlDvtMmit0AFsdoF36Gp4KqRbhzUHaDK%2F1KDINGSzMbDHa6hFCITrLL1mhMLf1oMehU6GBTLN%2BG53BrPTzoYYeL7S1zVs%2BO%2FjrSXhxANQT%2BazYndzUncBr5Tc8QzLeApXN2HuZojN0npufZnhvZNVIiIpmMLMsaI383IS6squfw3JeYVsd7JRkm%2Bh6zGid0fn%2Bw3bC6h7BNMwlReluklzBCtdJCRWDSd2u%2FgmpEp7Y0QMQqKq7A987dHFwokaDh4C%2F0NvLxlJcsevt%2FoeaCEJmavOrKvgXpvVEDUXRyeisQQQ7jvBjGHx1slcTwnTx7mny7MZ10dq%2FjFQY3qPhNqM5q9C4Wt01ig8gcY6tnsYY5mnmpDh1jOn8%2BqESNoshe8%2BGUq%2FstLUPCYrZ53kfzTKT7btJ9G%2FgqjFlnYqSu%2BnIWg%2Fq1pA%2BhAKdIjLO091MB8gta4D8GNGuArCD5Ikp0bic5ixWNUJPxIC8olLBnm4W0u8csbnGPGVzxkQsU1th2xPoCw74crp%2FO%2B7Lgfh7rgBemolWIEFzzKfQFtvDlqJeWydSUZ%2FaHWPd1rK997CPW5VGpNO5KeVgpn8lbbt5ujSfEvh5NtyypZyGlDonw09AWAkErTF%2F9GiDrVul0CiWezs28Sxil0DGZ1BOQLyx01zUib8B1RjKfp4rJrORB1fZQ3wveP5ZlOezLWMckY0%2FvsK3iZT3RmlBBZp8IfFpLcfA69SHCeNDS0yIqzNNv79mJ26LheH%2BadBTBD7cHiSsz0LB%2BEdVCoFD5ZVnKEwwmRZVBXE30xyebtCRzlFODJOj%2BlJtY4DlpLbuqz4IajzMMfAOFudfdmqJqGas5Fl95OaYeXmx8EUneUr2bPvJ75beC7oSXsp6xBUdZyQpixhGQePyoeDgYa%2BIn6LpXd7hGmVN2aXL4U6TA9wg0e%2B%2F6BFR40YzZrtushWvpwXj%2FPi%2FYboqqE6m1AfJRDgUGDp4QrV1Tg9XeGtNZiQtC2jwpe9YATPMKIw9eNPfbvXNc0CXG0%2BgCnjaeAco4nMzlmt%2BQ0CrRT459b3E9wXLoAAnq%2Fvf2RZInpm6fnbKqGqMdW7POVdjqxWMJq%2FlyN3nPkAXiPCMBHq6hJA0zXTtOWVWErAh%2Bq3c%2BXkF0qYpSI0xT5JePqHmTLnntImaECTtpCvVQhyy40GxiUhzBxZXyfbIfmW%2FV1zT15XXVM9z%2BUE3FvNQRkOKinpMdTFT8%2FUTfhUK2qb0Vpx0q4Ci2K6roCXPvqSHT3G6SshXAk4ZrfUni%2FlIRWXm1pTkQ3fv0hUcT%2BuytUba45ytjt%2FMxOAETKNQ6QbpldeN2lTaCACkxGViEqduxLEsTFwHFKfXuKzeWF79YBxQheouGxcnfVDbG%2FZDEj%2F7Iu4PJSt%2FNE7IzLL0DG9U9jkQ9cb2zWWsCsXLQlpwhItQX8nlzP0hPhbsm7pzH5zYqIUoRZHgtdlGmLq5LN8Oi%2BNqq66wNu9AsLSv%2FNiQhau9h6EQ5iDl7kDKbD%2F1%2BipUouhfaQarFSgpd35HLZloXL4FkY5TQ2FVm5SFM4aDj9yqKxO85w51N3vkJrKVQQKCo0fsXD3LU6LvrKpnuatkvHtF61hIEFyJWIuuZ4GWxlaJt%2Bk8EX9g90yERabIDIqFIx96VMno79Vz3Lm9L4aR2iuQHJexEzHXCC6Z3%2FY7N8FCTjmwaAHAvoRqBBiU79CDOgnGSi6xXRmhMNQB8sDiJbyspRyHh5I%2Bt%2FfzXZIwd5Yt1V5cIf0zU%3D";
            string PageId = $"ctl00%24bodyContent%24lbtnPager{Page}";

            if (FormData["VIEWSTATE"] != "")
            {
                localViewState = HttpUtility.UrlEncode(FormData["VIEWSTATE"]);
                localEktronClientManager = HttpUtility.UrlEncode(FormData["EktronClientManager"]);
                localViewGenerator = HttpUtility.UrlEncode(FormData["VIEWSTATEGENERATOR"]);
                localEventValidation = HttpUtility.UrlEncode(FormData["EVENTVALIDATION"]);
            }


            return $"__EVENTTARGET={PageId}&__EVENTARGUMENT=&EktronClientManager={localEktronClientManager}&__VIEWSTATE={localViewState}&__VIEWSTATEGENERATOR={localViewGenerator}&__SCROLLPOSITIONX=125&__SCROLLPOSITIONY=1751&__EVENTVALIDATION={localEventValidation}&ctl00%24searchBox%24searchTxt=&ctl00%24bodyContent%24datepickerLanguage=en&ctl00%24bodyContent%24tx_name=&ctl00%24bodyContent%24RulingBodyDropDownListControl1%24RulingDropDownListControl%24DropDownList1=-&ctl00%24bodyContent%24ViolationDropDownListControl1%24DropDownListControl1%24DropDownList1=-&ctl00%24bodyContent%24SanctionDropDownListControl1%24DropDownListControl1%24DropDownList1=-&ctl00%24bodyContent%24txtFromDate={FromDate}&ctl00%24bodyContent%24txtToDate={ToDate}";
        }
    }
}
