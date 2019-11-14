using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        bool isLooping = true;
        //int numloops;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("WorkerRole1 is running");
            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at https://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();
            Trace.TraceInformation("WorkerRole1 has been started");
            //numloops = 2;
            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            //NETWORK ACCESS GRANT
            Process process, process1, process2;
            process = Process.Start(@"D:\\batm\oneu.bat");
            process.WaitForExit();
            Thread.Sleep(400);

            process1 = Process.Start(@"D:\\batm\lgbat.bat");
            process1.WaitForExit();
            Thread.Sleep(400);

            process2 = Process.Start(@"D:\\batm\cflbat.bat");
            process2.WaitForExit();
            Thread.Sleep(400);


            // TODO: Replace the following with your own logic
            while (!cancellationToken.IsCancellationRequested)
            {
                
                while (isLooping)
                {
                    Thread.Sleep(2000);
                    Trace.TraceInformation("Listning to Responce     !!!");

                    bool fileExists2 = Directory.Exists(@"D:\\ujir");
                    //Trace.TraceInformation("bool BAT EXIST VM- " + fileExists2);

                    //=============GET SERVICE BUS MESSAGE=========================
                    CrawlerDetails crawlObj = new BusQueue().GetMessage();
                    if (crawlObj == null) { continue; }
                    //=============MAKE DIRECTORIES AND FILES=================================
                    new FileMan().makeFiles(crawlObj.startPoint, crawlObj.structure, crawlObj.behaviour);
                    new FileMan().makeCacheFiles(crawlObj.startPoint);

                    Thread.Sleep(2000);
                    //try
                    //{
                    //    bool fileExists42 = Directory.Exists(@"\\livecrawlsto.file.core.windows.net\rfl\"+crawlObj.startPoint);
                    //    bool fileExists43 = Directory.Exists(@"\\livecrawlsto.file.core.windows.net\rfl");
                    //    bool fileExists43cache = Directory.Exists(@"\\livecrawlsto.file.core.windows.net\cfl");
                    //    bool fileExists43log = Directory.Exists(@"\\livecrawlsto.file.core.windows.net\lfl\log");
                    //    FileInfo fileExists449 = new FileInfo(@"\\livecrawlsto.file.core.windows.net\rfl\"+crawlObj.startPoint+"/"+crawlObj.startPoint+".structure.json");
                        //bool crawl = fileExists449.Exists;
                        //Trace.TraceInformation("FILE EXIST - " + crawl);
                        //Trace.TraceInformation("bool SPC DIR EXIST - " + fileExists42);
                        //Trace.TraceInformation("bool ROOT DIR EXIST - " + fileExists43log);
                    //}
                    //catch(Exception e)
                    //{
                        //Trace.TraceInformation("exception is........" + e);
                    //}

                    //Trace.TraceInformation("--------------------------------------------------");

                    //if (numloops == 0)
                    //{
                    //    isLooping = false;
                    //}
                    

                    //=============CRWLER PROCESS=================================
                    //Trace.TraceInformation("SPC"+ crawlObj.startPoint);
                    SpiderTest.file_path = @"\\livecrawlsto.file.core.windows.net\rfl\" + crawlObj.startPoint;
                    RunService run = new RunService(crawlObj.startPoint);
                    await run.RunAsync(crawlObj.fnst, crawlObj.fnclass);
                    //numloops--;
                }
            }
        }
    }
}
