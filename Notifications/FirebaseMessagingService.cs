using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Notifications
{
    public class FirebaseMessagingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FirebaseMessagingService> _logger;
        private readonly string _firebaseProjectId;
        private readonly GoogleCredential _credential;

        public FirebaseMessagingService(HttpClient httpClient, ILogger<FirebaseMessagingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read Firebase credentials from environment variables
            _firebaseProjectId = Environment.GetEnvironmentVariable("FirebaseProjectId") ?? throw new Exception("Missing Firebase_ProjectId");
            string clientEmail = Environment.GetEnvironmentVariable("Firebase_ClientEmail") ?? throw new Exception("Missing Firebase_ClientEmail");
            string privateKey = Environment.GetEnvironmentVariable("Firebase_PrivateKey")?.Replace("\\n", "\n") ?? throw new Exception("Missing Firebase_PrivateKey");

            // Construct GoogleCredential manually (no JSON file needed)
            var jsonCredential = new
            {
                type = "service_account",
                project_id = _firebaseProjectId,
                private_key_id = Guid.NewGuid().ToString(),
                private_key = privateKey,
                client_email = clientEmail,
                client_id = "your-client-id",
                auth_uri = "https://accounts.google.com/o/oauth2/auth",
                token_uri = "https://oauth2.googleapis.com/token",
                auth_provider_x509_cert_url = "https://www.googleapis.com/oauth2/v1/certs",
                client_x509_cert_url = $"https://www.googleapis.com/robot/v1/metadata/x509/{Uri.EscapeDataString(clientEmail)}"
            };

            string jsonCredentials = JsonConvert.SerializeObject(jsonCredential);
            _credential = GoogleCredential.FromJson(jsonCredentials)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            return await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
        }

        public async Task<bool> SendNotificationAsync(string token, string title, string body, object data)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var fcmMessage = new
                {
                    message = new
                    {
                        token = token,
                        notification = new
                        {
                            title = title,
                            body = body
                        },
                        data = data
                    }
                };

                var json = JsonConvert.SerializeObject(fcmMessage);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var fcmUrl = $"https://fcm.googleapis.com/v1/projects/{_firebaseProjectId}/messages:send";
                var response = await _httpClient.PostAsync(fcmUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to send notification to {token}. Error: {errorResponse}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to {token}");
                return false;
            }
        }
    }
}