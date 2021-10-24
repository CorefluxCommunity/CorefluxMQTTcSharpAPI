﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client;
using System;
using System.Text;
using System.Net;
using Random = System.Random;

namespace Coreflux.API.Networking.MQTT
{
    /// <summary>
    /// A managed MQTT client (version 3.1 / 3.11 / 5 )
    /// </summary>
    public static class MQTTController
    {
        /// <summary>
        /// Event Triggered if connected to Broker
        /// </summary>
        public static event Action OnConnect;
        /// <summary>
        /// Event Triggered if disconnected from Broker
        /// </summary>
        public static event Action OnDisconnect;
        /// <summary>
        /// Use GetDataAsync to map a subscription in order to trigger a payload . Replies using an MQTTNewPayload
        /// </summary>
        public static event Action<MQTTNewPayload> NewPayload;
        /// <summary>
        /// The client Name for the 
        /// </summary>
        public static string ClientName { get; set; }
        /// <summary>
        /// PresistentCOnnection remembers the topics , QOS and last data even if disconnect. When a Start is run again it uses the old Data.
        /// </summary>
        public static bool PersistentConnection { get; set; }

        private static string Uri;
        private static int Portserver;
        private static string User;
        private static string Password;
        private static bool Secure;
        private static bool WithWS;

        private static Dictionary<string, string> Data;
        private static Dictionary<string, int> DataQosLevel;

