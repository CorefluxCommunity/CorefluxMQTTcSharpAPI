using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;

using Coreflux.API.DataModels;

namespace Coreflux.API
{




    public class Client
    {
        public enum Version
        {
            LegacyHTTPS,
            MQTTJWT
        }

        private string ip;

        public Client(string Host, Version version)
        {
            ip= Host;
        }




        public enum httpVerb
        {
            GET,
            POST,
            PUT,
            DELETE
        }
        //var t9b = await test.executeActiononInstance("fOsj", "start"); // EXISTS
        //if (t9b != null)
        //{
        //    _logger.LogInformation(t9b.ToString());
        //}

        public AppInstance[] GetInstances()
        {
            RESTClient rClient = new RESTClient();

            rClient.endPoint = @"https://"+ip+":9501/CorefluxCentral/instances";

            string strJSON = string.Empty;

            strJSON = rClient.makeRequest();


            var returner= JsonConvert.DeserializeObject<AppInstance[]>(strJSON);

            return returner;
        }


        public bool StartInstance(string id)
        {
            bool returner = false;

            if(FindAppInInstances(id))
            {
                // VERBS: start, stop, run, install, uninstall
                RESTClient rClient = new RESTClient();
                rClient.endPoint = @"https://" + ip + ":9501/CorefluxCentral/instances/" + id + "/start";
                rClient.httpMethod = httpVerb.POST;

                var response=rClient.makeRequest();
                if(response=="")
                {
                    returner = true;
                }
                else
                {
                    throw new Exception(response);
                }

            }
            return returner;
        }

        private bool FindAppInInstances(string id)
        {
            bool returner = false;
            AppInstance[] apps = GetInstances();
            foreach (AppInstance app in apps)
            {
                if (app.code.Equals(id))
                {
                    returner = true;
                    break;
                }
            }
            return returner;
        }
        public bool StopInstance(string id)
        {
            bool returner = false;

            if (FindAppInInstances(id))
            {
                // VERBS: start, stop, run, install, uninstall
                RESTClient rClient = new RESTClient();
                rClient.endPoint = @"https://" + ip + ":9501/CorefluxCentral/instances/" + id + "/stop";
                rClient.httpMethod = httpVerb.POST;

                var response = rClient.makeRequest();
                if (response == "")
                {
                    returner = true;
                }
                else
                {
                    throw new Exception(response);
                }

            }
            return returner;
        }

        internal class RESTClient
        {

  
            public string endPoint { get; set; }
            public httpVerb httpMethod { get; set; }

            public RESTClient()
            {
                endPoint = "";
                httpMethod = httpVerb.GET;
            }

            public string makeRequest()
            {

                string strResponseValue = string.Empty;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endPoint);

                request.Method = httpMethod.ToString();

                HttpWebResponse response = null;

                try
                {
                    response = (HttpWebResponse)request.GetResponse();


                    //Proecess the resppnse stream... (could be JSON, XML or HTML etc..._

                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            using (StreamReader reader = new StreamReader(responseStream))
                            {
                                strResponseValue = reader.ReadToEnd();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    strResponseValue = "{\"errorMessages\":[\"" + ex.Message.ToString() + "\"],\"errors\":{}}";
                }
                finally
                {
                    if (response != null)
                    {
                        ((IDisposable)response).Dispose();
                    }
                }

                return strResponseValue;
            }
        }
    }
}
