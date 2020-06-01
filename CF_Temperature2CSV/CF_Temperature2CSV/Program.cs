using Coreflux.API.cSharp.Networking.MQTT;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CF_Temperature2CSV
{
    class Program
    {
        static void Main(string[] args)
        {
            var myData = new List<dynamic>();
            
            var MyID = MQTTController.Init("192.168.100.186", 1883, (topic,data)=>
            {
                
                DateTime filedate = DateTime.Now;
                filedate.AddDays(-1);
                string filename = filedate.ToString("yyyyMMdd") + ".csv";

                if (!File.Exists(filename))
                {
                    // Create a file to write to.
                    string[] createText = { "sep=,", "Temperature" };
                    File.WriteAllLines(filename, createText);
                }

                // This text is always added, making the file longer over time
                // if it is not deleted.
                string appendText = data.getMessage()+ Environment.NewLine;
                File.AppendAllText(filename, appendText);
                



            }, null);

            MQTTController.Subscribe(MyID, "Temperature/PAC", 1);


            Console.WriteLine("Connected " + MQTTController.Connected(MyID));
            Console.ReadLine();

        }
        


        private static void OnNewMQTTMessage(string String, Coreflux.API.cSharp.Networking.MQTT.Messages.MQTTMsgPublishedEventArgs data)
        {

        }
    }
}
