using System.Collections;
using System.Collections.Generic;

using Coreflux.API.cSharp.Networking.MQTT;
using Coreflux.API.cSharp.Networking.MQTT.Messages;
using Coreflux.API.cSharp.Networking.MQTT.Utility;
using Coreflux.API.cSharp.Networking.MQTT.Exceptions;
using System;
using System.Net;
using Random = System.Random;
using System.Linq;

namespace Coreflux.API.cSharp.Networking.MQTT
{
    public class MQTTid {
        internal string brokerADDR;
        internal int brokerPort;
        public MQTTid(string brokerADDR, int brokerPort) {
            this.brokerADDR = brokerADDR;
            this.brokerPort = brokerPort;
        }

    }
    internal class mqttClientData {
        public MqttClient client;
        private Dictionary<string,MQTTMsgPublishEventArgs> myTopics;
        private object myTopicsLock;
        public event Action<string, MQTTMsgPublishEventArgs> onNewDataAction;
        public event Action<string, bool, System.Diagnostics.EventLogEntryType> logMessage;
        public mqttClientData(MqttClient client, Action<string, MQTTMsgPublishEventArgs> onNewMessage,
            Action<string, bool, System.Diagnostics.EventLogEntryType> logMessage) {
            this.myTopicsLock = new object();
            this.myTopics = new Dictionary<string, MQTTMsgPublishEventArgs>();
            this.client = client;
            this.onNewDataAction = onNewMessage;
            this.logMessage = logMessage;
        }
        public string getData(string topic) {
            string returnData = "";
            lock (this.myTopicsLock) {
                if (this.myTopics.ContainsKey(topic) && this.myTopics[topic]!=null) {
                    returnData = this.myTopics[topic].getMessage();

                }
            }
            return returnData;
        }

        public void addRecivedData(string topic, MQTTMsgPublishEventArgs message=null) {
            lock (this.myTopicsLock) {
                if (this.myTopics.ContainsKey(topic)) {
                    if (message != null) {
                        this.myTopics[topic] = message;

                    }

                } else {
                    this.myTopics.Add(topic, message);
                }
            }
        }


        public void addRecivedData(string topic, MQTTMsgPublishEventArgs message,MqttClient client) {
            lock (this.myTopicsLock) {
                if (this.client.Equals(client) && this.myTopics.ContainsKey(topic)) {
                    this.myTopics[topic] = message;

                } else {
                    this.myTopics.Add(topic, message);
                }
            }
            this.onNewDataAction?.Invoke(topic, message);
        }
        public bool subscribedTo(string topic) {
            bool returnData = false;
            lock (this.myTopicsLock) {
                returnData = this.myTopics.ContainsKey(topic);
            }
            return returnData;
        }

        public List<string> getsubscribedTopics() {
            List<string> returnData = new List<string>();
            lock (this.myTopicsLock) {
                foreach(string topic in this.myTopics.Keys) {
                    returnData.Add(topic);
                }
            }
            return returnData;
        }
        public void LogMessage(string message, bool onlyConsole, System.Diagnostics.EventLogEntryType type, MqttClient sender) {
            if (sender.Equals(this.client)) {
                this.logMessage?.Invoke(message, onlyConsole, type);
            }
        }
    }
    public static class MQTTController
    {

        private static Dictionary<string, List<mqttClientData>> clientsActiveConnections=new Dictionary<string, List<mqttClientData>>();
        private static Dictionary<string, MqttClient> idsToClients = new Dictionary<string, MqttClient>();
        //private static Dictionary<string, Tuple<MqttClient, Action<string, MQTTMsgPublishEventArgs>, Dictionary<string, MQTTMsgPublishEventArgs>>> clientsIDStuff;
        private static object clientStuffLock = new object();
        private static object brokerStuffLock = new object();
        //private static event Action<string, MQTTMsgPublishEventArgs, MqttClient> onMQTTMessage;


        static MQTTController() {
            /*MQTTController.clientsActiveConnections = new Dictionary<string, List<mqttClientData>>();
            MQTTController.idsToClients = new Dictionary<string, MqttClient>();
            MQTTController.clientStuffLock = new object();
            MQTTController.brokerStuffLock = new object();*/
            //MQTTController.onMQTTMessage += MQTTController.Client_MQTTMsgPublishReceived;

        }

        private static string getClientIdInternal() {
            //Random random = new Random(DateTime.Now.Hour * 60 * 60 + DateTime.Now.Minute * 60 + DateTime.Now.Second);
            //const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
            //return "OLAMUNDO";//System.Reflection.Assembly.GetEntryAssembly().Location.Replace(System.IO.Path.PathSeparator.ToString(), "");
        }

