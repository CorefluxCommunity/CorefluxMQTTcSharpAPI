using CorefluxCSharpAPI.API.Models.Enums;
using CorefluxCSharpAPI.API.Services.Interfaces;
using System;
using System.IO;
using System.Net;

namespace CorefluxCSharpAPI.API.Services
{
    public class CommunicationService : ICommunicationService
    {
        public string PostInformation(string url) => makeHttpRequest(url, HttpVerb.POST);


        public string GetInformation(string url) => makeHttpRequest(url, HttpVerb.GET);


        private string makeHttpRequest(string url, HttpVerb verb)
        {

            string strResponseValue = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = verb.ToString();
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                //Proecess the resppnse stream... (could be JSON, XML or HTML etc...
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
                strResponseValue = string.Format("{\"errorMessages\":[\"{0}\"],\"errors\":{}}", ex.Message.ToString());
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
