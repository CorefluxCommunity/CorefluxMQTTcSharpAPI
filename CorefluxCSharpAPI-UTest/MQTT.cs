using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;


namespace Coreflux.API.Networking.MQTT.UTest
{
    [TestClass]
    public class MQTTTest
    {

        [TestMethod]
        public async void StaticMqttControllerStartLocalMQTT()
        {
            var teste=await StaticMqttControllerStart("127.0.0.1");
            Assert.AreEqual(true, teste);
        }


        private async Task<bool> StaticMqttControllerStart(string IP, int port = 1883, string user = "", string password = "", bool mqttSecure = false, bool usingWebSocket = false)
        {
            bool isConnectedByEvent = false;
            bool isConnectedByCheckConnect = false;


            MQTTController.OnConnect += () => { 
                isConnectedByEvent = false;
            };

            MQTTController.Start(IP,port,user,password,mqttSecure,usingWebSocket);

            await Task.Delay(2000);
            isConnectedByCheckConnect = MQTTController.Connected();

            
            Assert.AreEqual(true, isConnectedByCheckConnect);

            return isConnectedByCheckConnect && isConnectedByEvent;

        }
    }
}
