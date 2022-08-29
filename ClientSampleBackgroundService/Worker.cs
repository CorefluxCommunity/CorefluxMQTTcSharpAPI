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
        public int Teste;
        public Coreflux.API.Client API = new Coreflux.API.Client("localhost", Coreflux.API.Client.Version.LegacyHTTPS);
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
                if (isConnected)
                {
                    Teste++;
                    if (Teste == 50)
                    {
                        Coreflux.API.Client API = new Coreflux.API.Client("localhost", Coreflux.API.Client.Version.LegacyHTTPS);
                        var InstalledAssets = API.GetInstances();
                        
                        foreach(var inst in InstalledAssets)
                        {
                            if(inst.status== Coreflux.API.DataModels.InstanceStatus.Stopped)
                            {
                                API.StartInstance(inst.code);
                            }
                        }


                        var testeJson = Newtonsoft.Json.JsonConvert.SerializeObject(API.GetInstances());
                        _logger.LogInformation(testeJson);
                      
                        try { 
                            API.StartInstance("9T11");
                            _logger.LogInformation("Worked");
                        }
                        catch
                        {
                            _logger.LogInformation("Didn't work");
                        }

                    }
                    if(Teste==100)
                    {
                        var tio = API.StopInstance("9T11");
                        Teste = 0;

                    }
                    var t = MQTTControllerInstance.GetDataAsync("HV/RobotComFilho");
                     var teste=t.GetAwaiter().GetResult();
                    //            _logger.LogInformation("subscribe finished");
                    
                    _logger.LogInformation("received " + teste + " @ {time} ", DateTimeOffset.Now);
                }
                Task.Delay(100).Wait();
                var t1 = MQTTControllerInstance.SetDataAsync("HV/Teste", "teste", qoslevel: 1);
                t1.Wait(100);
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
               
            }
        }
    }
}
