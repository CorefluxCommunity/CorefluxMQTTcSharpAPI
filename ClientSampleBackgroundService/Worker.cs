using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coreflux.API.Networking.MQTT;

namespace ClientSampleBackgroundService
{
    public class Worker : BackgroundService
    {
        private bool AlreadyConnectedOneTime;
        private readonly ILogger<Worker> _logger;
        private MQTTControllerInstance MQTTControllerInstance;
        public bool isConnected;
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
                //var t = MQTTController.GetDataAsync("teste").GetAwaiter();
                _logger.LogInformation("Get inside MQTT_Controller");
                var t = MQTTControllerInstance.GetDataAsync("HV/RobotComFilho").GetAwaiter();
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
                MQTTControllerInstance.ClientName = "Teste";
                await MQTTControllerInstance.StartAsync("127.0.0.1", port: 1883, timeOut: 5, keepAlive: 5, mqttSecure: false);
            }
            catch
            {
                _logger.LogInformation("Failed to find the  broker {time}", DateTimeOffset.Now);
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                //    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                if (isConnected)
                {

                    // await MQTTController.SetDataAsync("HV/RobotComFilho", "weeeee");
                    //          _logger.LogInformation("isConnected subscribe");
                    var t = MQTTControllerInstance.GetDataAsync("HV/RobotComFilho");

                     var teste=t.GetAwaiter().GetResult();
                    //            _logger.LogInformation("subscribe finished");
                    
                    _logger.LogInformation("received " + teste + " @ {time} ", DateTimeOffset.Now);
                }
                //         _logger.LogInformation("Wait 1000ms");
                Task.Delay(100).Wait();
                //         _logger.LogInformation("Publish Data Async");
                var t1 = MQTTControllerInstance.SetDataAsync("HV/Teste", "teste", qoslevel: 1);
                //          _logger.LogInformation("Publish Confirm ");
                t1.Wait(100);
                //        _logger.LogInformation("Publish Done check results ");
                var result = t1.GetAwaiter().GetResult();
                if (result != null)
                {
                    if (result.ReasonFeedback == MQTTPublishFeedback.FeedbackType.PublishSucess)
                    {
                        _logger.LogInformation("Was able to publish HV/Teste by publish sucess @ {time} ", DateTimeOffset.Now);
                    }
                    else
                    {
                        _logger.LogInformation("Failed  to publish HV/Teste by publish failed @ {time} ", DateTimeOffset.Now);
                    }
                }
                else
                {
                    _logger.LogInformation("Failed  to publish HV/Teste by null @ {time} ", DateTimeOffset.Now);
                }
                //          _logger.LogInformation("Publish Results decision");

                //else
                //{
                //    try
                //    {
                //        await MQTTControllerInstance.StartAsync("127.0.0.1");
                //    }
                //    catch
                //    {
                //        _logger.LogInformation("Failed to find the  broker {time}", DateTimeOffset.Now);
                //    }
                //}

            }
        }
    }
}
