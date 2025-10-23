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

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send")
            {
                Content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
