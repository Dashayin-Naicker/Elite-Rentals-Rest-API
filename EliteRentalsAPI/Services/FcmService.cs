using Google.Apis.Auth.OAuth2;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EliteRentalsAPI.Services
{
    public class FcmService
    {
        private readonly string _projectId;
        private readonly GoogleCredential _credential;
        private readonly HttpClient _http;
        private readonly ILogger<FcmService> _logger;

        public FcmService(IConfiguration config, ILogger<FcmService> logger)
        {
            _logger = logger;

            _projectId = config["Fcm:ProjectId"];
            var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_KEY_JSON");

            if (string.IsNullOrWhiteSpace(firebaseJson))
                throw new InvalidOperationException("❌ Firebase key not found in environment variable 'FIREBASE_KEY_JSON'");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(firebaseJson));
            _credential = GoogleCredential.FromStream(stream)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");


            _http = new HttpClient();

            _logger.LogInformation("✅ FCM Service initialized for project: {ProjectId}", _projectId);
        }

        public async Task SendAsync(string token, string title, string body, object? data = null)
        {
            try
            {
                // Get OAuth2 access token for FCM
                var scoped = await _credential.CreateScoped(new[] { "https://www.googleapis.com/auth/firebase.messaging" })
                    .UnderlyingCredential.GetAccessTokenForRequestAsync();

                if (string.IsNullOrWhiteSpace(scoped))
                    throw new InvalidOperationException("Failed to acquire FCM access token.");

                // Build message payload
                var message = new
                {
                    message = new
                    {
                        token,
                        notification = new
                        {
                            title,
                            body
                        },
                        data = data
                    }
                };

                var payloadJson = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("📦 FCM Payload:\n{Payload}", payloadJson);

                // Send to FCM REST API
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send")
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", scoped);

                _logger.LogInformation("🎯 Sending push notification to token: {Token}", token);

                var response = await _http.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ FCM Error {Status}: {Body}", response.StatusCode, responseBody);
                    throw new HttpRequestException($"FCM send failed: {response.StatusCode} - {responseBody}");
                }

                _logger.LogInformation("✅ Push notification sent successfully: {Status}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ FCM SendAsync failed");
                throw;
            }
        }
    }
}
