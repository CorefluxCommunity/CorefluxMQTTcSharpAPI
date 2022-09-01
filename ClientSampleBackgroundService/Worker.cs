using Coreflux.API.Networking.MQTT;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClientSampleBackgroundService
{
    public class Worker : BackgroundService
    {
        private bool AlreadyConnectedOneTime;
        private readonly ILogger<Worker> _logger;
        private MQTTControllerInstance mqttInstance;
        public bool isConnected;
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            mqttInstance = new MQTTControllerInstance();
            mqttInstance.OnConnect += MQTTController_OnConnect;
            mqttInstance.OnDisconnect += MQTTController_OnDisconnect;
            mqttInstance.NewPayload += MQTTController_NewPayload;
            mqttInstance.PersistentConnection = true;
            AlreadyConnectedOneTime = false;
            isConnected = false;
        }


        private void MQTTController_NewPayload(MQTTNewPayload obj)
        {
            _logger.LogInformation(string.Format("Message received on topic {0} , paylod: {1} @ Time: {2}"), obj.topic, obj.payload, DateTimeOffset.Now);
        }

        private void MQTTController_OnDisconnect()
        {
            _logger.LogInformation(string.Format("Disconnected of broker {0}", DateTimeOffset.Now));
            isConnected = false;
        }

        private void MQTTController_OnConnect()
        {
            _logger.LogInformation(string.Format("Connected to broker {0}", DateTimeOffset.Now));
            if (!AlreadyConnectedOneTime) 
                _logger.LogInformation("Get inside MQTT_Controller");
            AlreadyConnectedOneTime = true;
            isConnected = true;
        }

        private async void ReConnect()
        {
            try
            {
                await mqttInstance.StartAsync("127.0.0.1", timeOut: 5, keepAlive: 1);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(string.Format("Failed to find the  broker {0}. Exception: {1}", DateTimeOffset.Now, ex.ToString()));
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ConnectToBroker();

            while (!stoppingToken.IsCancellationRequested)
            {
                if (isConnected)
                {
                    //Get the last value of the topic received 
                    var t = mqttInstance.GetDataAsync("CF/GetTest").GetAwaiter();
                    _logger.LogInformation("Was able to subscribe CF/GetTest with {string} ", t.GetResult());
                    Task.Delay(100).Wait();
                    //Set the new value of topic CF/Test
                    var t1 = mqttInstance.SetDataAsync("CF/Test", "test", qoslevel: 1);
                    t1.Wait(100);
                    var result = t1.GetAwaiter().GetResult();
                    if (result != null)
                    {
                        if (result.ReasonFeedback == MQTTPublishFeedback.FeedbackType.PublishSucess)
                        {
                            _logger.LogInformation(string.Format("Message publish to CF/Test with success @ {0} ", DateTimeOffset.Now));
                        }
                        else
                        {
                            _logger.LogInformation(string.Format("Failed to publish message to CF/Test @ {0} ", DateTimeOffset.Now));
                        }
                    }
                }
                else
                {
                    _logger.LogInformation(string.Format("Failed to publish message to CF/Test. Isn't connected. @ {0} ", DateTimeOffset.Now));
                }
            }
        }

        private async Task ConnectToBroker()
        {
            try
            {
                mqttInstance.ClientName = "Test";
                await mqttInstance.StartAsync("127.0.0.1", port: 1883, timeOut: 5, keepAlive: 5, mqttSecure: false);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(string.Format("Failed to find the broker {0}. Exception {1}", DateTimeOffset.Now, ex.ToString()));
            }
        }
    }
}
