using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Client.Options;

namespace Coreflux.API
{

    public class Central
    {

        private IMqttClient client;
        private string serverAddress = "127.0.0.1"; // replace with your Coreflux server address
        private string username = "root";
        private string pass = "coreflux";
        private string cloudCommand = "$SYS/Coreflux/Cloud/Command";
        private string cloudCommandOutput = "$SYS/Coreflux/Cloud/Command/Output";
        public Central(string Host, string user, string password)
        {
            var factory = new MqttFactory();
            serverAddress = Host;
            username = user;
            pass = password;
            client = factory.CreateMqttClient();
        }

        /// <summary>
        /// Connects to the Coreflux Central.
        /// </summary>
        public async void Connect()
        {
            var options = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer(serverAddress)
                .WithCredentials(username, pass)
                .Build();

            await client.ConnectAsync(options);
        }

        private async void SendCommand(string command)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(cloudCommand)
                .WithPayload(command)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag()
                .Build();

            await client.PublishAsync(message);
        }

        /// <summary>
        /// Subscribes to the given MQTT topic to receive messages from the Coreflux server.
        /// </summary>
        /// <param name="responseTopic">The MQTT topic to subscribe to.</param>
        public async void SubscribeToCommandOutput(string responseTopic)
        {
            await client.SubscribeAsync(new TopicFilterBuilder()
                .WithTopic(responseTopic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

            client.UseApplicationMessageReceivedHandler(e =>
            {
                Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
                Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
                Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
                Console.WriteLine();
            });
        }


        /// <summary>
        /// Sends a login command to the Coreflux Cloud.
        /// </summary>
        /// <param name="account">The account email.</param>
        /// <param name="pass">The password.</param>
        public void CloudLogin(string account, string pass)
        {
            SendCommand($"-L {account} {pass}");
        }

        /// <summary>
        /// Sends a command to install an asset on the Coreflux server.
        /// </summary>
        /// <param name="assetName">The name of the asset to install.</param>
        public void InstallAsset(string assetName)
        {
            SendCommand($"-I {assetName}");

        }
        /// <summary>
        /// Sends a command to list all assets on the Coreflux server.
        /// </summary>
        public void ListAssets()
        {
            SendCommand("-l");
        }

        /// <summary>
        /// Sends a command to uninstall an asset on the Coreflux server.
        /// </summary>
        /// <param name="assetGuid">The GUID of the asset to uninstall.</param>
        public void UninstallAsset(string assetInstanceGuid)
        {
            SendCommand($"-U {assetInstanceGuid}");
        }

        /// <summary>
        /// Sends a command to stop an asset instance on the Coreflux server.
        /// </summary>
        /// <param name="assetInstanceGuid">The GUID of the asset instance to stop.</param>
        public void StopInstance(string assetInstanceGuid)
        {
            SendCommand($"-S {assetInstanceGuid}");
        }

        /// <summary>
        /// Sends a command to start an asset instance on the Coreflux server.
        /// </summary>
        /// <param name="assetInstanceGuid">The GUID of the asset instance to start.</param>
        public void StartAsset(string assetInstanceGuid)
        {
            SendCommand($"-R {assetInstanceGuid}");
        }

    }



}