        private static IMqttClient _mqttClient;
        /// <summary>
        /// Initialize the global  MQTT client
        /// </summary>
        [Obsolete("Start is deprecated, please use StartAsync instead.")]
        public static void Start(string IP, int port = 1883, string user = "", string password = "", bool mqttSecure = false, bool usingWebSocket = false)
        {

            Random Random = new Random();
            int randInt = Random.Next();
            //       ClientName += "_" + randInt;

            try
            {

                if (Data == null)
                {
                    Data = new Dictionary<string, string>();
                    DataQosLevel = new Dictionary<string, int>();
                }
                Uri = IP;
                Portserver = port;
                User = user;
                Password = password;
                Secure = mqttSecure;
                WithWS = usingWebSocket;

                var ConnectTask = ConnectAsync();


                if (PersistentConnection)
                {
                    foreach (KeyValuePair<string, string> entry in Data)
                    {
                        Subscribe(entry.Key, DataQosLevel[entry.Key]);
                    }
                }
            }
            catch
            {
                throw new ConnectionFailBrokerException(IP);
            }
        }
        /// <summary>
        /// Initialize the global  MQTT client asyncrounsly
        /// </summary>
        public static async Task StartAsync(string IP, int port = 1883, string user = "", string password = "", bool mqttSecure = false, bool usingWebSocket = false)
        {

            Random Random = new Random();
            int randInt = Random.Next();
            //     ClientName += "_" + randInt;
            try
            {

                if (Data == null)
                {
                    Data = new Dictionary<string, string>();
                    DataQosLevel = new Dictionary<string, int>();
                }
                Uri = IP;
                Portserver = port;
                User = user;
                Password = password;
                Secure = mqttSecure;
                WithWS = usingWebSocket;




                if (PersistentConnection)
                {
                    foreach (KeyValuePair<string, string> entry in Data)
                    {
                        Subscribe(entry.Key, DataQosLevel[entry.Key]);
                    }
                }
                await ConnectAsync();
            }
            catch
            {
                throw new ConnectionFailBrokerException(IP);
            }

        }
        /// <summary>
        /// Reaction to disconnection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void client_MQTTMsgDisconnected()
        {
            if (!PersistentConnection)
            {
                Data.Clear();
                DataQosLevel.Clear();
            }
        }
        /// <summary>
        /// Publishes a retain topic payload
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="payload"></param>
        /// <param name="QosLevel"></param>
        /// <param name="Retain"></param>
        [Obsolete("Publish is deprecated, please use PublishAsync instead.")]
        private static void Publish(string topic, string payload, int QosLevel = 0, bool Retain = false)
        {
            if (Connected())
            {
                var pub = PublishA(topic, payload, Retain, QosLevel);
                pub.Wait(1);
            }
        }
        /// <summary>
        /// Publishes a retain topic payload async
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="payload"></param>
        /// <param name="QosLevel"></param>
        /// <param name="Retain"></param>
        private static async Task PublishAsync(string topic, string payload, int QosLevel = 0, bool Retain = false)
        {
            if (Connected())
            {
                var t = await PublishA(topic, payload, Retain, QosLevel);

            }
        }
        /// <summary>
        /// Subscribes to a topic
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="QosLevel"></param>
        [Obsolete("Subscribe is deprecated, please use SubscribeAsync instead.")]
        private static void Subscribe(string topic, int QosLevel)
        {
            if (Connected())
            {
                var sub = SubscribeA(topic, QosLevel);
                sub.Wait(50);
                AddTopicToData(topic, QosLevel);
            }
        }
        /// <summary>
        /// Subscribes Asyncronsly to a certain topic
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="QosLevel"></param>
        private static async Task SubscribeAsync(string topic, int QosLevel)
        {
            if (Connected())
            {
                await SubscribeA(topic, QosLevel);

                AddTopicToData(topic, QosLevel);
            }
        }
        /// <summary>
        /// Subscribes Async the topic
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="qos"></param>
        /// <returns></returns>
        private static async Task SubscribeA(string topic, int qos = 1) => await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos).Build());
        /// <summary>
        /// Publish Message.
        /// </summary>
        /// <param name="topic">Topic.</param>
        /// <param name="payload">Payload.</param>
        /// <param name="retainFlag">Retain flag.</param>
        /// <param name="qos">Quality of Service.</param>
        /// <returns>Task.</returns>
        private static async Task<MQTTnet.Client.Publishing.MqttClientPublishResult> PublishA(string topic, string payload, bool retainFlag = true, int qos = 1)
        {
            return await _mqttClient.PublishAsync(topic, payload, (MQTTnet.Protocol.MqttQualityOfServiceLevel)qos, retainFlag);
        }
        /// <summary>
        /// Adds topics to data Array
        /// </summary>
        /// <param name="topic">topic </param>
        /// <param name="qoslevel">level of Qauality of service</param>
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
        /// <summary>
        /// Connect to broker aysnc and construct the handled client
        /// </summary>
        /// <returns>Task.</returns>
        private static async Task ConnectAsync()
        {
            string clientId = ClientName;
            string mqttURI = Uri;
            string mqttUser = User;
            string mqttPassword = Password;
            int mqttPort = Portserver;
            bool mqttSecure = Secure;
            bool websocket = WithWS;

            var messageBuilder = new MqttClientOptionsBuilder()
              .WithClientId(clientId)
              .WithCredentials(mqttUser, mqttPassword)


              .WithCleanSession();
            if (WithWS)
            {
                messageBuilder.WithWebSocketServer(mqttURI);
            }
            else
            {
                messageBuilder.WithTcpServer(mqttURI, mqttPort).WithCleanSession();
            }

            var options = mqttSecure
              ? messageBuilder
                .WithTls()
                .Build()
              : messageBuilder
                .Build();



            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();
            _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            //   _mqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(OnConnectingFailed);

            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {

                try
                {
                    string topic = e.ApplicationMessage.Topic;

                    if (string.IsNullOrWhiteSpace(topic) == false)
                    {
                        string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        if (Data.ContainsKey(topic))
                        {
                            Data[topic] = payload;
                        }
                        MQTTNewPayload t = new MQTTNewPayload();
                        t.topic = topic;
                        t.payload = payload;
                        NewPayload?.Invoke(t);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message, ex);
                }
            });

            await _mqttClient.ConnectAsync(options);

        }
        private static void OnConnected(MqttClientConnectedEventArgs obj)
        {
            if (OnConnect != null)
                OnConnect.Invoke();
        }
        private static void OnDisconnected(MqttClientDisconnectedEventArgs obj)
        {
            if (OnDisconnect != null)
                OnDisconnect.Invoke();
            client_MQTTMsgDisconnected();
        }
        /// <summary>
        /// Verifies the connection to broker
        /// </summary>
        /// <returns>True if connected to broker</returns>
        public static bool Connected()
        {
            return _mqttClient.IsConnected;
        }
        /// <summary>
        /// Stops the managed client and disconnects entirelly to  the broker
        /// </summary>
        public static async Task StopAsync()
        {
            await _mqttClient.DisconnectAsync();
        }
        /// <summary>
        /// Publishses the topics 
        /// </summary>
        /// <param name="topic">The topic it is required to publish </param>
        /// <param name="payload">The payload required to publish </param>
        /// <param name="qoslevel">The level of quality of service 0,1,2</param>
        /// <param name="retain">If the topic will be retain or not on the broker True/False</param>
        [Obsolete("SetData is deprecated, please use SetDataAsync instead.")]
        public static void SetData(string topic, string payload, int qoslevel = 0, bool retain = false)
        {
            if (Connected())
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
        /// <summary>
        /// Publishses the topics  async
        /// </summary>
        /// <param name="topic">The topic it is required to publish </param>
        /// <param name="payload">The payload required to publish </param>
        /// <param name="qoslevel">The level of quality of service 0,1,2</param>
        /// <param name="retain">If the topic will be retain or not on the broker True/False</param>
        public static async Task SetDataAsync(string topic, string payload, int qoslevel = 0, bool retain = false)
        {
            if (Connected())
            {
                if (Data.ContainsKey(topic))
                {
                    Data[topic] = payload;
                    await PublishAsync(topic, payload, qoslevel, retain);

                }
                else
                {
                    //Subscribe(topic, qoslevel);
                    AddTopicToData(topic, qoslevel);
                    Data[topic] = payload;
                    await PublishAsync(topic, payload, qoslevel, retain);

                }
            }
        }
        /// <summary>
        /// Gets the topics we are subscribed and provides the last received values
        /// </summary>
        /// <param name="topic">The topic it is required to publish </param>
        /// <param name="qoslevel">The quality of service required 0,1,2</param>
        /// <returns>The last payload received in hte topic</returns>
        [Obsolete("GetData is deprecated, please use GetDataAsync instead.")]
        public static string GetData(string topic, int qoslevel = 0)
        {
            if (Connected())
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
        /// <summary>
        /// Gets topic data Async mode
        /// </summary>
        /// <param name="topic">The topic it is required to publish </param>
        /// <param name="qoslevel">The quality of service required 0,1,2</param>
        /// <returns>The last payload received in hte topic</returns>
        public static async Task<string> GetDataAsync(string topic, int qoslevel = 0)
        {
            if (Connected())
            {
                if (Data.ContainsKey(topic))
                {
                    return Data[topic];
                }
                else
                {
                    await SubscribeAsync(topic, qoslevel);
                    return "";
                }
            }
            else
            {
                return "";
            }
        }
    }

    /// <summary>
    /// Reply from subscription from NewPayload event 
    /// </summary>
    public class MQTTNewPayload
    {
        /// <summary>
        /// The topic of the NewPayload received
        /// </summary>
        public string topic;
        /// <summary>
        /// The payload received 
        /// </summary>
        public string payload;
    }
    [Serializable]
    class ConnectionFailBrokerException : Exception
    {
        public ConnectionFailBrokerException()
        {

        }

        public ConnectionFailBrokerException(string ip)
            : base(String.Format("Failed To connect: {0}", ip))
        {

        }

    }
}