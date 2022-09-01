using Coreflux.API;
using CorefluxCSharpAPI.API.Services.Interfaces;
using Moq;
using System;
using Xunit;

namespace CorefluxCSharpAPI.API.Tests
{
    public class Client_StopInstance_Tests
    {
        [Fact(DisplayName = "Stop Instance. Instance not found")]
        [Trait("StopInstance", "Test the operations of start/stop and obtain instances")]
        public void InstanceNotFound()
        {
            // Act
            var communicationServiceMock = new Mock<ICommunicationService>();
            communicationServiceMock.Setup(x => x.GetInformation(It.IsAny<string>())).Returns(string.Empty);

            // Arrange
            Client client = new Client("localhost", communicationServiceMock.Object);
            bool state = client.StopInstance(It.IsAny<string>());

            // Assert
            Assert.False(state);
        }

        [Fact(DisplayName = "Stop Instance. Instance failed to start found")]
        [Trait("StopInstance", "Test the operations of start/stop and obtain instances")]
        public void InstanceFailedToStart()
        {
            // Act
            var communicationServiceMock = new Mock<ICommunicationService>();
            communicationServiceMock.Setup(x => x.GetInformation(It.IsAny<string>()))
                .Returns("[{\"app\":{\"architecture\":\"win32\",\"avaibleSlots\":2," +
                "\"code\":\"coreflux_broker_full\",\"codeParameterName\":\"InstanceID\",\"defaultConfig\"" +
                ":[{\"hint\":null,\"label\":\"BindtoIP\",\"name\":\"ipBind\",\"optional\":false,\"type\":{\"Value\":\"String\"}," +
                "\"validatorRegEx\":\"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$\",\"value\":\"0.0.0.0\"},{\"hint\":\"Thisfieldonlyallownumbersbetween(1-65535).\"" +
                ",\"label\":\"TCPPort\",\"name\":\"TcpPort\",\"optional\":false,\"type\":{\"Value\":\"Int\"}," +
                "\"validatorRegEx\":\"^(?:[1-9]|[1-5]?[0-9]{2,4}|6[1-4][0-9]{3}|65[1-4][0-9]{2}|655[1-2][0-9]|6553[1-5])$\"," +
                "\"value\":\"1883\"},{\"hint\":null,\"label\":\"Clienttimeout(ms)\",\"name\":\"ClientTimeoutMS\"," +
                "\"optional\":false,\"type\":{\"Value\":\"Int\"},\"validatorRegEx\":\"^\\\\d+$\",\"value\":\"5000\"}," +
                "{\"hint\":\"Messageswillbequeuedifclientdisconnects.\",\"label\":\"Persistentsessions\",\"name\":\"PersistentSessions\",\"optional\":false,\"type\":{\"Value\":\"Bool\"}" +
                ",\"validatorRegEx\":\"^(?:true)|(?:false)$\",\"value\":\"True\"},{\"hint\":\"Numberofdisconnectionstoremember.\",\"label\":\"Connectionbacklog\",\"name\":\"ConnectionBacklog\",\"optional\":false,\"type\":{\"Value\":\"Int\"}" +
                ",\"validatorRegEx\":\"^\\\\d+$\",\"value\":\"1000\"},{\"hint\":\"Numberofmessagestobequeued.\",\"label\":\"Maximumpendingmessages\",\"name\":\"MaxPendingMessages\",\"optional\":false,\"type\":{\"Value\":\"Int\"}" +
                ",\"validatorRegEx\":\"^\\\\d+$\",\"value\":\"5000\"},{\"hint\":null,\"label\":\"InstanceID\",\"name\":\"InstanceID\",\"optional\":true,\"type\":{\"Value\":\"String\"},\"validatorRegEx\":\"^(?:\\\\w|\\\\d)+$\",\"value\":null}]" +
                ",\"description\":\"ThisappallowyoutocentralizeallyourdataintoaMQTTserver.\",\"executableFileName\":\"CorefluxMQTTBroker.exe\",\"images\":[{\"Key\":\"square.md\",\"Value\":\"square.md.png\"}" +
                ",{\"Key\":\"logo.md\",\"Value\":\"icon.md.png\"}],\"key\":\"coreflux_broker\",\"name\":\"MQTTBroker\",\"official\":true,\"order\":0,\"priority\":2147483647,\"socialMedia\":[{\"Key\":\"en-US\",\"Value\":[{\"Key\":\"manual\",\"Value\":\"https:\\/\\/medium.com\\/coreflux-blog\\/mqtt-broker-asset-coreflux-511acecac3f7\"}" +
                ",{\"Key\":\"medium\",\"Value\":\"https:\\/\\/medium.com\\/coreflux-blog\\/practical-iot-fun-ctionality-at-the-office-with-coreflux-mqtt-unity-59982d90f6fa\"},{\"Key\":\"topup\",\"Value\":\"https:\\/\\/buy.stripe.com\\/eVacOt4pI6t4eTC004\"},{\"Key\":\"youtube\",\"Value\":\"https:\\/\\/www.youtube.com\\/watch?v=5eVhCcnbMMs\"}]}" +
                ",{\"Key\":\"pt-PT\",\"Value\":[{\"Key\":\"manual\",\"Value\":\"https:\\/\\/coreflux.org\\/\"},{\"Key\":\"medium\",\"Value\":\"https:\\/\\/medium.com\\/coreflux-blog\\/mqtt-broker-asset-coreflux-511acecac3f7\"},{\"Key\":\"topup\",\"Value\":\"https:\\/\\/buy.stripe.com\\/eVacOt4pI6t4eTC004\"}" +
                ",{\"Key\":\"youtube\",\"Value\":\"https:\\/\\/www.youtube.com\\/watch?v=5eVhCcnbMMs\"}]}],\"usedSlots\":2},\"code\":\"9OPd\",\"companyID\":\"-NAiOHHU3uSq_CauJyf3\",\"modelTrial\":false,\"name\":\"Coreflux.MQTT.Broker_9OPd\",\"status\":1}]");

            communicationServiceMock.Setup(x => x.PostInformation(It.IsAny<string>())).Returns("{\"errorMessages\":[\"Error!\"],\"errors\":{}}");

            // Arrange
            Client client = new Client("localhost", communicationServiceMock.Object);

            // Assert
            Assert.Throws<Exception>(() => client.StopInstance("9OPd"));
        }


