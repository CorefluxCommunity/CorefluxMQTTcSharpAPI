using Coreflux.API.cSharp.Networking.MQTT;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CF_TempToCSV
{
    class CF_TempToCSV
    {
        static void Main(string[] args)
        {
            var myData = new List<dynamic>();
            
            // Initialize controller, passing correct IP and Port as arguments
            var MyID = MQTTController.Init("127.0.0.1", 1883, (topic,data) =>
            {
                DateTime filedate = DateTime.Now;
                filedate.AddDays(-1);
                string filename = filedate.ToString("yyyyMMdd") + ".csv";

                if (!File.Exists(filename))
                {
                    // Create a file to write to
                    string[] createText = { "sep=,", "Temperature" };
                    File.WriteAllLines(filename, createText);
                }

                // Text is always added, making file longer over time if not deleted
                string appendText = data.getMessage()+ Environment.NewLine;
                File.AppendAllText(filename, appendText);

            }, null);

            // Subscribing to the appropriate MQTT topic
            MQTTController.Subscribe(MyID, "Temperature/PAC", 1);

            Console.WriteLine("Connected: " + MQTTController.Connected(MyID));
            Console.ReadLine();
        }
        
        private static void OnNewMQTTMessage(string String, Coreflux.API.cSharp.Networking.MQTT.Messages.MQTTMsgPublishedEventArgs data)
        {

        }
    }
}
