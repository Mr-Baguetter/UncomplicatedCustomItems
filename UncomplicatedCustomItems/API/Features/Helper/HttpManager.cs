﻿using Exiled.Loader;
using MEC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UncomplicatedCustomItems.API.Struct;
using Exiled.API.Features;

namespace UncomplicatedCustomItems.API.Features.Helper
{
    internal class HttpManager
    {
        /// <summary>
        /// Gets the <see cref="CoroutineHandle"/> of the presence coroutine.
        /// </summary>
        public CoroutineHandle PresenceCoroutine { get; internal set; }

        /// <summary>
        /// Gets the <see cref="true"/> the message that confirm that the server is communicating correctly with our APIs has been sent in the console.
        /// </summary>
        public bool SentConfirmationMessage { get; internal set; } = false;

        /// <summary>
        /// Gets the number of errors that has occurred. If this number exceed the <see cref="MaxErrors"/> quote then this feature will be deactivated.
        /// </summary>
        public uint Errors { get; internal set; } = 0;

        /// <summary>
        /// Gets the maximum number of errors that can occur before deactivating the function.
        /// </summary>
        public uint MaxErrors { get; }

        /// <summary>
        /// Gets whether <see cref="true"/> this feature is active.
        /// </summary>
        public bool Active { get; internal set; } = false;

        /// <summary>
        /// Gets if the feature can be activated - missing library
        /// </summary>
        public bool IsAllowed { get; internal set; } = true;

        /// <summary>
        /// Gets the prefix of the plugin for our APIs
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        /// Gets the <see cref="HttpClient"/> public istance
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// Gets the UCS APIs endpoint
        /// </summary>
        public string Endpoint { get; } = "https://ucs.fcosma.it/api/v2";

        /// <summary>
        /// Gets the CreditTag storage for the plugin, downloaded from our central server
        /// </summary>
        public Dictionary<string, Triplet<string, string, bool>> Credits { get; internal set; } = new();

        /// <summary>
        /// Gets the List of the ResponseTimes
        /// </summary>
        public List<float> ResponseTimes { get; } = new();

        /// <summary>
        /// Create a new istance of the HttpManager
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="maxErrors"></param>
        public HttpManager(string prefix, uint maxErrors = 5)
        {
            if (Type.GetType("Newtonsoft.Json.JsonConvert") is null)
            {
                LogManager.Error($"Failed to load the HttpManager of {prefix.ToUpper()}: Missing library Newtonsoft.Json v13.0.3\nPlease install it AS SOON AS POSSIBLE!");
                IsAllowed = false;
                return;
            }

            Prefix = prefix;
            MaxErrors = maxErrors;
            HttpClient = new();
            LoadCreditTags();
        }

