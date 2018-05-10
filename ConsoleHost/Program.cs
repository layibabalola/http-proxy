using HttpProxy.Host;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleHost
{
    public class Program : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        static string proxyAddress = @"http://*:8080/";

        static void Main(string[] args) /* Console entry point */
        {
            // Need lots of outgoing connections and hang on to them
            ServicePointManager.DefaultConnectionLimit = 20;
            ServicePointManager.MaxServicePointIdleTime = 10000;
            //send packets as soon as you get them
            ServicePointManager.UseNagleAlgorithm = false;
            //send both header and body together
            ServicePointManager.Expect100Continue = false;

            Proxy.Start(proxyAddress);
            Console.ReadLine();
            Proxy.Stop();
        }


        public override void Run()  /* Worker Role entry point */
        {
            Trace.TraceInformation("HostWorkerRole is running");

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
            // Need lots of outgoing connections and hang on to them
            ServicePointManager.DefaultConnectionLimit = 20;
            ServicePointManager.MaxServicePointIdleTime = 10000;
            //send packets as soon as you get them
            ServicePointManager.UseNagleAlgorithm = false;
            //send both header and body together
            ServicePointManager.Expect100Continue = false;

            IEnumerable<IPEndPoint> endpoints;

            if (RoleEnvironment.IsEmulated)
            {
                endpoints = Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .Select(address => new IPEndPoint(address, 9100));
            }
            else
            {
                endpoints = RoleEnvironment.CurrentRoleInstance
                    .InstanceEndpoints
                    .Select(endpoint => endpoint.Value.IPEndpoint);
            }

            foreach (var endpoint in endpoints)
            {
                if (!endpoint.Address.ToString().Contains(":"))
                {
                    string baseUri = string.Format("{0}://{1}:{2}",
                        "http", endpoint.Address.ToString(), endpoint.Port);

                    Trace.TraceInformation(String.Format("Starting OWIN at {0}", baseUri));
                    Proxy.Start(baseUri);
                }
            }
            
            bool result = base.OnStart();

            Trace.TraceInformation("HostWorkerRole has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("HostWorkerRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            Proxy.Stop();
            base.OnStop();

            Trace.TraceInformation("HostWorkerRole has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            Trace.TraceInformation("Working");

            while (!cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}
