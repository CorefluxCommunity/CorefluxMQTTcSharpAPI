using Coreflux.API.DataModels;
using CorefluxCSharpAPI.API.Services;
using CorefluxCSharpAPI.API.Services.Interfaces;
using Newtonsoft.Json;
using System;

namespace Coreflux.API
{
    public class Client
    {
        private readonly string _ip;
        private readonly ICommunicationService _communicationService;

        public Client(string ip, ICommunicationService communicationService = null)
        {
            _ip = ip;
            _communicationService = communicationService ?? new CommunicationService();
        }

        /// <summary>
        /// If null is returned, something failed on obtaining the instances.
        /// </summary>
        /// <returns></returns>
        public AppInstance[] GetInstances()
        {
            string response = _communicationService.GetInformation(string.Format("https://{0}:9501/CorefluxCentral/instances", _ip));
            return string.IsNullOrEmpty(response) ? null : JsonConvert.DeserializeObject<AppInstance[]>(response);
        }


        public bool StartInstance(string id)
        {
            if (FindAppInInstances(id))
            {
                // VERBS: start, stop, run, install, uninstall
                string response = _communicationService.PostInformation(string.Format("https://{0}:9501/CorefluxCentral/instances/{1}/start", _ip, id));
                return string.IsNullOrEmpty(response) ? true : throw new Exception(response);
            }
            return false;
        }

        public bool StopInstance(string id)
        {
            if (FindAppInInstances(id))
            {
                var response = _communicationService.PostInformation(string.Format("https://{0}:9501/CorefluxCentral/instances/{1}/stop", _ip, id));
                return string.IsNullOrEmpty(response) ? true : throw new Exception(response);
            }
            return false;
        }

        private bool FindAppInInstances(string instance)
        {
            AppInstance[] apps = GetInstances();
            if (apps == null)
                return false;

            foreach (AppInstance app in apps)
                if (app.code.Equals(instance))
                    return true;

            return false;
        }
    }
}
