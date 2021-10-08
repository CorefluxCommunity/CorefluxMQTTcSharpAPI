# Coreflux API 
This nuget package is resposible for all integration with C# and Coreflux. 


# Coreflux MQTT

The MQTT namespace enables MQTT communication within your development. 

## Usage

 - Add the necessary namespaces in your project
	 `using Coreflux.API.cSharp.Networking.MQTT;`
	 
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
  

## Author
![Coreflux](https://i.imgur.com/JdvJkGY.png)

http://coreflux.org - Coreflux the most advanced industrial IOT platform!




