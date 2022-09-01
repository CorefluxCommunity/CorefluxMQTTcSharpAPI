using Coreflux.API.Networking.MQTT;
using CorefluxCSharpAPI.API.Services;
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
        private MQTTControllerInstance MQTTControllerInstance;
        public bool isConnected;
        public int Teste;
        public Coreflux.API.Client API = new Coreflux.API.Client("localhost");
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            MQTTControllerInstance = new MQTTControllerInstance();

            MQTTControllerInstance.OnConnect += MQTTController_OnConnect;
            MQTTControllerInstance.OnDisconnect += MQTTController_OnDisconnect;
            MQTTControllerInstance.NewPayload += MQTTController_NewPayload;
            MQTTControllerInstance.PersistentConnection = true;
            AlreadyConnectedOneTime = false;
            isConnected = false;
        }


        private void MQTTController_NewPayload(MQTTNewPayload obj)
        {
            _logger.LogInformation("received" + obj.topic + " , " + obj.payload + " @ {time} ", DateTimeOffset.Now);
        }

        private void MQTTController_OnDisconnect()
        {

            //   MQTTController.StartAsync("127.0.0.1", timeOut: 5, keepAlive: 1).Wait();
            _logger.LogInformation("Disconnected of broker {time}", DateTimeOffset.Now);
            isConnected = false;
            //    ReConnect();

        }

        private void MQTTController_OnConnect()
        {
            _logger.LogInformation("Connected to broker {time}", DateTimeOffset.Now);
            if (!AlreadyConnectedOneTime)
            {
                
                _logger.LogInformation("Get inside MQTT_Controller");
                
            }
            AlreadyConnectedOneTime = true;
            isConnected = true;
        }

        private async void ReConnect()
        {
            try
            {
                await MQTTControllerInstance.StartAsync("127.0.0.1", timeOut: 5, keepAlive: 1);
            }
            catch
            {
                _logger.LogInformation("Failed to find the  broker {time}", DateTimeOffset.Now);
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                MQTTControllerInstance.ClientName = "Test";
                await MQTTControllerInstance.StartAsync("127.0.0.1", port: 1883, timeOut: 5, keepAlive: 5, mqttSecure: false);
            }
            catch
            {
                _logger.LogInformation("Failed to find the  broker {time}", DateTimeOffset.Now);
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                if (isConnected)
                {
                    //Get the last value of the topic received 
                    var t = MQTTControllerInstance.GetDataAsync("CF/GetTest").GetAwaiter();
                    _logger.LogInformation("Was able to subscribe CF/GetTest with {string} ", t.GetResult());
                    Task.Delay(100).Wait();
                    //Set the new value of topic CF/Test
                    var t1 = MQTTControllerInstance.SetDataAsync("CF/Test", "test", qoslevel: 1);
                    t1.Wait(100);
                    var result = t1.GetAwaiter().GetResult();
                    if (result != null)
                    {
                        if (result.ReasonFeedback == MQTTPublishFeedback.FeedbackType.PublishSucess)
                        {
                            _logger.LogInformation("Was able to publish CF/Test by publish sucess @ {time} ", DateTimeOffset.Now);
                        }
                        else
                        {
                            _logger.LogInformation("Failed  to publish CF/Test by publish failed @ {time} ", DateTimeOffset.Now);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Failed  to publish CF/Test by null @ {time} ", DateTimeOffset.Now);
                }
          
               
            }
        }
    }
}
