using System.Net.Http.Json;
using System.Text.Json;

namespace WypozyczalniaAut.Web.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;
        private UserInfo? _currentUser;

        public AuthService(HttpClient http) { _http = http; }

        public UserInfo? CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null;
        public event Action? OnChange;

        public async Task<bool> Login(string login, string password)
        {
            var response = await _http.PostAsJsonAsync("api/Auth/login",
                new { Login = login, Password = password });
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null) return false;

            _currentUser = new UserInfo
            {
                IdUzytkownika = result.IdUzytkownika,
                Nazwa = result.Nazwa,
                Rola = result.Rola,
                Token = result.Token
            };

            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.Token);

            OnChange?.Invoke();
            return true;
        }

        public void Logout()
        {
            _currentUser = null;
            _http.DefaultRequestHeaders.Authorization = null;
            OnChange?.Invoke();
        }

        public bool HasRole(params string[] roles) =>
            _currentUser != null && roles.Contains(_currentUser.Rola);
    }

    public class UserInfo
    {
        public int IdUzytkownika { get; set; }
        public string Nazwa { get; set; } = string.Empty;
        public string Rola { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    public class LoginResult
    {
        public int IdUzytkownika { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Rola { get; set; } = string.Empty;
        public string Nazwa { get; set; } = string.Empty;
    }
}