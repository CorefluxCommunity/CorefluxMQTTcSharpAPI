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
        private readonly ILogger<Worker> _logger;
        private MQTTControllerInstance MQTTControllerInstance;
        public bool isConnected;
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            MQTTControllerInstance = new MQTTControllerInstance();
            MQTTControllerInstance.OnConnect += MQTTControllerInstance_OnConnect;
            MQTTControllerInstance.OnDisconnect += MQTTControllerInstance_OnDisconnect;
            MQTTControllerInstance.NewPayload += MQTTControllerInstance_NewPayload;
            isConnected = false;
        }

        private void MQTTControllerInstance_NewPayload(MQTTNewPayload obj)
        {
            _logger.LogInformation("received" + obj.topic+ " , "+ obj.payload +" @ {time} ", DateTimeOffset.Now);
        }

        private void MQTTControllerInstance_OnDisconnect()
        {
            _logger.LogInformation("Disconnected of broker {time}", DateTimeOffset.Now);
            isConnected = false;
        //    ReConnect();

        }

        private void MQTTControllerInstance_OnConnect()
        {
            _logger.LogInformation("Connected to broker {time}", DateTimeOffset.Now);
            isConnected = true;
        }

        private async void ReConnect()
        {
            try
            {
                await MQTTControllerInstance.StartAsync("127.0.0.1");
            }
            catch
            {
                _logger.LogInformation("Failed to find the  broker {time}", DateTimeOffset.Now);
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { 
            await MQTTControllerInstance.StartAsync("127.0.0.1");
            }
            catch
                {
                _logger.LogInformation("Failed to find the  broker {time}", DateTimeOffset.Now);
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                if(isConnected)
                {
                 
                    await MQTTControllerInstance.SetDataAsync("teste", "1234");
                    var t=MQTTControllerInstance.GetDataAsync("teste").GetAwaiter();
                    var q= t.GetResult();
                    _logger.LogInformation("received" + q + " @ {time} ", DateTimeOffset.Now);
                }
                else
                {
                    try
                    {
                        await MQTTControllerInstance.StartAsync("127.0.0.1");
                    }
                    catch
                    {
                        _logger.LogInformation("Failed to find the  broker {time}", DateTimeOffset.Now);
                    }
                }
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