        private static string randomClientIDbroker(int length) {
            Random random = new Random(DateTime.Now.Hour * 60 * 60 + DateTime.Now.Minute * 60 + DateTime.Now.Second);
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return ("COREFLUXMQTTAPI_"+ new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray()));
        }

        private static  void Client_MQTTMsgPublishReceived(string topic,MQTTMsgPublishEventArgs message, MqttClient client) {
            Dictionary<string, mqttClientData> everyBodyUsisngBroker = null;
            lock (MQTTController.brokerStuffLock) {
                everyBodyUsisngBroker = MQTTController.getWhoisUsing(client.remoteAddress, client.remotePort);
            }
            if(everyBodyUsisngBroker!=null && everyBodyUsisngBroker.Count > 0) {
                System.Threading.Tasks.Parallel.ForEach(everyBodyUsisngBroker.Values.ToList(), (clientUsingBroker) =>
                {
                    clientUsingBroker.addRecivedData(topic, message, client);
                });
            }
        }

        private static void logMessage(string message, bool onlyConsole, System.Diagnostics.EventLogEntryType type, MqttClient sender) {
            List<mqttClientData> everyBodyUsisngBroker = new List<mqttClientData>();
            lock (MQTTController.clientStuffLock) {
                foreach(string clientID in MQTTController.clientsActiveConnections.Keys) {
                    everyBodyUsisngBroker.AddRange(MQTTController.clientsActiveConnections[clientID].FindAll(mqqtCData => mqqtCData.client.Equals(sender)));
                }
            }
            if (everyBodyUsisngBroker != null && everyBodyUsisngBroker.Count > 0) {
                System.Threading.Tasks.Parallel.ForEach(everyBodyUsisngBroker, (clientUsingBroker) => {
                    clientUsingBroker.LogMessage(message, onlyConsole, type, sender);
                });
            }


        }

        private static string randomClientIDbroker(string clientID) {
            return MQTTController.randomClientIDbroker(15);
        }

        private static MqttClient getConnectionTORemoteBroker(string addr, int port) {
            MqttClient result = null;
            lock (MQTTController.brokerStuffLock) {
                string clientKEY = null;
                try {
                    clientKEY = MQTTController.idsToClients.First(x => x.Value.remoteAddress.Equals(addr) && x.Value.remotePort == port).Key;

                }catch(Exception e) {

                }
                if (string.IsNullOrEmpty(clientKEY)) {//Not Exist
                    clientKEY = MQTTController.randomClientIDbroker(15);
                    result = new MqttClient(addr, port, 0, clientKEY, false, null, null, false, false, 0, true);
                    result.logAction = MQTTController.logMessage;
                    result.onMessageRecievedToSyncronize += MQTTController.Client_MQTTMsgPublishReceived;
                    MQTTController.idsToClients.Add(clientKEY, result);
                } else {
                    result = MQTTController.idsToClients[clientKEY];
                }


            }
            result.connectToRemote();
            return result;
        }

        private static Dictionary<string, mqttClientData> getWhoisUsing(string remoteAddr, int remotePort) {
            Dictionary<string, mqttClientData> returnData = new Dictionary<string, mqttClientData>();
            foreach (KeyValuePair<string, List<mqttClientData>> connectedClient in MQTTController.clientsActiveConnections) {
                mqttClientData usingBroker = connectedClient.Value.Find(clientData => clientData.client.remoteAddress.Equals(remoteAddr) && clientData.client.remotePort == remotePort);
                if (usingBroker != null) {
                    returnData.Add(connectedClient.Key, usingBroker);
                }
            }
            return returnData;
        }

        private static void UnSubscribe(MqttClient remoteBrokerClient, string topic, int maxBeforRemove, Dictionary<string, mqttClientData> everyBodyUsisngBroker) {
            int actualCount = 0;
            foreach (mqttClientData cData in everyBodyUsisngBroker.Values) {
                if (cData.subscribedTo(topic)) {
                    actualCount++;
                }
                if (actualCount > maxBeforRemove) {
                    break;
                }
            }
            if (actualCount <= maxBeforRemove) {
                remoteBrokerClient.unsubscribe(topic);
            }
        }

