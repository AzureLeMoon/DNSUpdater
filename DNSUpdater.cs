using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace DNSUpdaterService
{
    public partial class DNSUpdater : ServiceBase
    {
        private Timer timer;
        private string apiKey = "#YourArvanCloudAPiKey#";
        private string domainId = "#YourDmoainID#";
        private string baseUrl = "https://napi.arvancloud.ir/cdn/4.0/domains/malijack.icu/dns-records/";

        public DNSUpdater()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Start a timer to periodically update the DNS record
            timer = new Timer();
            timer.Interval = TimeSpan.FromMinutes(30).TotalMilliseconds; // Adjust the interval as needed
            timer.Elapsed += TimerElapsed;
            timer.Start();
        }

        protected override void OnStop()
        {
            // Stop the timer when the service is stopped
            timer.Stop();
            timer.Dispose();
        }

        private async void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            Log("Timer elapsed. Attempting to update DNS record.");

            // Get the public IP address
            string publicIp = await GetPublicIpAddressAsync();
            Log($"Retrieved public IP address: {publicIp}");

            // Get the current IP address of the hostname "nima.malijack.icu"
            string currentDnsIp = await GetDnsIpAddressAsync();
            Log($"Retrieved DNS IP address: {currentDnsIp}");

            // Compare the public IP with the DNS IP
            if (publicIp != currentDnsIp)
            {
                // Create the JSON payload
                string jsonPayload = $@"
                {{
                    ""type"": ""a"",
                    ""name"": ""#YourSubDomain#"",
                    ""value"": [
                        {{
                            ""ip"": ""{publicIp}"",
                            ""port"": null,
                            ""weight"": 100,
                            ""country"": ""IR""
                        }}
                    ],
                    ""ttl"": 120,
                    ""cloud"": false,
                    ""upstream_https"": ""default"",
                    ""ip_filter_mode"": {{
                        ""count"": ""single"",
                        ""order"": ""none"",
                        ""geo_filter"": ""none""
                    }}
                }}";

                bool success = await UpdateDnsRecordAsync(jsonPayload);
                if (success)
                {
                    Log("DNS record update successful.");
                }
                else
                {
                    Log("DNS record update failed.");
                }
            }
            else
            {
                Log("Public IP address matches DNS IP address. Skipping DNS update.");
            }
        }
        private async Task<bool> UpdateDnsRecordAsync(string jsonPayload)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                string url = $"{baseUrl}{domainId}";
                var request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Headers.Add("Authorization", $"ApiKey {apiKey}");
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                try
                {
                    var response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else
                    {
                        Log($"API request failed with status code: {response.StatusCode}");
                        return false;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Log($"API request failed: {ex.Message}");
                    return false;
                }
            }
        }


        private async Task<string> GetPublicIpAddressAsync()
        {
            string[] ipServices = {
                "https://httpbin.org/ip",
                "https://icanhazip.com",
                "http://checkip.dyndns.org"
            };

            using (HttpClient httpClient = new HttpClient())
            {
                foreach (string ipService in ipServices)
                {
                    try
                    {
                        var response = await httpClient.GetStringAsync(ipService);

                        // Parse the response based on the service
                        if (ipService.Contains("httpbin.org"))
                        {
                            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                            return json.origin;
                        }
                        else if (ipService.Contains("icanhazip.com"))
                        {
                            return response.Trim();
                        }
                        else if (ipService.Contains("checkip.dyndns.org"))
                        {
                            int startIndex = response.IndexOf("Current IP Address: ") + 20;
                            int endIndex = response.IndexOf("</body>");
                            return response.Substring(startIndex, endIndex - startIndex);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"IP retrieval failed for {ipService}: {ex.Message}");
                    }
                }

                // Return a default value or handle the failure as needed
                return "Unable to retrieve public IP address.";
            }
        }

        private async Task<string> GetDnsIpAddressAsync()
        {
                // Resolve the DNS hostname to get its IP address
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync("#YourFullDomain#");
                    if (hostEntry.AddressList.Length > 0)
                    {
                        return hostEntry.AddressList[0].ToString();
                    }
                    else
                    {
                        Log("Failed to resolve DNS hostname to an IP address.");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to resolve DNS hostname: {ex.Message}");
                    return null;
                }

        }

        private HttpClient CreateHttpClientWithLocalEndpoint()
        {
            // Specify the local endpoint (IP address and port) for the HttpClient
            var localEndpoint = new IPEndPoint(IPAddress.Parse("#Your InterFace IP#"), 0);

            // Create a custom HttpClient with a specific local endpoint
            var httpClientHandler = new HttpClientHandler
            {
                UseDefaultCredentials = false,
                Proxy = null,
                UseProxy = false
            };

            var customHttpClient = new HttpClient(httpClientHandler);
            customHttpClient.DefaultRequestHeaders.Add("User-Agent", "Your_User_Agent"); // Add a user agent header if needed
            customHttpClient.DefaultRequestHeaders.Add("Authorization", $"ApiKey {apiKey}");

            // Bind the HttpClient to the specified local endpoint
            var bindingProperty = customHttpClient.GetType().GetProperty("LocalEndPoint");
            bindingProperty.SetValue(customHttpClient, localEndpoint);

            return customHttpClient;
        }

        private void Log(string message)
        {
            EventLog.WriteEntry("DNSUpdaterService", message, EventLogEntryType.Information);
        }
    }
}
