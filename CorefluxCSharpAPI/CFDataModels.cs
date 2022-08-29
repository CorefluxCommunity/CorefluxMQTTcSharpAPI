using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Coreflux.API.DataModels
{

    #region JsonConverter for ParameterType

    //public class ParameterTypeConverter : JsonConverter
    //{
    //    public override bool CanConvert(Type objectType)
    //    {
    //        return typeof(ParameterType).IsAssignableFrom(objectType);
    //    }

    //    public override object ReadJson(JsonReader reader,
    //        Type objectType, object existingValue, JsonSerializer serializer)
    //    {
    //        JObject jo = JObject.Load(reader);
    //        //var JTokenValue = jo.GetValue("Value");
    //        var JTokenValue = jo.ToString();

    //        bool? isParameterType = (bool?)jo["type"];

    //        ParameterType item = new ParameterType();

    //        if (JTokenValue.Equals("{\r\n  \"Value\": \"String\"\r\n}"))
    //        {
    //            item = new ParameterType("String");
    //        }
    //        else if (JTokenValue.Equals("{\r\n  \"Value\": \"Bool\"\r\n}"))
    //        {
    //            item = new ParameterType("Bool");
    //        }
    //        else if (JTokenValue.Equals("{\r\n  \"Value\": \"Int\"\r\n}"))
    //        {
    //            item = new ParameterType("Int");
    //        }
    //        else if (JTokenValue.Equals("{\r\n  \"Value\": \"Float\"\r\n}"))
    //        {
    //            item = new ParameterType("Float");
    //        }

    //        serializer.Populate(jo.CreateReader(), item);

    //        return item;
    //    }

    //    public override bool CanWrite
    //    {
    //        get { return false; }
    //    }

    //    public override void WriteJson(JsonWriter writer,
    //        object value, JsonSerializer serializer)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    #endregion


    public class User
    {
    
        public string username { get; set; } = "";

        public string password { get; set; } = "";


        public bool RemoteConnection { get; set; } = false;

        public string name { get; set; } = "";
    }

    [DataContract]
    public class LoginData
    {

        public string username { get; set; } = "";

        public string password { get; set; } = "";
    }

    public class Application // PREVIOUS TYPO: AppC
    {

        public string code { get; set; }

        public string name { get; set; } = "";
        public List<KeyValuePair<string, string>> images { get; set; } = null;

        public string description { get; set; } = "";

        public List<Parameter> defaultConfig { get; set; } = null;

        public string codeParameterName { get; set; } = null;

        public int order { get; set; } = 0;

        public int availableSlots { get; set; } = 0;  // PREVIOUS TYPO: avaibleSlots
    
        public int usedSlots { get; set; } = 0;
        //[DataMember]
        //public string versionNumber { get; set; } = null;
    
        public List<KeyValuePair<string, List<KeyValuePair<string, string>>>> socialMedia { get; set; }

        public App toAPP(bool isGlobal)
        {
            var app = new App()
            {
                code = this.code,
                name = this.name,
                description = this.description,
                defaultConfig = this.defaultConfig,
                codeParameterName = this.codeParameterName,
                order = this.order,
                usedSlots = this.usedSlots,
                availableSlots = this.availableSlots
                //socialMedia = this.socialMedia
            };

            if (isGlobal)
            {
                App.currentAPPS.Add(app);
            }

            if (this.socialMedia != null)
            {
                var dic = new Dictionary<string, Dictionary<string, string>>();

                foreach (var kp in this.socialMedia)
                {
                    dic.Add(kp.Key, kp.Value.ToDictionary(x => x.Key, x => x.Value));
                }

                app.socialMedia = dic;
            }

            app.images = this.images.ToDictionary(x => x.Key, x => x.Value);
            return app;
        }
    }
    public class App
    {
        public static List<App> currentAPPS = new List<App>();
        public enum mediaType
        {
            Youtube,
            Manual,
            Medium,
            TopUp
        }
    
        public string code { get; set; }

        public string name { get; set; } = "";

        public bool official { get; set; } = false;

        public Dictionary<string, string> images { get; set; } = null;

        public string description { get; set; } = "";

        public List<Parameter> defaultConfig { get; set; } = null;

        public string codeParameterName { get; set; } = null;

        public int order { get; set; } = 0;

        public string architecture { get; set; } = "x86";

        public int usedSlots { get; set; } = 0;
    
        public int availableSlots { get; set; } = 0;
        public string executableFileName { get; set; }

        public Dictionary<string, Dictionary<string, string>> socialMedia { get; set; }
        public string getMedia(mediaType type)
        {
            if (this.socialMedia == null) return null;

            try
            {
                switch (type)
                {
                    case mediaType.Manual:
                        if (!this.socialMedia["en-US"].ContainsKey("manual")) return null;
                        var A = this.socialMedia["en-US"];
                        var B = A["manual"];
                        return B;

                    case mediaType.Youtube:
                        if (!this.socialMedia["en-US"].ContainsKey("youtube")) return null;
                        var C = this.socialMedia["en-US"];
                        var D = C["youtube"];
                        return D;

                    case mediaType.Medium:
                        if (!this.socialMedia["en-US"].ContainsKey("medium")) return null;
                        var E = this.socialMedia["en-US"];
                        var F = E["medium"];
                        return F;

                    case mediaType.TopUp:
                        if (!this.socialMedia["en-US"].ContainsKey("topup")) return null;
                        var G = this.socialMedia["en-US"];
                        var H = G["topup"];
                        return H;
                }
            }
            catch (Exception e)
            {
                return null;
            }

            return null;
        }
    }

    //public class AppStore : App
    //{
    //    [DataMember]
    //    public bool dataAvaible { get; set; }
    //    [DataMember]
    //    public string storeID { get; set; } = "";
    //    [DataMember]
    //    public int accountMaxQty { get; set; }
    //    [DataMember]
    //    public float discount { get; set; }
    //    [DataMember]
    //    public float price { get; set; }

    //    [DataMember]
    //    public string itemRef { get; set; } = null;

    //    [DataMember]
    //    public float taxValue { get; set; }

    //    public StoreElemGUI myGameObject;

    //    public float GetPrice()
    //    {
    //        return this.price * (1 + this.taxValue);
    //    }
    //}


    public class Parameter
    {

        public string name { get; set; } = "";
        public string label { get; set; } = "";

        // TODO: REQUIRES SOLUTION

        [NonSerialized]
        public ParameterType type = null;

        //[JsonConverter(typeof(ParameterTypeConverter))]
        //[DataMember]
        //public ParameterType type { get; set; } = null;


        public string value { get; set; } = null;
     
        public string validatorRegEx { get; set; } = null;

        public bool optional { get; set; } = false;

        public string hint { get; set; } = null;
    }


    public class SignInData
    {
        
        public string username { get; set; } = "";

        public string password { get; set; } = "";

        public string passwordConfirm { get; set; } = "";

        public string name { get; set; } = "";
    }


    public class ParameterType
    {
        private readonly string[] valid = new string[] { "String", "Int", "Float", "Bool" };

      
        public string Value { get; set; }

        public ParameterType() : this("String") { }

        public ParameterType(string value)
        {
            if (!this.valid.Contains(value))
            {
                throw new Exception("Must be a valid type.");
            }

            this.Value = value;
        }

   
        public static ParameterType String
        {
            get
            {
                return new ParameterType("String");
            }
        }

        public static ParameterType Int
        {
            get
            {
                return new ParameterType("Int");
            }
        }

        public static ParameterType Float
        {
            get
            {
                return new ParameterType("Float");
            }
        }

        public static ParameterType Bool
        {
            get
            {
                return new ParameterType("Bool");
            }
        }
    }
   
    public class AppInstance // PREVIOUS TYPO: InstanceC
    {

        public Application app { get; set; }

        public string code { get; set; }

        public string name { get; set; } = "";

        public InstanceStatus status { get; set; }

        public Instance toInstance()
        {
            return new Instance()
            {
                code = this.code,
                name = this.name,
                app = this.app.toAPP(false),
                status = this.status
            };
        }
    }

    public class Instance
    {

        public App app { get; set; }
        public string code { get; set; }
        public string name { get; set; } = "";
        public InstanceStatus status { get; set; }

        public string getLabel()
        {
            //TODO change
            return this.code;
        }
        public string getName()
        {
            //TODO change
            try
            {
                return this.name.Split('_')[0];

            }
            catch
            {

            }
            return this.name;
        }
        public App getAppGlobal()
        {
            return App.currentAPPS.Find(appT => appT.code.Equals(this.app.code));
        }
    }
    public enum InstanceStatus
    {
        Started,
        Stopped,
        Starting,
        Stopping,
        Error
    }
}