        public static MQTTid Init(string remoteAddr, int remotePort,
            /*Action<string,bool,System.Diagnostics.EventLogEntryType> logMessage,*/
            Action<string,MQTTMsgPublishEventArgs> onNewMessage,
            Action<string,bool, System.Diagnostics.EventLogEntryType> logMessage
            ) {

            //First get client to remote if not exists new is created
            MqttClient remoteClient = MQTTController.getConnectionTORemoteBroker(remoteAddr, remotePort);
            string connectedSoftwareId = MQTTController.getClientIdInternal();
            //Assign This client to this remote software
            lock (MQTTController.clientStuffLock) {
                if (MQTTController.clientsActiveConnections.ContainsKey(connectedSoftwareId)) { //Not First Connection
                    mqttClientData onMememory =  MQTTController.clientsActiveConnections[connectedSoftwareId].First(cdata => cdata.client.Equals(remoteClient));
                    if(onMememory == null) {// Not Connected to this broker
                        MQTTController.clientsActiveConnections[connectedSoftwareId].Add(new mqttClientData(remoteClient, onNewMessage, logMessage));
                    }
                } else { //FirstConnection
                    MQTTController.clientsActiveConnections.Add(connectedSoftwareId, new List<mqttClientData>());
                    MQTTController.clientsActiveConnections[connectedSoftwareId].Add(new mqttClientData(remoteClient, onNewMessage, logMessage));
                }
            }
            return new MQTTid(remoteAddr, remotePort);
        }


        public static int Disconnect(MQTTid brokerID) {
            if (brokerID == null) {
                return -1;
            }
            string connectedSoftwareId = MQTTController.getClientIdInternal();
            lock (MQTTController.clientStuffLock) {
                if (MQTTController.clientsActiveConnections.ContainsKey(connectedSoftwareId)) { //Connected to somewhere
                    mqttClientData onMemory = MQTTController.clientsActiveConnections[connectedSoftwareId].First(cdata => cdata.client.remoteAddress.Equals(brokerID.brokerADDR) && cdata.client.remotePort== brokerID.brokerPort);
                    if (onMemory != null) {//  Connected to this broker
                        MQTTController.clientsActiveConnections[connectedSoftwareId].Remove(onMemory); // remove this cleint reference to the broker
                        
                        Dictionary<string, mqttClientData> everyBodyUsisngBroker = MQTTController.getWhoisUsing(brokerID.brokerADDR, brokerID.brokerPort);
                        //List<mqttClientData> everyBodyUsisngBroker= MQTTController.clientsActiveConnections[connectedSoftwareId].FindAll(cdata => cdata.client.remoteAddress.Equals(remoteAddr) && cdata.client.remotePort == remotePort);
                        //Disconect if nobody is using this broker 
                        if (everyBodyUsisngBroker==null || everyBodyUsisngBroker.Count == 0) {
                            onMemory.client.disconnectToRemoteAsync();
                            lock (MQTTController.brokerStuffLock) {
                                string remoteBrokerID = MQTTController.idsToClients.First(x => x.Value.Equals(onMemory.client)).Key;
                                MQTTController.idsToClients.Remove(remoteBrokerID);
                            }
                        } else { //somebodyelse is using thes connection 
                            //remove all the subscriptions from this client if nobody is using them
                            foreach (string subscriptionToBroker in onMemory.getsubscribedTopics()) {
                                MQTTController.UnSubscribe(onMemory.client, subscriptionToBroker, 0, everyBodyUsisngBroker);
                            }
                        }  
                    }
                } else { //notConnected
                   //Nothing
                }
            }
            return 0;
           
        }

        public static int UnSubscribe(MQTTid brokerID, string topic) {
            if (brokerID == null) {
                return -1;
            }
            string connectedSoftwareId = MQTTController.getClientIdInternal();
            Dictionary<string, mqttClientData> everyBodyUsisngBroker = MQTTController.getWhoisUsing(brokerID.brokerADDR, brokerID.brokerPort);
            lock (MQTTController.clientStuffLock) {
                if (MQTTController.clientsActiveConnections.ContainsKey(connectedSoftwareId) ) { //Connected to somewhere
                    mqttClientData onMemory = MQTTController.clientsActiveConnections[connectedSoftwareId].First(cdata => cdata.client.remoteAddress.Equals(brokerID.brokerADDR) && cdata.client.remotePort == brokerID.brokerPort);
                    if (onMemory != null) {
                        MQTTController.UnSubscribe(onMemory.client, topic, 1, everyBodyUsisngBroker);

                    }

                }
            }
            return 0;
        }

        public static int Publish(MQTTid brokerID, string topic, string data, byte QOSLevel,bool retain) {
            if (brokerID == null) {
                return -1;
            }
            mqttClientData onMemory = null;
            string connectedSoftwareId = MQTTController.getClientIdInternal();
            lock (MQTTController.clientStuffLock) {
                if (MQTTController.clientsActiveConnections.ContainsKey(connectedSoftwareId)) { //Connected to somewhere
                    onMemory = MQTTController.clientsActiveConnections[connectedSoftwareId].First(cdata => cdata.client.remoteAddress.Equals(brokerID.brokerADDR) && cdata.client.remotePort == brokerID.brokerPort);
                }
            }
            if (onMemory != null) {//I'm connected to this broker
                return onMemory.client.publishOnRemote(topic, data, QOSLevel, retain);
            }
            return -2;
        }

