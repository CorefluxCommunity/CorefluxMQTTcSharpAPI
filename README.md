
[![NuGet Badge](https://buildstats.info/nuget/CorefluxMQTTcSharpAPI)](https://www.nuget.org/packages/CorefluxMQTTcSharpAPI/)
# Coreflux C# API 
This nuget package is resposible for all integration with C# and Coreflux. 

# Coreflux Start / Stop Asset
 ## Usage

 - Add the necessary namespaces in your project
	 `using Coreflux.API;`
 - Get all installed assets @ our Coreflux Hub and if they are stopped Start them
       
   			Coreflux.API.Client API = new Coreflux.API.Client("localhost");
                        var InstalledAssets = API.GetInstances();
                        
                        foreach(var inst in InstalledAssets)
                        {
                            if(inst.status== Coreflux.API.DataModels.InstanceStatus.Stopped)
                            {
                                API.StartInstance(inst.code);
                            }
                        }
			
 - Get all installed assets @ our Coreflux Hub and if they are started / stop them       
   			
			Coreflux.API.Client API = new Coreflux.API.Client("localhost");
                        var InstalledAssets = API.GetInstances();
                        
                        foreach(var inst in InstalledAssets)
                        {
                            if(inst.status== Coreflux.API.DataModels.InstanceStatus.Started)
                            {
                                API.StopInstance(inst.code);
                            }
                        }
                        
# Coreflux MQTT Managed client 

The MQTT namespace enables MQTT communication within your development.  There is already in place an MQTT Managed client.

## Usage

 - Add the necessary namespaces in your project
	 `using Coreflux.API.Networking.MQTT;`
	 
 - Call the managed mqtt client from Coreflux 
  ` MQTTController.Start("127.0.0.1", 1883);` //using IP with normal  TCP/IP socket
  
  ` MQTTController.Start("cloud.coreflux.org", 1883);` //using dns  with normal  TCP/IP socket
  
` MQTTController.Start("cloud.coreflux.org:8080/mqtt", 8080, "", "", false, true);` //using dns  , without username "", no  password , no TLS and with websocket

  - Connect the events
  ` MQTTController.NewPayload += this.MQTTController_NewPayload;` //Provides a standard reception of subscribed topics
  > This is asynchronous usage
  - Subscribe
  ` string payload=MQTTController.GetData("mytopic/teste");` // provides the payload of a topic directly from the cache of the managed mqtt client
> This is synchronous usage
   - Publish
  ` MQTTController.SetData("mytopic/teste", "payload", 0, false);` // Publishes the value to the topic with the payload . In this case without retain(false) and QOS 0.

## System Compatibility

 - Windows 11 (pre-release tested) , 10, IoT , 7 
 - Linux ( Ubuntu , Raspian, Debian,etc..)
 - Android (Phones,SmartTVs)
 - IOs
 - macOS
 
## Protocols
 - MQTT 3.1  / 3.1.1 /5.00 


## Nuget 

There is a nuget package available [Coreflux Nuget ](https://www.nuget.org/packages/CorefluxMQTTcSharpAPI/) 

## Author
![Coreflux](https://i.imgur.com/JdvJkGY.png)

http://coreflux.org - Coreflux the most advanced industrial IOT platform!




