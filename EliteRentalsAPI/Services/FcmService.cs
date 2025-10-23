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

        public FcmService(IConfiguration config)
        {
            var jsonPath = config["Fcm:ServiceAccountPath"];
            _projectId = config["Fcm:ProjectId"];
            _credential = GoogleCredential.FromFile(jsonPath)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
            _http = new HttpClient();
        }

        public async Task SendAsync(string token, string title, string body, object? data = null)
        {
            try
            {
                var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

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
                Console.WriteLine("📦 FCM Payload:");
                Console.WriteLine(payloadJson);

                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send")
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                Console.WriteLine($"🎯 Sending to token: {token}");

                var response = await _http.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📡 FCM Response: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine(responseBody);

                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FCM SendAsync failed: {ex.Message}");
                throw;
            }
        }

    }
}