        public static int Subscribe(MQTTid brokerID, string topic, byte QOSLevel) {
            if (brokerID == null) {
                return -1;
            }
            mqttClientData onMemory = null;
            string connectedSoftwareId = MQTTController.getClientIdInternal();
            lock (MQTTController.clientStuffLock) {
                if (MQTTController.clientsActiveConnections.ContainsKey(connectedSoftwareId)) { //Connected to somewhere
                    onMemory = MQTTController.clientsActiveConnections[connectedSoftwareId].First(cdata => cdata.client.remoteAddress.Equals(brokerID.brokerADDR) && cdata.client.remotePort == brokerID.brokerPort);
                }
            }
            if (onMemory != null) {//I'm connected to this broker
                onMemory.client.subscribe(topic, QOSLevel);
                onMemory.addRecivedData(topic);
                return 0;
            }
            return -2;
        }

        public static bool Connected(MQTTid brokerID) {
            if (brokerID == null) {
                throw new ArgumentNullException("brokerID cannot be null");
            }
            mqttClientData onMemory = null;
            string connectedSoftwareId = MQTTController.getClientIdInternal();
            lock (MQTTController.clientStuffLock) {
                if (MQTTController.clientsActiveConnections.ContainsKey(connectedSoftwareId)) { //Connected to somewhere
                    onMemory = MQTTController.clientsActiveConnections[connectedSoftwareId].First(cdata => cdata.client.remoteAddress.Equals(brokerID.brokerADDR) && cdata.client.remotePort == brokerID.brokerPort);
                }
            }
            if (onMemory == null) {//I'm connected to this broker
                return false;
            }
            return onMemory.client.connected();
        }

        public static string GetData(MQTTid brokerID, string topic,byte QOSLevel) {
            if (brokerID == null) {
                throw new ArgumentNullException("brokerID cannot be null");
            }
            MQTTController.Subscribe(brokerID, topic, QOSLevel);
            mqttClientData onMemory = null;
            string connectedSoftwareId = MQTTController.getClientIdInternal();
            lock (MQTTController.clientStuffLock) {
                if (MQTTController.clientsActiveConnections.ContainsKey(connectedSoftwareId)) { //Connected to somewhere
                    onMemory = MQTTController.clientsActiveConnections[connectedSoftwareId].First(cdata => cdata.client.remoteAddress.Equals(brokerID.brokerADDR) && cdata.client.remotePort == brokerID.brokerPort);
                }
            }
            if (onMemory != null) {//I'm connected to this broker
                return onMemory.getData(topic);

            }
            return null;
        }


    }


    public interface ICorefluxMQTTClient {
        void Init(string remoteAddr, int remotePort, Action<string, MQTTMsgPublishEventArgs> onNewMessage, 
            Action<string, bool, System.Diagnostics.EventLogEntryType> logMessage);
        int Disconnect();
        int UnSubscribe(string topic);
        int Subscribe(string topic, byte QOSLevel);
        int Publish(string topic, string data, byte QOSLevel, bool retain);
        bool Connected();
        string GetData(string topic, byte QOSLevel);
    }

    public class CorefluxMQTTClient : ICorefluxMQTTClient{
        private MQTTid myID;

        public CorefluxMQTTClient() {
            
        }

        bool ICorefluxMQTTClient.Connected() {
            return MQTTController.Connected(this.myID);
        }

        int ICorefluxMQTTClient.Disconnect() {
            return MQTTController.Disconnect(this.myID);
        }

        string ICorefluxMQTTClient.GetData(string topic, byte QOSLevel) {
            return  MQTTController.GetData(this.myID,topic,QOSLevel);
        }

        void ICorefluxMQTTClient.Init(string remoteAddr, int remotePort, Action<string, MQTTMsgPublishEventArgs> onNewMessage,
            Action<string, bool, System.Diagnostics.EventLogEntryType> logMessage) {
            this.myID = MQTTController.Init(remoteAddr,remotePort,onNewMessage, logMessage);
        }

        int ICorefluxMQTTClient.Publish(string topic, string data, byte QOSLevel, bool retain) {
            return MQTTController.Publish(this.myID, topic, data,QOSLevel,retain);
        }

        int ICorefluxMQTTClient.Subscribe(string topic, byte QOSLevel) {
            return MQTTController.Subscribe(this.myID, topic, QOSLevel);
        }

        int ICorefluxMQTTClient.UnSubscribe(string topic) {
            return MQTTController.UnSubscribe(this.myID, topic);
        }
    }

}
