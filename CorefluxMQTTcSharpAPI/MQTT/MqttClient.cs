/*
MQTT Project - MQTT Client Library for .Net and GnatMQ MQTT Broker for .NET
Copyright (c) 2014, Paolo Patierno, All rights reserved.

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 3.0 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library.
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Coreflux.API.cSharp.Networking.MQTT.Exceptions;
using Coreflux.API.cSharp.Networking.MQTT.Messages;
using Coreflux.API.cSharp.Networking.MQTT.Utility;
// if .Net Micro Framework

using System.Collections.Generic;

using System.Security.Authentication;
using System.Net.Security;


using System.Security.Cryptography.X509Certificates;
using System.Collections;

// alias needed due to Microsoft.SPOT.Trace in .Net Micro Framework
// (it's ambiguos with Coreflux.API.cSharp.Networking.MQTT.Utility.Trace)
using MQTTUtility = Coreflux.API.cSharp.Networking.MQTT.Utility;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace Coreflux.API.cSharp.Networking.MQTT
{

    public class ListQueue : List<object>
    {
        new public void Add(object item) { throw new NotSupportedException(); }
        new public void AddRange(IEnumerable<object> collection) { throw new NotSupportedException(); }
        new public void Insert(int index, object item) { throw new NotSupportedException(); }
        new public void InsertRange(int index, IEnumerable<object> collection) { throw new NotSupportedException(); }
        new public void Reverse() { throw new NotSupportedException(); }
        new public void Reverse(int index, int count) { throw new NotSupportedException(); }
        new public void Sort() { throw new NotSupportedException(); }
        new public void Sort(Comparison<object> comparison) { throw new NotSupportedException(); }
        new public void Sort(IComparer<object> comparer) { throw new NotSupportedException(); }
        new public void Sort(int index, int count, IComparer<object> comparer) { throw new NotSupportedException(); }

        public void Enqueue(object item)
        {
            base.Add(item);
        }

        public object Dequeue()
        {
            var t = base[0];
            base.RemoveAt(0);
            return t;
        }

        public object Peek()
        {
            return base[0];
        }
        public object Get(Predicate<object> match)
        {
            var ret= base.Find(match);
            return ret;

        }

    }
    /// <summary>
    /// MQTT Client
    /// </summary>
    internal class MQTTClient
    {


        /// <summary>
        /// Delagate that defines event handler for PUBLISH message received
        /// </summary>
        public delegate void MQTTMsgPublishEventHandler(object sender, MQTTMsgPublishEventArgs e);

        /// <summary>
        /// Delegate that defines event handler for published message
        /// </summary>
        public delegate void MQTTMsgPublishedEventHandler(object sender, MQTTMsgPublishedEventArgs e);

        /// <summary>
        /// Delagate that defines event handler for subscribed topic
        /// </summary>
        public delegate void MQTTMsgSubscribedEventHandler(object sender, MQTTMsgSubscribedEventArgs e);

        /// <summary>
        /// Delagate that defines event handler for unsubscribed topic
        /// </summary>
        public delegate void MQTTMsgUnsubscribedEventHandler(object sender, MQTTMsgUnsubscribedEventArgs e);


        /// <summary>
        /// Delegate that defines event handler for client disconnection (DISCONNECT message or not)
        /// </summary>
        public delegate void MQTTMsgDisconnectEventHandler(object sender, EventArgs e);       

        // CA certificate
        private X509Certificate caCert;

        // broker hostname, ip address and port
        private string brokerHostName;
        private IPAddress brokerIpAddress;
        private int brokerPort;
        // using SSL
        private bool secure;

        // thread for receiving incoming message
        private Thread receiveThread;
        // thread for raising received message event
        private Thread receiveEventThread;
        private bool isRunning;
        // event for raising received message event
        private AutoResetEvent receiveEventWaitHandle;

        // thread for handling inflight messages queue asynchronously
        private Thread processInflightThread;
        // event for starting process inflight queue asynchronously
        private AutoResetEvent inflightWaitHandle;

        // event for signaling synchronous receive
        AutoResetEvent syncEndReceiving;
        // message received
        MQTTMsgBase msgReceived;

        // exeption thrown during receiving
        Exception exReceiving;

        // keep alive period (in ms)
        private int keepAlivePeriod;
        // thread for sending keep alive message
        private Thread keepAliveThread;
        private AutoResetEvent keepAliveEvent;
        // keep alive timeout expired
        private bool isKeepAliveTimeout;
        // last communication time in ticks
        private long lastCommTime;

        // event for PUBLISH message received
        public event MQTTMsgPublishEventHandler MQTTMsgPublishReceived;
        // event for published message
        public event MQTTMsgPublishedEventHandler MQTTMsgPublished;
        // event for subscribed topic
        public event MQTTMsgSubscribedEventHandler MQTTMsgSubscribed;
        // event for unsubscribed topic
        public event MQTTMsgUnsubscribedEventHandler MQTTMsgUnsubscribed;

        // event for client disconnection (DISCONNECT message or not)
        public event MQTTMsgDisconnectEventHandler MQTTMsgDisconnected;
        
        // channel to communicate over the network
        private IMQTTNetworkChannel channel;

        // inflight messages queue
        private Queue inflightQueue;
        // internal queue for received messages about inflight messages
        private ListQueue internalQueue;
        // receive queue for received messages
        private Queue receiveQueue;

        // reference to avoid access to singleton via property
        private MQTTSettings settings;

        // current message identifier generated
        private ushort messageIdCounter = 0;

        /// <summary>
        /// Connection status between client and broker
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Client identifier
        /// </summary>
        public string ClientId { get; private set; }

        /// <summary>
        /// Clean session flag
        /// </summary>
        public bool CleanSession { get; private set; }

        /// <summary>
        /// Will flag
        /// </summary>
        public bool WillFlag { get; private set; }

        /// <summary>
        /// Will QOS level
        /// </summary>
        public byte WillQosLevel { get; private set; }

        /// <summary>
        /// Will topic
        /// </summary>
        public string WillTopic { get; private set; }

        /// <summary>
        /// Will message
        /// </summary>
        public string WillMessage { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerIpAddress">Broker IP address</param>
        public MQTTClient(IPAddress brokerIpAddress) :
            this(brokerIpAddress, MQTTSettings.MQTT_BROKER_DEFAULT_PORT, false, null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerIpAddress">Broker IP address</param>
        /// <param name="brokerPort">Broker port</param>
        /// <param name="secure">Using secure connection</param>
        /// <param name="caCert">CA certificate for secure connection</param>
        public MQTTClient(IPAddress brokerIpAddress, int brokerPort, bool secure, X509Certificate caCert)
        {
            this.Init(null, brokerIpAddress, brokerPort, secure, caCert);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerHostName">Broker Host Name</param>
        public MQTTClient(string brokerHostName) :
            this(brokerHostName, MQTTSettings.MQTT_BROKER_DEFAULT_PORT, false, null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="brokerHostName">Broker Host Name</param>
        /// <param name="brokerPort">Broker port</param>
        /// <param name="secure">Using secure connection</param>
        /// <param name="caCert">CA certificate for secure connection</param>
        public MQTTClient(string brokerHostName, int brokerPort, bool secure, X509Certificate caCert)
        {
            // throw exceptions to the caller
            IPHostEntry hostEntry = Dns.GetHostEntry(brokerHostName);

            if ((hostEntry != null) && (hostEntry.AddressList.Length > 0))
            {
                // check for the first address not null
                // it seems that with .Net Micro Framework, the IPV6 addresses aren't supported and return "null"
                int i = 0;
                while (hostEntry.AddressList[i] == null) i++;
                this.Init(brokerHostName, hostEntry.AddressList[i], brokerPort, secure, caCert);
            }
            else
                throw new ApplicationException("No address found for the broker");
        }



        /// <summary>
        /// MQTTClient initialization
        /// </summary>
        /// <param name="brokerHostName">Broker host name</param>
        /// <param name="brokerIpAddress">Broker IP address</param>
        /// <param name="brokerPort">Broker port</param>
        /// <param name="secure">>Using secure connection</param>
        /// <param name="caCert">CA certificate for secure connection</param>
        private void Init(string brokerHostName, IPAddress brokerIpAddress, int brokerPort, bool secure, X509Certificate caCert)
        {


            if (secure)
                throw new ArgumentException("Library compiled without SSL support");


            this.brokerHostName = brokerHostName;
            // if broker hostname is null, set ip address
            if (this.brokerHostName == null)
                this.brokerHostName = brokerIpAddress.ToString();

            this.brokerIpAddress = brokerIpAddress;
            this.brokerPort = brokerPort;
            this.secure = secure;



            // reference to MQTT settings
            this.settings = MQTTSettings.Instance;

            this.syncEndReceiving = new AutoResetEvent(false);
            this.keepAliveEvent = new AutoResetEvent(false);

            // queue for handling inflight messages (publishing and acknowledge)
            this.inflightWaitHandle = new AutoResetEvent(false);
            this.inflightQueue = new Queue();

            // queue for received message
            this.receiveEventWaitHandle = new AutoResetEvent(false);
            this.receiveQueue = new Queue();
            this.internalQueue = new ListQueue();
        }

        /// <summary>
        /// Connect to broker
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <returns>Return code of CONNACK message from broker</returns>
        public byte Connect(string clientId)
        {
            return this.Connect(clientId, null, null, false, MQTTMsgConnect.QOS_LEVEL_AT_MOST_ONCE, false, null, null, true, MQTTMsgConnect.KEEP_ALIVE_PERIOD_DEFAULT);
        }

        /// <summary>
        /// Connect to broker
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <returns>Return code of CONNACK message from broker</returns>
        public byte Connect(string clientId,
            string username,
            string password)
        {
            return this.Connect(clientId, username, password, false, MQTTMsgConnect.QOS_LEVEL_AT_MOST_ONCE, false, null, null, true, MQTTMsgConnect.KEEP_ALIVE_PERIOD_DEFAULT);
        }

        /// <summary>
        /// Connect to broker
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <param name="cleanSession">Clean sessione flag</param>
        /// <param name="keepAlivePeriod">Keep alive period</param>
        /// <returns>Return code of CONNACK message from broker</returns>
        public byte Connect(string clientId,
            string username,
            string password,
            bool cleanSession,
            ushort keepAlivePeriod)
        {
            return this.Connect(clientId, username, password, false, MQTTMsgConnect.QOS_LEVEL_AT_MOST_ONCE, false, null, null, cleanSession, keepAlivePeriod);
        }

        /// <summary>
        /// Connect to broker
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <param name="willRetain">Will retain flag</param>
        /// <param name="willQosLevel">Will QOS level</param>
        /// <param name="willFlag">Will flag</param>
        /// <param name="willTopic">Will topic</param>
        /// <param name="willMessage">Will message</param>
        /// <param name="cleanSession">Clean sessione flag</param>
        /// <param name="keepAlivePeriod">Keep alive period</param>
        /// <returns>Return code of CONNACK message from broker</returns>
        public byte Connect(string clientId,
            string username,
            string password,
            bool willRetain,
            byte willQosLevel,
            bool willFlag,
            string willTopic,
            string willMessage,
            bool cleanSession,
            ushort keepAlivePeriod)
        {
            // create CONNECT message
            MQTTMsgConnect connect = new MQTTMsgConnect(clientId,
                username,
                password,
                willRetain,
                willQosLevel,
                willFlag,
                willTopic,
                willMessage,
                cleanSession,
                keepAlivePeriod);

            try
            {


                this.channel = new MQTTNetworkChannel(this.brokerHostName, this.brokerIpAddress, this.brokerPort, this.secure, this.caCert);

                this.channel.Connect();
            }
            catch (Exception ex)
            {
                throw new MQTTConnectionException("Exception connecting to the broker", ex);
            }

            this.lastCommTime = 0;
            this.isRunning = true;
            // start thread for receiving messages from broker
            this.receiveThread = new Thread(this.ReceiveThread);
            this.receiveThread.Start();

            MQTTMsgConnack connack = (MQTTMsgConnack)this.SendReceive(connect);
            // if connection accepted, start keep alive timer and 
            if (connack.ReturnCode == MQTTMsgConnack.CONN_ACCEPTED)
            {
                // set all client properties
                this.ClientId = clientId;
                this.CleanSession = cleanSession;
                this.WillFlag = willFlag;
                this.WillTopic = willTopic;
                this.WillMessage = willMessage;
                this.WillQosLevel = willQosLevel;

                this.keepAlivePeriod = keepAlivePeriod * 1000; // convert in ms

                // start thread for sending keep alive message to the broker
                this.keepAliveThread = new Thread(this.KeepAliveThread);
                this.keepAliveThread.Start();

                // start thread for raising received message event from broker
                this.receiveEventThread = new Thread(this.ReceiveEventThread);
                this.receiveEventThread.Start();

                // start thread for handling inflight messages queue to broker asynchronously (publish and acknowledge)
                this.processInflightThread = new Thread(this.ProcessInflightThread);
                this.processInflightThread.Start();

                this.IsConnected = true;
            }
            return connack.ReturnCode;
        }

        /// <summary>
        /// Disconnect from broker
        /// </summary>
        public void Disconnect()
        {
            MQTTMsgDisconnect disconnect = new MQTTMsgDisconnect();
            this.Send(disconnect);

            // close client
            this.Close();
        }


        /// <summary>
        /// Close client
        /// </summary>

        private void Close()

        {
            // stop receiving thread
            this.isRunning = false;

            // wait end receive thread
            //if (this.receiveThread != null)
            //    this.receiveThread.Join();

            // wait end receive event thread
            if (this.receiveEventThread != null)
            {
                this.receiveEventWaitHandle.Set();
                // NOTE : no join because Close() could be called inside ReceiveEventThread
                //        so we have to avoid deadlock
                //this.receiveEventThread.Join();
            }

            // waint end process inflight thread
            if (this.processInflightThread != null)
            {
                this.inflightWaitHandle.Set();
                // NOTE : no join because Close() could be called inside ProcessInflightThread
                //        so we have to avoid deadlock
                //this.processInflightThread.Join();
            }

            // avoid deadlock if keep alive timeout expired
            if (!this.isKeepAliveTimeout)
            {

                // unlock keep alive thread and wait
                this.keepAliveEvent.Set();

                if (this.keepAliveThread != null)
                    this.keepAliveThread.Join();

            }

            // close network channel
            this.channel.Close();

            // keep alive thread will set it gracefully
            if (!this.isKeepAliveTimeout)
                this.IsConnected = false;
        }

        /// <summary>
        /// Execute ping to broker for keep alive
        /// </summary>
        /// <returns>PINGRESP message from broker</returns>
        private MQTTMsgPingResp Ping()
        {
            MQTTMsgPingReq pingreq = new MQTTMsgPingReq();
            try
            {
                // broker must send PINGRESP within timeout equal to keep alive period
                return (MQTTMsgPingResp)this.SendReceive(pingreq, this.keepAlivePeriod);
            }
            catch (Exception e)
            {
                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Error, "Exception occurred: {0}", e.ToString());

                this.isKeepAliveTimeout = true;
                // client must close connection
                this.Close();
                return null;
            }
        }


        /// <summary>
        /// Subscribe for message topics
        /// </summary>
        /// <param name="topics">List of topics to subscribe</param>
        /// <param name="qosLevels">QOS levels related to topics</param>
        /// <returns>Message Id related to SUBSCRIBE message</returns>
        public ushort Subscribe(string[] topics, byte[] qosLevels)
        {
            MQTTMsgSubscribe subscribe =
                new MQTTMsgSubscribe(topics, qosLevels);
            subscribe.MessageId = this.GetMessageId();

            // enqueue subscribe request into the inflight queue
            this.EnqueueInflight(subscribe, MQTTMsgFlow.ToPublish);

            return subscribe.MessageId;
        }

        /// <summary>
        /// Unsubscribe for message topics
        /// </summary>
        /// <param name="topics">List of topics to unsubscribe</param>
        /// <returns>Message Id in UNSUBACK message from broker</returns>
        public ushort Unsubscribe(string[] topics)
        {
            MQTTMsgUnsubscribe unsubscribe =
                new MQTTMsgUnsubscribe(topics);
            unsubscribe.MessageId = this.GetMessageId();

            // enqueue unsubscribe request into the inflight queue
            this.EnqueueInflight(unsubscribe, MQTTMsgFlow.ToPublish);

            return unsubscribe.MessageId;
        }

        /// <summary>
        /// Publish a message asynchronously (QoS Level 0 and not retained)
        /// </summary>
        /// <param name="topic">Message topic</param>
        /// <param name="message">Message data (payload)</param>
        /// <returns>Message Id related to PUBLISH message</returns>
        public ushort Publish(string topic, byte[] message)
        {
            return this.Publish(topic, message, MQTTMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
        }

        /// <summary>
        /// Publish a message asynchronously
        /// </summary>
        /// <param name="topic">Message topic</param>
        /// <param name="message">Message data (payload)</param>
        /// <param name="qosLevel">QoS Level</param>
        /// <param name="retain">Retain flag</param>
        /// <returns>Message Id related to PUBLISH message</returns>
        public ushort Publish(string topic, byte[] message, byte qosLevel, bool retain)
        {
            MQTTMsgPublish publish =
                    new MQTTMsgPublish(topic, message, false, qosLevel, retain);
            publish.MessageId = this.GetMessageId();

            // enqueue message to publish into the inflight queue
            this.EnqueueInflight(publish, MQTTMsgFlow.ToPublish);           

            return publish.MessageId;
        }

        /// <summary>
        /// Wrapper method for raising message received event
        /// </summary>
        /// <param name="msg">Message received</param>
        private void OnMQTTMsgReceived(MQTTMsgBase msg)
        {
            lock (this.receiveQueue)
            {
                this.receiveQueue.Enqueue(msg);
            }

            this.receiveEventWaitHandle.Set();
        }

        /// <summary>
        /// Wrapper method for raising PUBLISH message received event
        /// </summary>
        /// <param name="publish">PUBLISH message received</param>
        private void OnMQTTMsgPublishReceived(MQTTMsgPublish publish)
        {
            if (this.MQTTMsgPublishReceived != null)
            {
                this.MQTTMsgPublishReceived(this,
                    new MQTTMsgPublishEventArgs(publish.Topic, publish.Message, publish.DupFlag, publish.QosLevel, publish.Retain));
            }
        }

        /// <summary>
        /// Wrapper method for raising published message event
        /// </summary>
        /// <param name="messageId">Message identifier for published message</param>
        private void OnMQTTMsgPublished(ushort messageId)
        {
            if (this.MQTTMsgPublished != null)
            {
                this.MQTTMsgPublished(this,
                    new MQTTMsgPublishedEventArgs(messageId));
            }
        }

        /// <summary>
        /// Wrapper method for raising subscribed topic event
        /// </summary>
        /// <param name="suback">SUBACK message received</param>
        private void OnMQTTMsgSubscribed(MQTTMsgSuback suback)
        {
            if (this.MQTTMsgSubscribed != null)
            {
                this.MQTTMsgSubscribed(this,
                    new MQTTMsgSubscribedEventArgs(suback.MessageId, suback.GrantedQoSLevels));
            }
        }

        /// <summary>
        /// Wrapper method for raising unsubscribed topic event
        /// </summary>
        /// <param name="messageId">Message identifier for unsubscribed topic</param>
        private void OnMQTTMsgUnsubscribed(ushort messageId)
        {
            if (this.MQTTMsgUnsubscribed != null)
            {
                this.MQTTMsgUnsubscribed(this,
                    new MQTTMsgUnsubscribedEventArgs(messageId));
            }
        }


        /// <summary>
        /// Wrapper method for client disconnection event
        /// </summary>
        private void OnMQTTMsgDisconnected()
        {
            if (this.MQTTMsgDisconnected != null)
            {
                this.MQTTMsgDisconnected(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Send a message
        /// </summary>
        /// <param name="msgBytes">Message bytes</param>
        private void Send(byte[] msgBytes)
        {
            try
            {
                // send message
                this.channel.Send(msgBytes);

                // update last message sent ticks
                this.lastCommTime = Environment.TickCount;

            }
            catch (Exception e)
            {
                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Error, "Exception occurred: {0}", e.ToString());

                throw new MQTTCommunicationException(e);
            }
        }

        /// <summary>
        /// Send a message
        /// </summary>
        /// <param name="msg">Message</param>
        private void Send(MQTTMsgBase msg)
        {
            MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "SEND {0}", msg);
            this.Send(msg.GetBytes());
        }

        /// <summary>
        /// Send a message to the broker and wait answer
        /// </summary>
        /// <param name="msgBytes">Message bytes</param>
        /// <returns>MQTT message response</returns>
        private MQTTMsgBase SendReceive(byte[] msgBytes)
        {
            return this.SendReceive(msgBytes, MQTTSettings.MQTT_DEFAULT_TIMEOUT);
        }

        /// <summary>
        /// Send a message to the broker and wait answer
        /// </summary>
        /// <param name="msgBytes">Message bytes</param>
        /// <param name="timeout">Timeout for receiving answer</param>
        /// <returns>MQTT message response</returns>
        private MQTTMsgBase SendReceive(byte[] msgBytes, int timeout)
        {
            // reset handle before sending
            this.syncEndReceiving.Reset();
            try
            {
                // send message
                this.channel.Send(msgBytes);

                // update last message sent ticks
                this.lastCommTime = Environment.TickCount;
            }
            catch (SocketException e)
            {

                // connection reset by broker
                if (e.SocketErrorCode == SocketError.ConnectionReset)
                    this.IsConnected = false;

                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Error, "Exception occurred: {0}", e.ToString());

                throw new MQTTCommunicationException(e);
            }


            // wait for answer from broker
            if (this.syncEndReceiving.WaitOne(timeout))
      {
                // message received without exception
                if (this.exReceiving == null)
                    return this.msgReceived;
                // receiving thread catched exception
                else
                    throw this.exReceiving;
            }
            else
            {
                // throw timeout exception
                throw new MQTTCommunicationException();
            }
        }

        /// <summary>
        /// Send a message to the broker and wait answer
        /// </summary>
        /// <param name="msg">Message</param>
        /// <returns>MQTT message response</returns>
        private MQTTMsgBase SendReceive(MQTTMsgBase msg)
        {
            return this.SendReceive(msg, MQTTSettings.MQTT_DEFAULT_TIMEOUT);
        }

        /// <summary>
        /// Send a message to the broker and wait answer
        /// </summary>
        /// <param name="msg">Message</param>
        /// <param name="timeout">Timeout for receiving answer</param>
        /// <returns>MQTT message response</returns>
        private MQTTMsgBase SendReceive(MQTTMsgBase msg, int timeout)
        {
            MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "SEND {0}", msg);
            return this.SendReceive(msg.GetBytes(), timeout);
        }

        /// <summary>
        /// Enqueue a message into the inflight queue
        /// </summary>
        /// <param name="msg">Message to enqueue</param>
        /// <param name="flow">Message flow (publish, acknowledge)</param>
        private void EnqueueInflight(MQTTMsgBase msg, MQTTMsgFlow flow)
        {
            // enqueue is needed (or not)
            bool enqueue = true;

            // if it is a PUBLISH message with QoS Level 2
            if ((msg.Type == MQTTMsgBase.MQTT_MSG_PUBLISH_TYPE) &&
                (msg.QosLevel == MQTTMsgBase.QOS_LEVEL_EXACTLY_ONCE))
            {
                lock (this.inflightQueue)
                {
                    // if it is a PUBLISH message already received (it is in the inflight queue), the publisher
                    // re-sent it because it didn't received the PUBREC. In this case, we have to re-send PUBREC

                    // NOTE : I need to find on message id and flow because the broker could be publish/received
                    //        to/from client and message id could be the same (one tracked by broker and the other by client)
                    MQTTMsgContextFinder msgCtxFinder = new MQTTMsgContextFinder(((MQTTMsgPublish)msg).MessageId, MQTTMsgFlow.ToAcknowledge);
                    MQTTMsgContext msgCtx = (MQTTMsgContext)this.inflightQueue.Get(msgCtxFinder.Find);

                    // the PUBLISH message is alredy in the inflight queue, we don't need to re-enqueue but we need
                    // to change state to re-send PUBREC
                    if (msgCtx != null)
                    {
                        msgCtx.State = MQTTMsgState.QueuedQos2;
                        msgCtx.Flow = MQTTMsgFlow.ToAcknowledge;
                        enqueue = false;
                    }
                }
            }

            if (enqueue)
            {
                // set a default state
                MQTTMsgState state = MQTTMsgState.QueuedQos0;

                // based on QoS level, the messages flow between broker and client changes
                switch (msg.QosLevel)
                {
                    // QoS Level 0
                    case MQTTMsgBase.QOS_LEVEL_AT_MOST_ONCE:

                        state = MQTTMsgState.QueuedQos0;
                        break;

                    // QoS Level 1
                    case MQTTMsgBase.QOS_LEVEL_AT_LEAST_ONCE:

                        state = MQTTMsgState.QueuedQos1;
                        break;

                    // QoS Level 2
                    case MQTTMsgBase.QOS_LEVEL_EXACTLY_ONCE:

                        state = MQTTMsgState.QueuedQos2;
                        break;
                }

                // queue message context
                MQTTMsgContext msgContext = new MQTTMsgContext()
                {
                    Message = msg,
                    State = state,
                    Flow = flow,
                    Attempt = 0
                };

                lock (this.inflightQueue)
                {
                    // enqueue message and unlock send thread
                    this.inflightQueue.Enqueue(msgContext);
                }
            }

            this.inflightWaitHandle.Set();
        }

        /// <summary>
        /// Enqueue a message into the internal queue
        /// </summary>
        /// <param name="msg">Message to enqueue</param>
        private void EnqueueInternal(MQTTMsgBase msg)
        {
            // enqueue is needed (or not)
            bool enqueue = true;

            // if it is a PUBREL message (for QoS Level 2)
            if (msg.Type == MQTTMsgBase.MQTT_MSG_PUBREL_TYPE)
            {
                lock (this.inflightQueue)
                {
                    // if it is a PUBREL but the corresponding PUBLISH isn't in the inflight queue,
                    // it means that we processed PUBLISH message and received PUBREL and we sent PUBCOMP
                    // but publisher didn't receive PUBCOMP so it re-sent PUBREL. We need only to re-send PUBCOMP.

                    // NOTE : I need to find on message id and flow because the broker could be publish/received
                    //        to/from client and message id could be the same (one tracked by broker and the other by client)
                    MQTTMsgContextFinder msgCtxFinder = new MQTTMsgContextFinder(((MQTTMsgPubrel)msg).MessageId, MQTTMsgFlow.ToAcknowledge);
                    MQTTMsgContext msgCtx = (MQTTMsgContext)this.inflightQueue.Get(msgCtxFinder.Find);

                    // the PUBLISH message isn't in the inflight queue, it was already processed so
                    // we need to re-send PUBCOMP only
                    if (msgCtx == null)
                    {
                        MQTTMsgPubcomp pubcomp = new MQTTMsgPubcomp();
                        pubcomp.MessageId = ((MQTTMsgPubrel)msg).MessageId;

                        this.Send(pubcomp);

                        enqueue = false;
                    }
                }
            }

            if (enqueue)
            {
                lock (this.internalQueue)
                {
                    this.internalQueue.Enqueue(msg);
                    this.inflightWaitHandle.Set();
                }
            }
        }

        /// <summary>
        /// Thread for receiving messages
        /// </summary>
        private void ReceiveThread()
        {
            int readBytes = 0;
            byte[] fixedHeaderFirstByte = new byte[1];
            byte msgType;
            


            while (this.isRunning)
            {
                try
                {
                    if (this.channel.DataAvailable)
                        // read first byte (fixed header)
                        readBytes = this.channel.Receive(fixedHeaderFirstByte);
                    else
                    {


                        // no bytes available, sleep before retry
                        readBytes = 0;
                        Thread.Sleep(10);
                    }

                    if (readBytes > 0)
                    {


                        // extract message type from received byte
                        msgType = (byte)((fixedHeaderFirstByte[0] & MQTTMsgBase.MSG_TYPE_MASK) >> MQTTMsgBase.MSG_TYPE_OFFSET);

                        switch (msgType)
                        {
                            // CONNECT message received
                            case MQTTMsgBase.MQTT_MSG_CONNECT_TYPE:


                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);

                                
                            // CONNACK message received
                            case MQTTMsgBase.MQTT_MSG_CONNACK_TYPE:


                                this.msgReceived = MQTTMsgConnack.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", this.msgReceived);
                                this.syncEndReceiving.Set();
                                break;


                            // PINGREQ message received
                            case MQTTMsgBase.MQTT_MSG_PINGREQ_TYPE:


                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);


                            // PINGRESP message received
                            case MQTTMsgBase.MQTT_MSG_PINGRESP_TYPE:


                                this.msgReceived = MQTTMsgPingResp.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", this.msgReceived);
                                this.syncEndReceiving.Set();
                                break;


                            // SUBSCRIBE message received
                            case MQTTMsgBase.MQTT_MSG_SUBSCRIBE_TYPE:


                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);


                            // SUBACK message received
                            case MQTTMsgBase.MQTT_MSG_SUBACK_TYPE:


                                // enqueue SUBACK message received (for QoS Level 1) into the internal queue
                                MQTTMsgSuback suback = MQTTMsgSuback.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", suback);

                                // enqueue SUBACK message into the internal queue
                                this.EnqueueInternal(suback);

                                break;


                            // PUBLISH message received
                            case MQTTMsgBase.MQTT_MSG_PUBLISH_TYPE:

                                MQTTMsgPublish publish = MQTTMsgPublish.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", publish);

                                // enqueue PUBLISH message to acknowledge into the inflight queue
                                this.EnqueueInflight(publish, MQTTMsgFlow.ToAcknowledge);

                                break;

                            // PUBACK message received
                            case MQTTMsgBase.MQTT_MSG_PUBACK_TYPE:

                                // enqueue PUBACK message received (for QoS Level 1) into the internal queue
                                MQTTMsgPuback puback = MQTTMsgPuback.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", puback);

                                // enqueue PUBACK message into the internal queue
                                this.EnqueueInternal(puback);

                                break;

                            // PUBREC message received
                            case MQTTMsgBase.MQTT_MSG_PUBREC_TYPE:

                                // enqueue PUBREC message received (for QoS Level 2) into the internal queue
                                MQTTMsgPubrec pubrec = MQTTMsgPubrec.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", pubrec);

                                // enqueue PUBREC message into the internal queue
                                this.EnqueueInternal(pubrec);

                                break;

                            // PUBREL message received
                            case MQTTMsgBase.MQTT_MSG_PUBREL_TYPE:

                                // enqueue PUBREL message received (for QoS Level 2) into the internal queue
                                MQTTMsgPubrel pubrel = MQTTMsgPubrel.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", pubrel);

                                // enqueue PUBREL message into the internal queue
                                this.EnqueueInternal(pubrel);

                                break;
                                
                            // PUBCOMP message received
                            case MQTTMsgBase.MQTT_MSG_PUBCOMP_TYPE:

                                // enqueue PUBCOMP message received (for QoS Level 2) into the internal queue
                                MQTTMsgPubcomp pubcomp = MQTTMsgPubcomp.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", pubcomp);

                                // enqueue PUBCOMP message into the internal queue
                                this.EnqueueInternal(pubcomp);

                                break;

                            // UNSUBSCRIBE message received
                            case MQTTMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE:

#if BROKER
                                MQTTMsgUnsubscribe unsubscribe = MQTTMsgUnsubscribe.Parse(fixedHeaderFirstByte[0], this.channel);
                                Trace.WriteLine(TraceLevel.Frame, "RECV {0}", unsubscribe);

                                // raise message received event
                                this.OnMQTTMsgReceived(unsubscribe);

                                break;
#else
                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
#endif

                            // UNSUBACK message received
                            case MQTTMsgBase.MQTT_MSG_UNSUBACK_TYPE:

#if BROKER
                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
#else
                                // enqueue UNSUBACK message received (for QoS Level 1) into the internal queue
                                MQTTMsgUnsuback unsuback = MQTTMsgUnsuback.Parse(fixedHeaderFirstByte[0], this.channel);
                                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Frame, "RECV {0}", unsuback);

                                // enqueue UNSUBACK message into the internal queue
                                this.EnqueueInternal(unsuback);

                                break;
#endif

                            // DISCONNECT message received
                            case MQTTMsgDisconnect.MQTT_MSG_DISCONNECT_TYPE:

#if BROKER
                                MQTTMsgDisconnect disconnect = MQTTMsgDisconnect.Parse(fixedHeaderFirstByte[0], this.channel);
                                Trace.WriteLine(TraceLevel.Frame, "RECV {0}", disconnect);

                                // raise message received event
                                this.OnMQTTMsgReceived(disconnect);

                                break;
#else
                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
#endif

                            default:

                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
                        }

                        this.exReceiving = null;
                    }

                }
                catch (Exception e)
                {
                    MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Error, "Exception occurred: {0}", e.ToString());
                    this.exReceiving = new MQTTCommunicationException(e);
                }
            }
        }

        /// <summary>
        /// Thread for handling keep alive message
        /// </summary>
        private void KeepAliveThread()
        {
            long now = 0;
            int wait = this.keepAlivePeriod;
            this.isKeepAliveTimeout = false;

            while (this.isRunning)
            {

                // waiting...
                this.keepAliveEvent.WaitOne(wait);


                if (this.isRunning)
                {
                    now = Environment.TickCount;

                    // if timeout exceeded ...
                    if ((now - this.lastCommTime) >= this.keepAlivePeriod)
                    {
#if BROKER
                        this.isKeepAliveTimeout = true;
                        // client must close connection
                        this.Close();
#else
                        // ... send keep alive
						this.Ping();
						wait = this.keepAlivePeriod;
#endif
                    }
                    else
                    {
                        // update waiting time
                        wait = (int)(this.keepAlivePeriod - (now - this.lastCommTime));
                    }
                }
            }

            if (this.isKeepAliveTimeout)
            {
                this.IsConnected = false;
                // raise disconnection client event
                this.OnMQTTMsgDisconnected();
            }
        }

        /// <summary>
        /// Thread for raising received message event
        /// </summary>
        private void ReceiveEventThread()
        {
            while (this.isRunning)
            {
                if (this.receiveQueue.Count == 0)
                    // wait on receiving message from client
                    this.receiveEventWaitHandle.WaitOne();

                // check if it is running or we are closing client
                if (this.isRunning)
                {
                    // get message from queue
                    MQTTMsgBase msg = null;
                    lock (this.receiveQueue)
                    {
                        if (this.receiveQueue.Count > 0)
                            msg = (MQTTMsgBase)this.receiveQueue.Dequeue();
                    }

                    if (msg != null)
                    {
                        switch (msg.Type)
                        {
                            // CONNECT message received
                            case MQTTMsgBase.MQTT_MSG_CONNECT_TYPE:

#if BROKER
                                // raise connected client event (CONNECT message received)
                                this.OnMQTTMsgConnected((MQTTMsgConnect)msg);
                                break;
#else
                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
#endif

                            // SUBSCRIBE message received
                            case MQTTMsgBase.MQTT_MSG_SUBSCRIBE_TYPE:

#if BROKER
                                MQTTMsgSubscribe subscribe = (MQTTMsgSubscribe)msg;
                                // raise subscribe topic event (SUBSCRIBE message received)
                                this.OnMQTTMsgSubscribeReceived(subscribe.MessageId, subscribe.Topics, subscribe.QoSLevels);
                                break;
#else
                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
#endif

                            // SUBACK message received
                            case MQTTMsgBase.MQTT_MSG_SUBACK_TYPE:

                                // raise subscribed topic event (SUBACK message received)
                                this.OnMQTTMsgSubscribed((MQTTMsgSuback)msg);
                                break;

                            // PUBLISH message received
                            case MQTTMsgBase.MQTT_MSG_PUBLISH_TYPE:

                                // raise PUBLISH message received event 
                                this.OnMQTTMsgPublishReceived((MQTTMsgPublish)msg);
                                break;

                            // PUBACK message received
                            case MQTTMsgBase.MQTT_MSG_PUBACK_TYPE:

                                // raise published message event
                                // (PUBACK received for QoS Level 1)
                                this.OnMQTTMsgPublished(((MQTTMsgPuback)msg).MessageId);
                                break;

                            // PUBREL message received
                            case MQTTMsgBase.MQTT_MSG_PUBREL_TYPE:

                                // raise message received event 
                                // (PUBREL received for QoS Level 2)
                                this.OnMQTTMsgPublishReceived((MQTTMsgPublish)msg);
                                break;

                            // PUBCOMP message received
                            case MQTTMsgBase.MQTT_MSG_PUBCOMP_TYPE:

                                // raise published message event
                                // (PUBCOMP received for QoS Level 2)
                                this.OnMQTTMsgPublished(((MQTTMsgPubcomp)msg).MessageId);
                                break;

                            // UNSUBSCRIBE message received from client
                            case MQTTMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE:

#if BROKER
                                MQTTMsgUnsubscribe unsubscribe = (MQTTMsgUnsubscribe)msg;
                                // raise unsubscribe topic event (UNSUBSCRIBE message received)
                                this.OnMQTTMsgUnsubscribeReceived(unsubscribe.MessageId, unsubscribe.Topics);
                                break;
#else
                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
#endif

                            // UNSUBACK message received
                            case MQTTMsgBase.MQTT_MSG_UNSUBACK_TYPE:

                                // raise unsubscribed topic event
                                this.OnMQTTMsgUnsubscribed(((MQTTMsgUnsuback)msg).MessageId);
                                break;

                            // DISCONNECT message received from client
                            case MQTTMsgDisconnect.MQTT_MSG_DISCONNECT_TYPE:

#if BROKER
                                // raise disconnected client event (DISCONNECT message received)
                                this.OnMQTTMsgDisconnected();
                                break;
#else
                                throw new MQTTClientException(MQTTClientErrorCode.WrongBrokerMessage);
#endif
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Process inflight messages queue
        /// </summary>
        private void ProcessInflightThread()
        {
            MQTTMsgContext msgContext = null;
            MQTTMsgBase msgInflight = null;
            MQTTMsgBase msgReceived = null;
            bool acknowledge = false;
            int timeout = Timeout.Infinite;

            try
            {
                while (this.isRunning)
                {
#if (MF_FRAMEWORK_VERSION_V4_2 || MF_FRAMEWORK_VERSION_V4_3 || COMPACT_FRAMEWORK)
                    // wait on message queueud to inflight
                    this.inflightWaitHandle.WaitOne(timeout, false);
#else
                    // wait on message queueud to inflight
                    this.inflightWaitHandle.WaitOne(timeout);
#endif

                    // it could be unblocked because Close() method is joining
                    if (this.isRunning)
                    {
                        lock (this.inflightQueue)
                        {
                            // set timeout tu MaxValue instead of Infinte (-1) to perform
                            // compare with calcultad current msgTimeout
                            timeout = Int32.MaxValue;

                            // a message inflight could be re-enqueued but we have to
                            // analyze it only just one time for cycle
                            int count = this.inflightQueue.Count;
                            // process all inflight queued messages
                            while (count > 0)
                            {
                                count--;
                                acknowledge = false;
                                msgReceived = null;

                                // dequeue message context from queue
                                msgContext = (MQTTMsgContext)this.inflightQueue.Dequeue();

                                // get inflight message
                                msgInflight = (MQTTMsgBase)msgContext.Message;

                                switch (msgContext.State)
                                {
                                    case MQTTMsgState.QueuedQos0:

                                        // QoS 0, PUBLISH message to send to broker, no state change, no acknowledge
                                        if (msgContext.Flow == MQTTMsgFlow.ToPublish)
                                        {
                                            this.Send(msgInflight);
                                        }
                                        // QoS 0, no need acknowledge
                                        else if (msgContext.Flow == MQTTMsgFlow.ToAcknowledge)
                                        {
                                            // notify published message from broker (no need acknowledged)
                                            this.OnMQTTMsgReceived(msgInflight);
                                        }
                                        break;

                                    case MQTTMsgState.QueuedQos1:

                                        // QoS 1, PUBLISH or SUBSCRIBE/UNSUBSCRIBE message to send to broker, state change to wait PUBACK or SUBACK/UNSUBACK
                                        if (msgContext.Flow == MQTTMsgFlow.ToPublish)
                                        {
                                            if (msgInflight.Type == MQTTMsgBase.MQTT_MSG_PUBLISH_TYPE)
                                                // PUBLISH message to send, wait for PUBACK
                                                msgContext.State = MQTTMsgState.WaitForPuback;
                                            else if (msgInflight.Type == MQTTMsgBase.MQTT_MSG_SUBSCRIBE_TYPE)
                                                // SUBSCRIBE message to send, wait for SUBACK
                                                msgContext.State = MQTTMsgState.WaitForSuback;
                                            else if (msgInflight.Type == MQTTMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE)
                                                // UNSUBSCRIBE message to send, wait for UNSUBACK
                                                msgContext.State = MQTTMsgState.WaitForUnsuback;

                                            msgContext.Timestamp = Environment.TickCount;
                                            msgContext.Attempt++;
                                            // retry ? set dup flag
                                            if (msgContext.Attempt > 1)
                                                msgInflight.DupFlag = true;

                                            this.Send(msgInflight);

                                            // update timeout
                                            int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                            timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                            // re-enqueue message (I have to re-analyze for receiving PUBACK, SUBACK or UNSUBACK)
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        // QoS 1, PUBLISH message received from broker to acknowledge, send PUBACK
                                        else if (msgContext.Flow == MQTTMsgFlow.ToAcknowledge)
                                        {
                                            MQTTMsgPuback puback = new MQTTMsgPuback();
                                            puback.MessageId = ((MQTTMsgPublish)msgInflight).MessageId;

                                            this.Send(puback);

                                            // notify published message from broker and acknowledged
                                            this.OnMQTTMsgReceived(msgInflight);
                                        }
                                        break;

                                    case MQTTMsgState.QueuedQos2:

                                        // QoS 2, PUBLISH message to send to broker, state change to wait PUBREC
                                        if (msgContext.Flow == MQTTMsgFlow.ToPublish)
                                        {
                                            msgContext.State = MQTTMsgState.WaitForPubrec;
                                            msgContext.Timestamp = Environment.TickCount;
                                            msgContext.Attempt++;
                                            // retry ? set dup flag
                                            if (msgContext.Attempt > 1)
                                                msgInflight.DupFlag = true;

                                            this.Send(msgInflight);

                                            // update timeout
                                            int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                            timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                            // re-enqueue message (I have to re-analyze for receiving PUBREC)
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        // QoS 2, PUBLISH message received from broker to acknowledge, send PUBREC, state change to wait PUBREL
                                        else if (msgContext.Flow == MQTTMsgFlow.ToAcknowledge)
                                        {
                                            MQTTMsgPubrec pubrec = new MQTTMsgPubrec();
                                            pubrec.MessageId = ((MQTTMsgPublish)msgInflight).MessageId;

                                            msgContext.State = MQTTMsgState.WaitForPubrel;

                                            this.Send(pubrec);

                                            // re-enqueue message (I have to re-analyze for receiving PUBREL)
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        break;

                                    case MQTTMsgState.WaitForPuback:
                                    case MQTTMsgState.WaitForSuback:
                                    case MQTTMsgState.WaitForUnsuback:

                                        // QoS 1, waiting for PUBACK of a PUBLISH message sent or
                                        //        waiting for SUBACK of a SUBSCRIBE message sent or
                                        //        waiting for UNSUBACK of a UNSUBSCRIBE message sent or
                                        if (msgContext.Flow == MQTTMsgFlow.ToPublish)
                                        {
                                            acknowledge = false;
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                {
                                                    if (msgInflight.Type == MQTTMsgBase.MQTT_MSG_PUBLISH_TYPE)
                                                    {
                                                        msgReceived= (MQTTMsgBase)this.internalQueue.Get(p => ((MQTTMsgBase)p).Type == MQTTMsgBase.MQTT_MSG_PUBACK_TYPE);
                                                    }else if(msgInflight.Type == MQTTMsgBase.MQTT_MSG_SUBSCRIBE_TYPE)
                                                    {
                                                        msgReceived = (MQTTMsgBase)this.internalQueue.Get(p => ((MQTTMsgBase)p).Type == MQTTMsgBase.MQTT_MSG_SUBACK_TYPE);

                                                    }
                                                    else // unsubscribe typr
                                                    {
                                                        msgReceived = (MQTTMsgBase)this.internalQueue.Get(p => ((MQTTMsgBase)p).Type == MQTTMsgBase.MQTT_MSG_UNSUBACK_TYPE);

                                                    }
                                                    //msgReceived = (MQTTMsgBase)this.internalQueue.Peek();
                                                }
                                                    
                                            }

                                            // it is a PUBACK message or a SUBACK/UNSUBACK message
                                            if ((msgReceived != null) && ((msgReceived.Type == MQTTMsgBase.MQTT_MSG_PUBACK_TYPE) ||
                                                                          (msgReceived.Type == MQTTMsgBase.MQTT_MSG_SUBACK_TYPE) ||
                                                                          (msgReceived.Type == MQTTMsgBase.MQTT_MSG_UNSUBACK_TYPE)))
                                            {
                                                // PUBACK message or SUBACK message for the current message
                                                if (((msgInflight.Type == MQTTMsgBase.MQTT_MSG_PUBLISH_TYPE) && (((MQTTMsgPuback)msgReceived).MessageId == ((MQTTMsgPublish)msgInflight).MessageId)) ||
                                                    ((msgInflight.Type == MQTTMsgBase.MQTT_MSG_SUBSCRIBE_TYPE) && (((MQTTMsgSuback)msgReceived).MessageId == ((MQTTMsgSubscribe)msgInflight).MessageId)) ||
                                                    ((msgInflight.Type == MQTTMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE) && (((MQTTMsgUnsuback)msgReceived).MessageId == ((MQTTMsgUnsubscribe)msgInflight).MessageId)))
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        //this.internalQueue.Dequeue();
                                                        this.internalQueue.Remove(msgReceived);
                                                        acknowledge = true;
                                                    }

                                                    // notify received acknowledge from broker of a published message or subscribe/unsubscribe message
                                                    this.OnMQTTMsgReceived(msgReceived);
                                                }
                                            }

                                            // current message not acknowledged, no PUBACK or SUBACK/UNSUBACK or not equal messageid 
                                            if (!acknowledge)
                                            {
                                                // check timeout for receiving PUBACK since PUBLISH was sent or
                                                // for receiving SUBACK since SUBSCRIBE was sent or
                                                // for receiving UNSUBACK since UNSUBSCRIBE was sent
                                                if ((Environment.TickCount - msgContext.Timestamp) >= this.settings.DelayOnRetry)
                                                {
                                                    // max retry not reached, resend
                                                    if (msgContext.Attempt <= this.settings.AttemptsOnRetry)
                                                    {
                                                        msgContext.State = MQTTMsgState.QueuedQos1;

                                                        // re-enqueue message
                                                        this.inflightQueue.Enqueue(msgContext);

                                                        // update timeout (0 -> reanalyze queue immediately)
                                                        timeout = 0;
                                                    }
                                                }
                                                else
                                                {
                                                    // re-enqueue message (I have to re-analyze for receiving PUBACK, SUBACK or UNSUBACK)
                                                    this.inflightQueue.Enqueue(msgContext);

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;
                                                }
                                            }
                                        }
                                        break;

                                    case MQTTMsgState.WaitForPubrec:

                                        // QoS 2, waiting for PUBREC of a PUBLISH message sent
                                        if (msgContext.Flow == MQTTMsgFlow.ToPublish)
                                        {
                                            acknowledge = false;
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                    msgReceived = (MQTTMsgBase)this.internalQueue.Get(p => ((MQTTMsgBase)p).Type == MQTTMsgBase.MQTT_MSG_PUBREC_TYPE);

                                                //msgReceived = (MQTTMsgBase)this.internalQueue.Peek();
                                            }

                                            // it is a PUBREC message
                                            if ((msgReceived != null) && (msgReceived.Type == MQTTMsgBase.MQTT_MSG_PUBREC_TYPE))
                                            {
                                                // PUBREC message for the current PUBLISH message, send PUBREL, wait for PUBCOMP
                                                if (((MQTTMsgPubrec)msgReceived).MessageId == ((MQTTMsgPublish)msgInflight).MessageId)
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        //this.internalQueue.Dequeue();
                                                        this.internalQueue.Remove(msgReceived);
                                                        acknowledge = true;
                                                    }

                                                    MQTTMsgPubrel pubrel = new MQTTMsgPubrel();
                                                    pubrel.MessageId = ((MQTTMsgPublish)msgInflight).MessageId;

                                                    msgContext.State = MQTTMsgState.WaitForPubcomp;
                                                    msgContext.Timestamp = Environment.TickCount;
                                                    msgContext.Attempt = 1;

                                                    this.Send(pubrel);

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);
                                                }
                                            }

                                            // current message not acknowledged
                                            if (!acknowledge)
                                            {
                                                // check timeout for receiving PUBREC since PUBLISH was sent
                                                if ((Environment.TickCount - msgContext.Timestamp) >= this.settings.DelayOnRetry)
                                                {
                                                    // max retry not reached, resend
                                                    if (msgContext.Attempt <= this.settings.AttemptsOnRetry)
                                                    {
                                                        msgContext.State = MQTTMsgState.QueuedQos2;

                                                        // re-enqueue message
                                                        this.inflightQueue.Enqueue(msgContext);

                                                        // update timeout (0 -> reanalyze queue immediately)
                                                        timeout = 0;
                                                    }
                                                }
                                                else
                                                {
                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;
                                                }
                                            }
                                        }
                                        break;

                                    case MQTTMsgState.WaitForPubrel:

                                        // QoS 2, waiting for PUBREL of a PUBREC message sent
                                        if (msgContext.Flow == MQTTMsgFlow.ToAcknowledge)
                                        {
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                    msgReceived = (MQTTMsgBase)this.internalQueue.Get(p => ((MQTTMsgBase)p).Type == MQTTMsgBase.MQTT_MSG_PUBREL_TYPE);

                                                //msgReceived = (MQTTMsgBase)this.internalQueue.Peek();
                                            }

                                            // it is a PUBREL message
                                            if ((msgReceived != null) && (msgReceived.Type == MQTTMsgBase.MQTT_MSG_PUBREL_TYPE))
                                            {
                                                // PUBREL message for the current message, send PUBCOMP
                                                if (((MQTTMsgPubrel)msgReceived).MessageId == ((MQTTMsgPublish)msgInflight).MessageId)
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        this.internalQueue.Remove(msgReceived);
                                                        //this.internalQueue.Dequeue();
                                                    }

                                                    MQTTMsgPubcomp pubcomp = new MQTTMsgPubcomp();
                                                    pubcomp.MessageId = ((MQTTMsgPublish)msgInflight).MessageId;

                                                    this.Send(pubcomp);

                                                    // notify published message from broker and acknowledged
                                                    this.OnMQTTMsgReceived(msgInflight);
                                                }
                                                else
                                                {
                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);
                                                }
                                            }
                                            else
                                            {
                                                // re-enqueue message
                                                this.inflightQueue.Enqueue(msgContext);
                                            }
                                        }
                                        break;

                                    case MQTTMsgState.WaitForPubcomp:

                                        // QoS 2, waiting for PUBCOMP of a PUBREL message sent
                                        if (msgContext.Flow == MQTTMsgFlow.ToPublish)
                                        {
                                            acknowledge = false;
                                            lock (this.internalQueue)
                                            {
                                                if (this.internalQueue.Count > 0)
                                                    msgReceived = (MQTTMsgBase)this.internalQueue.Get(p => ((MQTTMsgBase)p).Type == MQTTMsgBase.MQTT_MSG_PUBCOMP_TYPE);

                                                //msgReceived = (MQTTMsgBase)this.internalQueue.Peek();
                                            }

                                            // it is a PUBCOMP message
                                            if ((msgReceived != null) && (msgReceived.Type == MQTTMsgBase.MQTT_MSG_PUBCOMP_TYPE))
                                            {
                                                // PUBCOMP message for the current message
                                                if (((MQTTMsgPubcomp)msgReceived).MessageId == ((MQTTMsgPublish)msgInflight).MessageId)
                                                {
                                                    lock (this.internalQueue)
                                                    {
                                                        // received message processed
                                                        //(this.internalQueue.Dequeue();
                                                        this.internalQueue.Remove(msgReceived);
                                                        acknowledge = true;
                                                    }

                                                    // notify received acknowledge from broker of a published message
                                                    this.OnMQTTMsgReceived(msgReceived);
                                                }
                                            }

                                            // current message not acknowledged
                                            if (!acknowledge)
                                            {
                                                // check timeout for receiving PUBCOMP since PUBREL was sent
                                                if ((Environment.TickCount - msgContext.Timestamp) >= this.settings.DelayOnRetry)
                                                {
                                                    // max retry not reached, resend
                                                    if (msgContext.Attempt < this.settings.AttemptsOnRetry)
                                                    {
                                                        msgContext.State = MQTTMsgState.SendPubrel;

                                                        // re-enqueue message
                                                        this.inflightQueue.Enqueue(msgContext);

                                                        // update timeout (0 -> reanalyze queue immediately)
                                                        timeout = 0;
                                                    }
                                                }
                                                else
                                                {
                                                    // re-enqueue message
                                                    this.inflightQueue.Enqueue(msgContext);

                                                    // update timeout
                                                    int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                                    timeout = (msgTimeout < timeout) ? msgTimeout : timeout;
                                                }
                                            }
                                        }
                                        break;

                                    case MQTTMsgState.SendPubrec:

                                        // TODO : impossible ? --> QueuedQos2 ToAcknowledge
                                        break;

                                    case MQTTMsgState.SendPubrel:

                                        // QoS 2, PUBREL message to send to broker, state change to wait PUBCOMP
                                        if (msgContext.Flow == MQTTMsgFlow.ToPublish)
                                        {
                                            MQTTMsgPubrel pubrel = new MQTTMsgPubrel();
                                            pubrel.MessageId = ((MQTTMsgPublish)msgInflight).MessageId;

                                            msgContext.State = MQTTMsgState.WaitForPubcomp;
                                            msgContext.Timestamp = Environment.TickCount;
                                            msgContext.Attempt++;
                                            // retry ? set dup flag
                                            if (msgContext.Attempt > 1)
                                                pubrel.DupFlag = true;

                                            this.Send(pubrel);

                                            // update timeout
                                            int msgTimeout = (this.settings.DelayOnRetry - (Environment.TickCount - msgContext.Timestamp));
                                            timeout = (msgTimeout < timeout) ? msgTimeout : timeout;

                                            // re-enqueue message
                                            this.inflightQueue.Enqueue(msgContext);
                                        }
                                        break;

                                    case MQTTMsgState.SendPubcomp:
                                        // TODO : impossible ?
                                        break;
                                    case MQTTMsgState.SendPuback:
                                        // TODO : impossible ? --> QueuedQos1 ToAcknowledge
                                        break;
                                    default:
                                        break;
                                }
                            }

                            // if calculated timeout is MaxValue, it means that must be Infinite (-1)
                            if (timeout == Int32.MaxValue)
                                timeout = Timeout.Infinite;
                        }
                    }
                }
            }
            catch (MQTTCommunicationException e)
            {
                MQTTUtility.Trace.WriteLine(MQTTUtility.TraceLevel.Error, "Exception occurred: {0}", e.ToString());

                this.Close();

                // raise disconnection client event
                this.OnMQTTMsgDisconnected();
            }
        }

        /// <summary>
        /// Generate the next message identifier
        /// </summary>
        /// <returns>Message identifier</returns>
        private ushort GetMessageId()
        {
            if (this.messageIdCounter == 0)
                this.messageIdCounter++;
            else
                this.messageIdCounter = ((this.messageIdCounter % UInt16.MaxValue) != 0) ? (ushort)(this.messageIdCounter + 1) : (ushort)0;
            return this.messageIdCounter;
        }

        /// <summary>
        /// Finder class for PUBLISH message inside a queue
        /// </summary>
        internal class MQTTMsgContextFinder
        {
            // PUBLISH message id
            internal ushort MessageId { get; set; }
            // message flow into inflight queue
            internal MQTTMsgFlow Flow { get; set; }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="messageId">Message Id</param>
            /// <param name="flow">Message flow inside inflight queue</param>
            internal MQTTMsgContextFinder(ushort messageId, MQTTMsgFlow flow)
            {
                this.MessageId = messageId;
                this.Flow = flow;
            }

            internal bool Find(object item)
            {
                MQTTMsgContext msgCtx = (MQTTMsgContext)item;
                return ((msgCtx.Message.Type == MQTTMsgBase.MQTT_MSG_PUBLISH_TYPE) &&
                        (((MQTTMsgPublish)msgCtx.Message).MessageId == this.MessageId) &&
                        msgCtx.Flow == this.Flow);

            }
        }
    }

    public class MqttClient {
        private MQTTClient RemoteClient { get; set; }
        public string remoteUsername;
        public string remotePassword;
        public bool useTLS;
        public bool useAuth;
        public int QOSLevel;
        public string remoteTopic; //TODO ADD #
        //private string remoteTopicPublish;
        public string remoteAddress;
        public int remotePort;
        private bool clientDisconnectRequest;
        private int RemoteConnectionAttemptCount;
        private string remoteClientID;
        private bool retainFlag;
        private byte qosLevelPublish;
        private bool DisconnectRequest;
        public Action<string, bool, EventLogEntryType> logAction;
        //public MqttClient otherClient;
        public event Action<string, MQTTMsgPublishEventArgs, MqttClient> onMessageRecievedToSyncronize;
        private bool runForever;
        private Dictionary<string, byte[]> mydataSent;
        // private HashSet<string> mydataSentControl;
        public MqttClient(
            string remoteAddress, int remotePort,
            //string remoteTopic,
            int QOSLevel,
            string remoteClientID,
            bool useAuth,
            string remoteUsername, string remotePassword,
            bool useTLS,
            bool retainFlag,
            byte qosPublish,
            bool runForever
        ) {
            this.runForever = runForever;
            this.retainFlag = retainFlag;
            this.qosLevelPublish = qosPublish;
            //TODO move CONNECT
            this.mydataSent = new Dictionary<string, byte[]>();
            //this.mydataSentControl = new HashSet<string>();
            this.remoteAddress = remoteAddress;
            this.remotePort = remotePort;

            this.QOSLevel = QOSLevel;
            this.remoteClientID = remoteClientID;
            this.useAuth = useAuth;
            this.remoteUsername = remoteUsername;
            this.remotePassword = remotePassword;
            this.useTLS = useTLS;
        }

        public bool connected() {
            return this.RemoteClient.IsConnected;
        }
        public int connectToRemote(bool reconnet = false) {

            if (!reconnet) {
                this.logAction("Connecting to Remote server " + this.remoteAddress + ":" + this.remotePort, false, EventLogEntryType.Information);

            } else {
                int delayTime = this.RemoteConnectionAttemptCount * 10;
                this.logAction("Connecting to Remote server " + this.remoteAddress + ":" + this.remotePort + " in " + delayTime + " Seconds", false, EventLogEntryType.Information);
                System.Threading.Thread.Sleep(10000);
            }
            


            try {
               
                
                if (this.useTLS) {
                    //new X509Certificate().ver
                    //var cert = new X509ChainPolicy();
                    //cert.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    this.RemoteClient = new MQTTClient(this.remoteAddress, this.remotePort, this.useTLS,null );

                } else {
                    this.RemoteClient = new MQTTClient(this.remoteAddress, this.remotePort, this.useTLS, null);

                }
            }
            catch (Exception e) {
                this.logAction("Error connectiong to Remote: " + e.Message, false, EventLogEntryType.Error);
                throw (e);
            }


            this.clientDisconnectRequest = false;
            
            try {
                this.RemoteClient.Connect(this.remoteClientID);
                this.createClientConnectedHandler();
                return 0;
            }
            catch (Exception e) {
                this.logAction("Error on Connect " + e.Message, true, EventLogEntryType.Error);
                this.tryReconnectRemote();
            }

            return 0;

        }

        private void addDataControl(string key, byte[] data) {
            /*lock (this.mydataSent)
            {*/
            try {
                this.mydataSent[key] = data;
            }
            catch (Exception e) {
                try {
                    this.mydataSent.Add(key, data);
                }
                catch (Exception e1) {
                    this.logAction("ERROR ADD", true, EventLogEntryType.Information);
                }

            }
            //}
        }

        public void unsubscribe(string topic) {
            this.RemoteClient.Unsubscribe(new string[1] { topic });
        }

        public void unsubscribe(string[] topics) {
            this.RemoteClient.Unsubscribe(topics);
        }

        public int publishOnRemote(string topic, string message) {
            string topicToPublish = topic;// this.remoteTopicPublish + topic;
            if (this.RemoteClient == null || !this.RemoteClient.IsConnected) {
                return 1;
            }
            byte[] DataToPublish = System.Text.Encoding.ASCII.GetBytes(message);
            this.addDataControl(topicToPublish, DataToPublish);
            try {
                this.RemoteClient.Publish(topicToPublish, DataToPublish, this.qosLevelPublish, this.retainFlag);
            }
            catch (Exception e) {
                this.logAction("Error writting to MQTT, " +e.Message, false,EventLogEntryType.Warning);
                return 1;
            }
            return 0;
        }


        public int publishOnRemote(string topic, string message,byte QOSLevel, bool retain) {
            if (this.RemoteClient == null || !this.RemoteClient.IsConnected) {
                return 1;
            }
            byte[] DataToPublish = System.Text.Encoding.ASCII.GetBytes(message);
            this.addDataControl(topic, DataToPublish);
            try {
                this.RemoteClient.Publish(topic, DataToPublish, QOSLevel, retain);
            }
            catch (Exception e) {
                this.logAction("Error writting to MQTT, " + e.Message, false, EventLogEntryType.Warning);
                return 1;
            }
            return 0;
        }


        private void createClientConnectedHandler() {
            this.mydataSent = new Dictionary<string, byte[]>();
            this.DisconnectRequest = false;
            //this.connectSucessFirstTime = true;
            this.logAction("Connected to " + this.remoteAddress + ":" + this.remotePort, false, EventLogEntryType.Information);
            this.RemoteConnectionAttemptCount = 0;
            this.createClientHandlersRuntime();
            this.logAction("Subscribe " + this.remoteTopic + " on " + this.remoteAddress + ":" + this.remotePort, false, EventLogEntryType.Information);
            /*var level = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce;
            if (this.QOSLevel != 0) {
                level = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce;
            }
            await this.RemoteClient.SubscribeAsync(this.remoteTopic, level);*/

        }


        private void createClientHandlersRuntime() {

            this.RemoteClient.MQTTMsgDisconnected += (sender,e) => {
                //if (!this.connectSucessFirstTime)
                //{
                //    this.logAction("Cannot connect " + this.remoteAddress + ":" + this.remotePort, false, EventLogEntryType.Error);
                //    //System.Environment.Exit(1);
                //}

                if (!this.clientDisconnectRequest /*&& this.connectSucessFirstTime*/) {
                    this.logAction("Disconnected From " + this.remoteAddress + ":" + this.remotePort, false, EventLogEntryType.Warning);
                    this.tryReconnectRemote();

                }
            };


            this.RemoteClient.MQTTMsgPublishReceived += (sender,e) => {
                string topicPublic = e.Topic;

                if (!this.wasMEClient(e.Topic, e.Message)) {
                    //this.logMessage("Recieved Remote" + e.ApplicationMessage.Topic + " at " + DateTime.Now.ToString("HH:mm:ss.ffff tt"),true);
                    //var payload = e.ApplicationMessage.ConvertPayloadToString();
                    this.onMessageRecievedToSyncronize(topicPublic, e, this);
                } else {
                    //lock (this.mydataSent)
                    //{
                    try {
                        this.mydataSent.Remove(topicPublic);
                    }
                    catch {

                    }
                    //}
                }
            };
        }

        private bool wasMEClient(string topic, byte[] data) {
            return false;
            bool returnData = returnData = false;
            //lock (this.mydataSent)
            //{
            try {
                returnData = (this.mydataSent.ContainsKey(topic) && this.mydataSent[topic].SequenceEqual(data));
            }
            catch (Exception e) {
                returnData = false;
            }
            //}
            return returnData;
        }
        private int tryReconnectRemote() {
            if (this.RemoteConnectionAttemptCount > 10 && !this.runForever) {
                this.logAction("Remote Connection Atempt Exceded", false, EventLogEntryType.Error);
                //throw new Exception("Cannot connect to remote");
                return -1;
            }
            this.logAction("Will Recconect to Remote " + this.remoteAddress + ":" + this.remotePort, false, EventLogEntryType.Warning);
            this.RemoteConnectionAttemptCount++;
            try {
                this.connectToRemote(true);
                return 0;
            }
            catch (Exception e) {
                return this.tryReconnectRemote();
            }


        }


        public void subscribe(string topic,byte qosLevel) {
            this.RemoteClient.Subscribe(new string[1] { topic }, new byte[1] { qosLevel });
        }
        public int disconnectToRemoteAsync() {
            if (!this.RemoteClient.IsConnected) {
                return -1;
            }
            this.clientDisconnectRequest = true;
            return this.disconnectFromMQTT();

        }

        private int disconnectFromMQTT() {
            if (this.DisconnectRequest == false) {
                this.DisconnectRequest = true;
                this.RemoteClient.Disconnect();
                return 0;
            }
            return -1;
        }
    }


}