        [Fact(DisplayName = "Stop Instance. Instance found")]
        [Trait("StopInstance", "Test the operations of start/stop and obtain instances")]
        public void InstanceStarted()
        {
            // Act
            var communicationServiceMock = new Mock<ICommunicationService>();
            communicationServiceMock.Setup(x => x.GetInformation(It.IsAny<string>()))
                .Returns("[{\"app\":{\"architecture\":\"win32\",\"avaibleSlots\":2," +
                "\"code\":\"coreflux_broker_full\",\"codeParameterName\":\"InstanceID\",\"defaultConfig\"" +
                ":[{\"hint\":null,\"label\":\"BindtoIP\",\"name\":\"ipBind\",\"optional\":false,\"type\":{\"Value\":\"String\"}," +
                "\"validatorRegEx\":\"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$\",\"value\":\"0.0.0.0\"},{\"hint\":\"Thisfieldonlyallownumbersbetween(1-65535).\"" +
                ",\"label\":\"TCPPort\",\"name\":\"TcpPort\",\"optional\":false,\"type\":{\"Value\":\"Int\"}," +
                "\"validatorRegEx\":\"^(?:[1-9]|[1-5]?[0-9]{2,4}|6[1-4][0-9]{3}|65[1-4][0-9]{2}|655[1-2][0-9]|6553[1-5])$\"," +
                "\"value\":\"1883\"},{\"hint\":null,\"label\":\"Clienttimeout(ms)\",\"name\":\"ClientTimeoutMS\"," +
                "\"optional\":false,\"type\":{\"Value\":\"Int\"},\"validatorRegEx\":\"^\\\\d+$\",\"value\":\"5000\"}," +
                "{\"hint\":\"Messageswillbequeuedifclientdisconnects.\",\"label\":\"Persistentsessions\",\"name\":\"PersistentSessions\",\"optional\":false,\"type\":{\"Value\":\"Bool\"}" +
                ",\"validatorRegEx\":\"^(?:true)|(?:false)$\",\"value\":\"True\"},{\"hint\":\"Numberofdisconnectionstoremember.\",\"label\":\"Connectionbacklog\",\"name\":\"ConnectionBacklog\",\"optional\":false,\"type\":{\"Value\":\"Int\"}" +
                ",\"validatorRegEx\":\"^\\\\d+$\",\"value\":\"1000\"},{\"hint\":\"Numberofmessagestobequeued.\",\"label\":\"Maximumpendingmessages\",\"name\":\"MaxPendingMessages\",\"optional\":false,\"type\":{\"Value\":\"Int\"}" +
                ",\"validatorRegEx\":\"^\\\\d+$\",\"value\":\"5000\"},{\"hint\":null,\"label\":\"InstanceID\",\"name\":\"InstanceID\",\"optional\":true,\"type\":{\"Value\":\"String\"},\"validatorRegEx\":\"^(?:\\\\w|\\\\d)+$\",\"value\":null}]" +
                ",\"description\":\"ThisappallowyoutocentralizeallyourdataintoaMQTTserver.\",\"executableFileName\":\"CorefluxMQTTBroker.exe\",\"images\":[{\"Key\":\"square.md\",\"Value\":\"square.md.png\"}" +
                ",{\"Key\":\"logo.md\",\"Value\":\"icon.md.png\"}],\"key\":\"coreflux_broker\",\"name\":\"MQTTBroker\",\"official\":true,\"order\":0,\"priority\":2147483647,\"socialMedia\":[{\"Key\":\"en-US\",\"Value\":[{\"Key\":\"manual\",\"Value\":\"https:\\/\\/medium.com\\/coreflux-blog\\/mqtt-broker-asset-coreflux-511acecac3f7\"}" +
                ",{\"Key\":\"medium\",\"Value\":\"https:\\/\\/medium.com\\/coreflux-blog\\/practical-iot-fun-ctionality-at-the-office-with-coreflux-mqtt-unity-59982d90f6fa\"},{\"Key\":\"topup\",\"Value\":\"https:\\/\\/buy.stripe.com\\/eVacOt4pI6t4eTC004\"},{\"Key\":\"youtube\",\"Value\":\"https:\\/\\/www.youtube.com\\/watch?v=5eVhCcnbMMs\"}]}" +
                ",{\"Key\":\"pt-PT\",\"Value\":[{\"Key\":\"manual\",\"Value\":\"https:\\/\\/coreflux.org\\/\"},{\"Key\":\"medium\",\"Value\":\"https:\\/\\/medium.com\\/coreflux-blog\\/mqtt-broker-asset-coreflux-511acecac3f7\"},{\"Key\":\"topup\",\"Value\":\"https:\\/\\/buy.stripe.com\\/eVacOt4pI6t4eTC004\"}" +
                ",{\"Key\":\"youtube\",\"Value\":\"https:\\/\\/www.youtube.com\\/watch?v=5eVhCcnbMMs\"}]}],\"usedSlots\":2},\"code\":\"9OPd\",\"companyID\":\"-NAiOHHU3uSq_CauJyf3\",\"modelTrial\":false,\"name\":\"Coreflux.MQTT.Broker_9OPd\",\"status\":1}]");

            communicationServiceMock.Setup(x => x.PostInformation(It.IsAny<string>())).Returns(string.Empty);

            // Arrange
            Client client = new Client("localhost", communicationServiceMock.Object);
            bool state = client.StopInstance("9OPd");

            // Assert
            Assert.True(state);
        }
    }
}
