using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Text;
using System.Net;
using Random = System.Random;

namespace Coreflux.API.cSharp.Networking.MQTT
{
    public static class MQTTController
    {

        public static event Action OnConnect;
        public static event Action OnConnectFailed;
        public static event Action OnDisconnect;
        public static event Action<string, string> NewPayload;
        public static string ClientName { get; set; }
        public static bool PersistentConnection { get; set; }

        private static string Uri;
        private static int Portserver;
        private static string User;
        private static string Password;
        private static bool Secure;
        private static bool WithWS;

        private static Dictionary<string, string> Data;
        private static Dictionary<string, int> DataQosLevel;

        private static IManagedMqttClient _mqttClient;
        /// <summary>
        /// Initialize the global  MQTT client
        /// </summary>
        public static void Start(string IP, int port = 1883, string user = "", string password = "", bool mqttSecure = false, bool usingWebSocket = false)
        {

            Random Random = new Random();
            int randInt = Random.Next();
            ClientName += "_" + randInt;
            try
            {
                // MQTTClient = new MQTTClient(IPAddress.Parse(IP), port, false, null);
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

                var ConnectTask= ConnectAsync();
                ConnectTask.Wait(2000);

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
            ClientName += "_" + randInt;
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
        /// 
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
        private static void Publish(string topic, string payload, int QosLevel = 0, bool Retain = false)
        {
            if (Connected())
            {
                var pub = PublishAsync(topic, payload, Retain, QosLevel);
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
        private static async Task PublishAsy(string topic, string payload, int QosLevel = 0, bool Retain = false)
        {
            if (Connected())
            {
                await PublishAsync(topic, payload, Retain, QosLevel);

            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="QosLevel"></param>
        private static void Subscribe(string topic, int QosLevel)
        {
            if (Connected())
            {
                var sub=SubscribeAsync(topic, QosLevel);
                sub.Wait(50);
                AddTopicToData(topic, QosLevel);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="QosLevel"></param>
        private static async Task SubscribeAsy(string topic, int QosLevel)
        {
            if (Connected())
            {
                await SubscribeAsync(topic, QosLevel);

                AddTopicToData(topic, QosLevel);
            }
        }
        /// <summary>
        /// Subscribes Async the topic
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="qos"></param>
        /// <returns></returns>
        private static async Task SubscribeAsync(string topic, int qos = 1) => await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos).Build());
        /// <summary>
        /// Publish Message.
        /// </summary>
        /// <param name="topic">Topic.</param>
        /// <param name="payload">Payload.</param>
        /// <param name="retainFlag">Retain flag.</param>
        /// <param name="qos">Quality of Service.</param>
        /// <returns>Task.</returns>
        private static async Task PublishAsync(string topic, string payload, bool retainFlag = true, int qos = 1) => await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload).WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos).WithRetainFlag(retainFlag).Build());
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

            var managedOptions = new ManagedMqttClientOptionsBuilder()
              .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
              .WithClientOptions(options)
              .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();

            _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            _mqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(OnConnectingFailed);

            _mqttClient.UseApplicationMessageReceivedHandler(e => {

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
                        NewPayload?.Invoke(topic, payload);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message, ex);
                }
            });

            var teste = _mqttClient.StartAsync(managedOptions);
            teste.Wait(3000);
        }
        private static void OnConnected(MqttClientConnectedEventArgs obj)
        {
            if (OnConnect != null)
                OnConnect.Invoke();
        }
        private static void OnConnectingFailed(ManagedProcessFailedEventArgs obj)
        {
            if (OnConnectFailed != null)
                OnConnectFailed.Invoke();
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
        /// Stops the client and disconnects entirelly to  the broker
        /// </summary>
        public static void Stop()
        {
            _mqttClient.StopAsync();
        }
        /// <summary>
        /// Publishses the topics
        /// </summary>
        /// <param name="topic">The topic it is required to publish </param>
        /// <param name="payload">The payload required to publish </param>
        /// <param name="qoslevel">The level of quality of service 0,1,2</param>
        /// <param name="retain">If the topic will be retain or not on the broker True/False</param>
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
                    await PublishAsy(topic, payload, qoslevel, retain);

                }
                else
                {
                    //Subscribe(topic, qoslevel);
                    AddTopicToData(topic, qoslevel);
                    Data[topic] = payload;
                    await PublishAsy(topic, payload, qoslevel, retain);

                }
            }
        }
        /// <summary>
        /// Gets the topics we are subscribed and provides the last received values
        /// </summary>
        /// <param name="topic">The topic it is required to publish </param>
        /// <param name="qoslevel">The quality of service required 0,1,2</param>
        /// <returns>The last payload received in hte topic</returns>
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
        public static async Task<string> GetDataAsync(string topic,int qoslevel=0)
        {
            if (Connected())
            {
                if (Data.ContainsKey(topic))
                {
                    return Data[topic];
                }
                else
                {
                    await SubscribeAsy(topic, qoslevel);
                    return "";
                }
            }
            else
            {
                return "";
            }
        }
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