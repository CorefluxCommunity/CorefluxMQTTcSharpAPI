using System.Collections;
using System.Collections.Generic;

using Coreflux.API.cSharp.Networking.MQTT;
using Coreflux.API.cSharp.Networking.MQTT.Messages;
using Coreflux.API.cSharp.Networking.MQTT.Utility;
using Coreflux.API.cSharp.Networking.MQTT.Exceptions;
using System;
using System.Net;
using Random = System.Random;

namespace Coreflux.API.cSharp.Networking.MQTT
{
    public static class MQTTController
    {
        public static Dictionary<string, string> Data;

        private static MQTTClient MQTTClient;
        public static string ClientName;
        /// <summary>
        /// Initialize the global  MQTT client
        /// </summary>
        /// <param name="MQTT"></param>
        public static void Initialize(string IP, int port=1883)
        {
       

            Random Random = new Random();
            int randInt = Random.Next();
            ClientName += "_" + randInt;

            MQTTClient = new MQTTClient(IPAddress.Parse(IP), port, false, null);
            Data = new Dictionary<string, string>();
            MQTTClient.Connect(ClientName);
            // register to message received 
            MQTTClient.MQTTMsgPublishReceived += client_MQTTMsgPublishReceived;
            MQTTClient.MQTTMsgDisconnected += client_MQTTMsgDisconnected;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void client_MQTTMsgDisconnected(object sender, EventArgs e)
        {
         

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void client_MQTTMsgPublishReceived(object sender, MQTTMsgPublishEventArgs e)
        {
            if (Data.ContainsKey(e.Topic))
            {
                Data[e.Topic] = System.Text.Encoding.UTF8.GetString(e.Message);
            }
        }

        /// <summary>
        /// Publishes a topic with the current  payload
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="payload"></param>
        public static void Publish(string topic, string message)
        {

            MQTTClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message), MQTTMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);

        }
        /// <summary>
        /// Publishes a retain topic payload
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="payload"></param>
        public static void PublishRetain(string topic, string message)
        {

            MQTTClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message), MQTTMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        public static void Subscribe(string topic)
        {
            if (MQTTClient.IsConnected)
            {
                MQTTClient.Subscribe(new string[] { topic }, new byte[] { MQTTMsgBase.QOS_LEVEL_EXACTLY_ONCE });

                AddTopicToData(topic);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        private static void AddTopicToData(string topic)
        {
            if (!Data.ContainsKey(topic))
            {
                Data.Add(topic, "");
            }
        }

        public static string GetData(string topic)
        {
            if (Data.ContainsKey(topic))
            {
                return Data[topic];
            }
            else
            {
                Subscribe(topic);
                return "";
            }
        }
    }
}
