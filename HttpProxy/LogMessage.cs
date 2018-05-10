using Microsoft.Azure.CosmosDB.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;

namespace HttpProxy
{
    public class LogMessage : TableEntity
    {
        private const string MessagePartionKey = "LogEntry";
        private const string DateFormat = "yyyyMMdd ; HH:mm:ss:fffffff";
        private const string RowKeyFormat = "{0} - {1}";

        public string Method { get; set; }
        public byte[] HttpContent { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public Uri RequestUri { get; set; }


        public LogMessage()
        {
            PartitionKey = MessagePartionKey;
            string date = DateTime.Now.ToUniversalTime().ToString(DateFormat);
            RowKey = string.Format(RowKeyFormat, date, Guid.NewGuid().ToString());
        }

    }
}