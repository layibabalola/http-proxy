using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using HttpProxy.Handlers;

[assembly: OwinStartup(typeof(HttpProxy.Host.Proxy))]

namespace HttpProxy.Host
{
    public class Proxy
    {
        static List<IDisposable> apps = new List<IDisposable>();

        public static void Start(string proxyAddress)
        {
            try
            {
                // Start OWIN proxy host 
                apps.Add(WebApp.Start<Proxy>(proxyAddress));
                Trace.WriteLine("Proxy server is running");
                Trace.WriteLine("Set your IE proxy to:" + proxyAddress);
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                if (ex.InnerException != null)
                    message += ":" + ex.InnerException.Message;
                Trace.TraceInformation(message);
            }
        }

        public static void Stop()
        {
            foreach (var app in apps)
            {
                if (app != null)
                    app.Dispose();
            }
        }


        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration httpconfig = new HttpConfiguration();
            RegisterRoutes(httpconfig);
            appBuilder.UseWebApi(httpconfig);
        }

        private void RegisterRoutes(HttpConfiguration config)
        {
            //anything that needs to fall through needs to go in the pipeline
            config.Routes.MapHttpRoute(
            name: "Proxy",
            routeTemplate: "{*path}",
            handler: HttpClientFactory.CreatePipeline
                (
                    innerHandler: new HttpClientHandler(), // will never get here if proxy is doing its job
                    handlers: new DelegatingHandler[]
                    {
                        new ProxyHandler()
                    }
                ),
            defaults: new { path = RouteParameter.Optional },
            constraints: null);
        }
    }
}
