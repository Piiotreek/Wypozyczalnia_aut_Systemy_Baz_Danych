using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Oracle.ManagedDataAccess.Client;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace WypozyczalniaAut.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly OracleConnection _db;
        private readonly IConfiguration _config;

        public AuthController(OracleConnection db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            await _db.OpenAsync();

            var sqlPrac = @"SELECT ID_Pracownika, Imie || ' ' || Nazwisko, Rola, PasswordHash
                    FROM wypozyczalnia_owner.Pracownicy
                    WHERE PESEL = :login";

            var sqlKlient = @"SELECT ID_Klienta, Imie || ' ' || Nazwisko, 'Klient', PasswordHash
                      FROM wypozyczalnia_owner.Klienci
                      WHERE Email = :login";

            int userId = 0;
            string userName = string.Empty;
            string userRole = string.Empty;
            string? hash = null;

            using var cmdPrac = new OracleCommand(sqlPrac, _db);
            cmdPrac.Parameters.Add("login", dto.Login);
            using var readerPrac = await cmdPrac.ExecuteReaderAsync();
            if (await readerPrac.ReadAsync())
            {
                userId = readerPrac.GetInt32(0);
                userName = readerPrac.GetString(1);
                userRole = readerPrac.GetString(2);
                hash = readerPrac.IsDBNull(3) ? null : readerPrac.GetString(3);
            }
            await readerPrac.CloseAsync();

            if (userId == 0)
            {
                using var cmdKlient = new OracleCommand(sqlKlient, _db);
                cmdKlient.Parameters.Add("login", dto.Login);
                using var readerKlient = await cmdKlient.ExecuteReaderAsync();
                if (await readerKlient.ReadAsync())
                {
                    userId = readerKlient.GetInt32(0);
                    userName = readerKlient.GetString(1);
                    userRole = readerKlient.GetString(2);
                    hash = readerKlient.IsDBNull(3) ? null : readerKlient.GetString(3);
                }
            }

            await _db.CloseAsync();

            if (userId == 0)
                return Unauthorized(new { Message = "Nieprawidłowe dane logowania." });

            // Sprawdź hasło jeśli jest ustawione
            if (hash != null && !BCrypt.Net.BCrypt.Verify(dto.Password, hash))
                return Unauthorized(new { Message = "Nieprawidłowe hasło." });

            var token = GenerujToken(userId, userName, userRole);
            return Ok(new
            {
                Token = token,
                Rola = userRole,
                Nazwa = userName,
                IdUzytkownika = userId
            });
        }

        private string GenerujToken(int userId, string userName, string role)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.UtcNow.AddMinutes(
                           Convert.ToDouble(_config["Jwt:ExpiryMinutes"]));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name,           userName),
                new Claim(ClaimTypes.Role,           role),
                new Claim("IdUzytkownika",           userId.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiry,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordDto dto)
        {
            await _db.OpenAsync();
            var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Spróbuj pracownika
            var sqlPrac = @"UPDATE wypozyczalnia_owner.Pracownicy 
                    SET PasswordHash = :hash 
                    WHERE PESEL = :login";
            using var cmdPrac = new OracleCommand(sqlPrac, _db);
            cmdPrac.Parameters.Add("hash", hash);
            cmdPrac.Parameters.Add("login", dto.Login);
            var rowsPrac = await cmdPrac.ExecuteNonQueryAsync();

            if (rowsPrac == 0)
            {
                // Spróbuj klienta
                var sqlKlient = @"UPDATE wypozyczalnia_owner.Klienci 
                          SET PasswordHash = :hash 
                          WHERE Email = :login";
                using var cmdKlient = new OracleCommand(sqlKlient, _db);
                cmdKlient.Parameters.Add("hash", hash);
                cmdKlient.Parameters.Add("login", dto.Login);
                await cmdKlient.ExecuteNonQueryAsync();
            }

            await _db.CloseAsync();
            return Ok(new { Message = "Hasło ustawione." });
        }
    }
    public class LoginDto
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    public class SetPasswordDto
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}