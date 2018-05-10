using HttpProxy.Extensions;
using Microsoft.Azure;
using Microsoft.Azure.Storage;
using Microsoft.Azure.CosmosDB.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Threading;

namespace HttpProxy.Handlers
{
    public class ProxyHandler : DelegatingHandler
    {
        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            //have to explicitly null it to avoid protocol violation
            if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Trace) request.Content = null;

            var timeout = int.Parse(CloudConfigurationManager.GetSetting("timeout"));
            var retryThreshold = int.Parse(CloudConfigurationManager.GetSetting("retryThreshold"));
            var retryDelay = int.Parse(CloudConfigurationManager.GetSetting("retryDelay"));
            var retryCount = 0;

            HttpClient client = new HttpClient();
            try
            {
                while (retryCount < retryThreshold)
                {
                    if(retryCount > 0)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(retryDelay));
                    }

                    AddProxyRequestHeader(request);
                    Trace.TraceInformation("Request To:{0}", request.RequestUri.ToString());

                    client.Timeout = TimeSpan.FromSeconds(timeout);
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    if (response.IsSuccessStatusCode)
                    {
                        AddProxyResponseHeader(response);

                        if (request.Method == HttpMethod.Head)
                            response.Content = null;
                        return response;
                    }
                    else
                    {
                        switch (response.StatusCode.ToString())
                        {
                            case "408":
                            case "503":
                            case "504":
                                {
                                    retryCount++;
                                    break;
                                }
                            default:
                                {
                                    await LogRequestAsync(response.StatusCode, request);
                                    break;
                                }
                        }
                    }

                    if (retryCount >= retryThreshold)
                    {
                        return response;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                await LogRequestAsync(HttpStatusCode.InternalServerError, request);
                var response = request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
                string message = ex.Message;
                if (ex.InnerException != null)
                    message += ':' + ex.InnerException.Message;
                response.Content = new StringContent(message);
                Trace.TraceError("Error:{0}", message);
                return response;
            }
        }
        private async System.Threading.Tasks.Task LogRequestAsync(HttpStatusCode httpStatusCode, HttpRequestMessage request)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("httpproxylogs"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Retrieve a reference to the table.
            CloudTable table = tableClient.GetTableReference("requests");

            // Create the table if it doesn't exist.
            table.CreateIfNotExists();

            LogMessage log = new LogMessage();
            log.Method = request.Method.Method;
            log.HttpContent = await request.Content.ReadAsByteArrayAsync();
            log.StatusCode = httpStatusCode;
            log.RequestUri = request.RequestUri;

            TableOperation insertOperation = TableOperation.Insert(log);

            table.Execute(insertOperation);
        }

        private static void AddProxyResponseHeader(HttpResponseMessage response)
        {
            response.Headers.Via.Add(new ViaHeaderValue("1.1", "HttpProxy", "http"));
        }

        private void AddProxyRequestHeader(HttpRequestMessage request)
        {
            request.Headers.Add("X-Forwarded-For", request.GetClientIp());
            request.Headers.Add("user-agent", "HttpProxy");
        }
    }
}