        public HttpResponseMessage HttpGetRequest(string url)
        {
            try
            {
                Task<HttpResponseMessage> Response = Task.Run(() => HttpClient.GetAsync(url));

                Response.Wait();

                return Response.Result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public HttpResponseMessage HttpPutRequest(string url, string content)
        {
            try
            {
                Task<HttpResponseMessage> Response = Task.Run(() => HttpClient.PutAsync(url, new StringContent(content, Encoding.UTF8, "text/plain")));

                Response.Wait();

                return Response.Result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string RetriveString(HttpResponseMessage response)
        {
            if (response is null)
                return string.Empty;

            return RetriveString(response.Content);
        }

        public string RetriveString(HttpContent response)
        {
            if (response is null)
                return string.Empty;

            Task<string> String = Task.Run(response.ReadAsStringAsync);

            String.Wait();

            return String.Result;
        }

        public HttpStatusCode AddServerOwner(string discordId)
        {
            return HttpGetRequest($"{Endpoint}/owners/add?discordid={discordId}")?.StatusCode ?? HttpStatusCode.InternalServerError;
        }

        public Version LatestVersion()
        {
            string Version = RetriveString(HttpGetRequest($"{Endpoint}/{Prefix}/version?vts=5"));

            if (Version is not null && Version != string.Empty)
                return new(Version);

            return Plugin.Instance.Version;
        }

        public void LoadCreditTags()
        {
            Credits = new();
            try
            {
                Dictionary<string, Dictionary<string, string>> Data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(RetriveString(HttpGetRequest("https://ucs.fcosma.it/api/credits.json")));

                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in Data.Where(kvp => kvp.Value.ContainsKey("role") && kvp.Value.ContainsKey("color") && kvp.Value.ContainsKey("override")))
                    Credits.Add(kvp.Key, new(kvp.Value["role"], kvp.Value["color"], bool.Parse(kvp.Value["ovveride"])));
            }
            catch (Exception) { }
        }

        public Triplet<string, string, bool> GetCreditTag(Player player)
        {
            if (Credits.ContainsKey(player.UserId))
                return Credits[player.UserId];

            return new(null, null, false);
        }

        public void ApplyCreditTag(Player player)
        {
            Triplet<string, string, bool> Tag = GetCreditTag(player);
            if (player.RankName is not null && player.RankName != string.Empty && !Tag.Third)
                return; // Do not override

            if (Tag.First is not null && Tag.Second is not null)
            {
                player.RankName = Tag.First;
                player.RankColor = Tag.Second;
            }
        }

        public bool IsLatestVersion(out Version latest)
        {
            latest = LatestVersion();
            if (latest.CompareTo(Plugin.Instance.Version) > 0)
                return false;

            return true;

        }

        public bool IsLatestVersion()
        {
            if (LatestVersion().CompareTo(Plugin.Instance.Version) > 0)
                return false;

            return true;
        }

        internal bool Presence(out HttpContent httpContent)
        {
            float Start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            HttpResponseMessage Status = HttpGetRequest($"{Endpoint}/{Prefix}/presence?port={Server.Port}&cores={Environment.ProcessorCount}&ram=0&version={Plugin.Instance.Version}");
            httpContent = Status.Content;
            ResponseTimes.Add(DateTimeOffset.Now.ToUnixTimeMilliseconds() - Start);
            if (Status.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }
            return false;
        }

        internal void PresenceNotListed() => HttpGetRequest($"{Endpoint}/{Prefix}/presence_notlisted?port={Server.Port}&cores={Environment.ProcessorCount}&ram=0&version={Plugin.Instance.Version}");

        internal HttpStatusCode ShareLogs(string data, out HttpContent httpContent)
        {
            HttpResponseMessage Status = HttpPutRequest($"{Endpoint}/{Prefix}/error?port={Server.Port}&exiled_version={Loader.Version}&plugin_version={Plugin.Instance.Version}", data);
            httpContent = Status.Content;
            return Status.StatusCode;
        }

        internal KeyValuePair<HttpStatusCode, string> Mailbox()
        {
            HttpResponseMessage Message = HttpGetRequest($"{Endpoint}/{Prefix}/mailbox?version={Plugin.Instance.Version}");
            return new(Message.StatusCode, RetriveString(Message.Content));
        }

        internal IEnumerator<float> PresenceAction()
        {
            while (Active && Errors <= MaxErrors)
            {
                if (Server.IsVerified)
                    if (!Presence(out HttpContent content))
                        try
                        {
                            Dictionary<string, string> Response = JsonConvert.DeserializeObject<Dictionary<string, string>>(RetriveString(content));
                            Errors++;
                        }
                        catch (Exception) { }
                    else
                        PresenceNotListed();

                // Do anche the Mailbox action
                if (Plugin.Instance.Config.DoEnableAdminMessages)
                {
                    KeyValuePair<HttpStatusCode, string> Mail = Mailbox();

                    if (Mail.Key is HttpStatusCode.OK)
                        LogManager.Warn($"[UCS HTTP Manager]:[UCS Mailbox] >> Central server have a message:\n{Mail.Value}");
                }

                yield return Timing.WaitForSeconds(500.0f);
            }
        }

        public void Start()
        {
            if (Active)
                return;

            if (!IsAllowed)
                return;

            Active = true;
            PresenceCoroutine = Timing.RunCoroutine(PresenceAction());
        }

        public void Stop()
        {
            if (!Active)
                return;

            Active = false;
            Timing.KillCoroutines(PresenceCoroutine);
        }
    }
}
