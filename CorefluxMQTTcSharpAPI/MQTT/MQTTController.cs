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

        public static event Action Disconnect;
        public static string ClientName { get; set; }
        public static bool PersistentConnection { get; set; }


        private static Dictionary<string, string> Data;
        private static Dictionary<string, int> DataQosLevel;
        private static MQTTClient MQTTClient;

        /// <summary>
        /// Initialize the global  MQTT client
        /// </summary>
        /// <param name="MQTT"></param>
        public static void Start(string IP, int port = 1883)
        {

            Random Random = new Random();
            int randInt = Random.Next();
            ClientName += "_" + randInt;

            MQTTClient = new MQTTClient(IPAddress.Parse(IP), port, false, null);
            if (Data == null)
            {
                Data = new Dictionary<string, string>();
                DataQosLevel = new Dictionary<string, int>();
            }

            MQTTClient.Connect(ClientName);
            // register to message received 
            MQTTClient.MQTTMsgPublishReceived += client_MQTTMsgPublishReceived;
            MQTTClient.MQTTMsgDisconnected += client_MQTTMsgDisconnected;
            if (PersistentConnection)
            {
                foreach (KeyValuePair<string, string> entry in Data)
                {
                    Subscribe(entry.Key, DataQosLevel[entry.Key]);
                }
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void client_MQTTMsgDisconnected(object sender, EventArgs e)
        {
            Disconnect?.Invoke();
            if (!PersistentConnection)
            {
                Data.Clear();
                DataQosLevel.Clear();
            }


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
        /// Publishes a retain topic payload
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="payload"></param>
        /// <param name="QosLevel"></param>
        /// <param name="Retain"></param>
        private static void Publish(string topic, string payload, int QosLevel = 0, bool Retain = false)
        {
            switch (QosLevel)
            {
                case 0:
                    MQTTClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(payload), MQTTMsgBase.QOS_LEVEL_AT_MOST_ONCE, Retain);
                    break;
                case 1:
                    MQTTClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(payload), MQTTMsgBase.QOS_LEVEL_AT_LEAST_ONCE, Retain);
                    break;
                case 2:
                    MQTTClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(payload), MQTTMsgBase.QOS_LEVEL_EXACTLY_ONCE, Retain);
                    break;
                default:
                    MQTTClient.Publish(topic, System.Text.Encoding.UTF8.GetBytes(payload), MQTTMsgBase.QOS_LEVEL_AT_MOST_ONCE, Retain);
                    break;
            }


        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="QosLevel"></param>
        private static void Subscribe(string topic, int QosLevel)
        {
            if (MQTTClient.IsConnected)
            {
                switch (QosLevel)
                {
                    case 0:
                        MQTTClient.Subscribe(new string[] { topic }, new byte[] { MQTTMsgBase.QOS_LEVEL_AT_MOST_ONCE });
                        break;
                    case 1:
                        MQTTClient.Subscribe(new string[] { topic }, new byte[] { MQTTMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                        break;
                    case 2:
                        MQTTClient.Subscribe(new string[] { topic }, new byte[] { MQTTMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                        break;
                    default:
                        MQTTClient.Subscribe(new string[] { topic }, new byte[] { MQTTMsgBase.QOS_LEVEL_AT_MOST_ONCE });
                        break;
                }


                AddTopicToData(topic, QosLevel);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="qoslevel"></param>
        private static void AddTopicToData(string topic, int qoslevel)
        {
            if (!Data.ContainsKey(topic))
            {
                Data.Add(topic, "");
            }
            if (!DataQosLevel.ContainsKey(topic))
            {
                DataQosLevel.Add(topic, qoslevel);
            }
            else
            {
                DataQosLevel[topic] = qoslevel;
            }
        }


        public static bool Connected()
        {
            return (MQTTClient.IsConnected);
        }
        public static void Stop()
        {
            MQTTClient.Disconnect();
        }
        public static void SetData(string topic, string payload, int qoslevel = 0, bool retain = false)
        {
            if (MQTTClient.IsConnected)
            {
                if (Data.ContainsKey(topic))
                {
                    Data[topic] = payload;
                    Publish(topic, payload, qoslevel, retain);

                }
                else
                {
                    //Subscribe(topic, qoslevel);
                    AddTopicToData(topic, qoslevel);
                    Data[topic] = payload;
                    Publish(topic, payload, qoslevel, retain);

                }
            }
        }
        public static string GetData(string topic, int qoslevel = 0)
        {
            if (MQTTClient.IsConnected)
            {
                if (Data.ContainsKey(topic))
                {
                    return Data[topic];
                }
                else
                {
                    Subscribe(topic, qoslevel);
                    return "";
                }
            }
            else
            {
                return "";
            }
        }
    }
}
