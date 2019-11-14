using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QueueClient = Microsoft.ServiceBus.Messaging.QueueClient;

namespace WorkerRole1
{
    public class BusQueue
    {
        string sbConnectionString = "Endpoint=sb://automatedcrowlerbus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=Egyu5tXwpc4yo70YXWC5bL0I2bYhbEdI+tUq/ZdOmJg=";
        string sbQueueName = "automatedfilequeue";
        public BusQueue() { }
        public CrawlerDetails GetMessage()
        {
            var queueClient = QueueClient.CreateFromConnectionString(sbConnectionString, sbQueueName);
            BrokeredMessage message = queueClient.Receive();
            if (message == null)
            {
                CrawlerDetails dedMesages = new BusQueue().GetDeadLetterMessages();
                if (dedMesages == null)
                {
                    return null;
                }
            }
            var dataStram = new StreamReader(message.GetBody<Stream>()).ReadToEnd();
            var jArray = JsonConvert.DeserializeObject<List<CrawlerDetails>>(dataStram);
            var jObject = jArray.First();
            message.Complete();
            message.Abandon();
            return jObject;
        }
        public CrawlerDetails GetDeadLetterMessages()
        {
            var client = QueueClient.CreateFromConnectionString(sbConnectionString, sbQueueName);
            // this is for dead letter queue
            var deadLetterClient = QueueClient.CreateFromConnectionString(sbConnectionString, QueueClient.FormatDeadLetterPath(client.Path), ReceiveMode.ReceiveAndDelete);

            BrokeredMessage receivedDeadLetterMessage;
            if ((receivedDeadLetterMessage = deadLetterClient.Receive()) != null)
            {
                return null;
            }
            var dataStram = new StreamReader(receivedDeadLetterMessage.GetBody<Stream>()).ReadToEnd();
            var jArray = JsonConvert.DeserializeObject<List<CrawlerDetails>>(dataStram);
            var jObject = jArray.First();
            Trace.TraceInformation(jArray.ToString());
            Trace.TraceInformation(jObject.ToString());
            return jObject;
        }
    }
}
