﻿using Exiled.API.Features;
using MEC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UncomplicatedCustomItems;
using Unity.Collections.LowLevel.Unsafe;
//using UnityEngine.UIElements;

namespace UncomplicatedCustomItems.Managers
{
    internal class HttpManager
    {
        /// <summary>
        /// The <see cref="CoroutineHandle"/> of the presence coroutine.
        /// </summary>
        public CoroutineHandle PresenceCoroutine { get; internal set; }

        /// <summary>
        /// If <see cref="true"/> the message that confirm that the server is communicating correctly with our APIs has been sent in the console.
        /// </summary>
        public bool SentConfirmationMessage { get; internal set; } = false;

        /// <summary>
        /// The number of errors that has occurred. If this number exceed the <see cref="MaxErrors"/> quote then this feature will be deactivated.
        /// </summary>
        public uint Errors { get; internal set; } = 0;

        /// <summary>
        /// The maximum number of errors that can occur before deactivating the function.
        /// </summary>
        public uint MaxErrors { get; }

        /// <summary>
        /// If <see cref="true"/> this feature is active.
        /// </summary>
        public bool Active { get; internal set; } = false;

        /// <summary>
        /// The prefix of the plugin for our APIs
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        /// The <see cref="HttpClient"/> public istance
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// The UCS APIs endpoint
        /// </summary>
        public string Endpoint { get; } = "https://uci.fcosma.it/api/v2/";

        /// <summary>
        /// An array of response times
        /// </summary>
        public List<float> ResponseTimes { get; } = new();

        /// <summary>
        /// Create a new istance of the HttpManager
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="maxErrors"></param>
        public HttpManager(string prefix, uint maxErrors = 5)
        {
            Prefix = prefix;
            MaxErrors = maxErrors;
            HttpClient = new();
        }

        internal HttpResponseMessage HttpRequest(string url)
        {
            Task<HttpResponseMessage> Response = Task.Run(() => HttpClient.GetAsync(url));

            Response.Wait();

            return Response.Result;
        }

        internal string RetriveString(HttpResponseMessage response)
        {
            return RetriveString(response.Content);
        }

        internal string RetriveString(HttpContent response)
        {
            Task<string> String = Task.Run(response.ReadAsStringAsync);

            String.Wait();

            return String.Result;
        }

        public HttpStatusCode AddServerOwner(string discordId)
        {
            return HttpRequest($"{Endpoint}/owners/add?discorid={discordId}").StatusCode;
        }

        public Version LatestVersion()
        {
            return new(RetriveString(HttpRequest($"{Endpoint}/{Prefix}/version?vts=5")));
        }

        public bool IsLatestVersion()
        {
            if (LatestVersion().CompareTo(Plugin.Instance.Version) != 0)
            {
                return false;
            }

            return true;
        }

        internal bool Presence(out HttpContent httpContent)
        {
            float Start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            HttpResponseMessage Status = HttpRequest($"{Endpoint}/{Prefix}/presence?port={Server.Port}&cores={Environment.ProcessorCount}&ram=0&version={Plugin.Instance.Version}");
            httpContent = Status.Content;
            ResponseTimes.Add(DateTimeOffset.Now.ToUnixTimeMilliseconds() - Start);
            if (Status.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }
            return false;
        }

        internal IEnumerator<float> PresenceAction()
        {
            while (Active && Errors <= MaxErrors)
            {
                if (!Presence(out HttpContent content))
                {
                    Dictionary<string, string> Response = JsonConvert.DeserializeObject<Dictionary<string, string>>(RetriveString(content));
                    Errors++;
                    Log.Warn($"[UCS HTTP Manager] >> Error while trying to put data inside our APIs.\nThe endpoint say: {Response["message"]} ({Response["status"]})");
                }

                yield return Timing.WaitForSeconds(500.0f);
            }
        }
        
        public void Start()
        {
            if (Active)
            {
                return;
            }

            Active = true;
            PresenceCoroutine = Timing.RunCoroutine(PresenceAction());
        }

        public void Stop()
        {
            if (!Active)
            {
                return;
                
            }
           
            Active = false;
            Timing.KillCoroutines(PresenceCoroutine);
        }
    }
}
