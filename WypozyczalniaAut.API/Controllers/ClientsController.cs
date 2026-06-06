using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WypozyczalniaAut.API.Models;

namespace WypozyczalniaAut.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientsController : ControllerBase
    {
        private readonly OracleConnection _db;

        public ClientsController(OracleConnection db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] KlientCreateDto dto)
        {
            await _db.OpenAsync();
            var sql = @"INSERT INTO wypozyczalnia_owner.Klienci 
                        (Imie, Nazwisko, Pesel, Nip, Email, Telefon)
                        VALUES (:imie, :nazwisko, :pesel, :nip, :email, :telefon)
                        RETURNING ID_Klienta INTO :id";

            using var cmd = new OracleCommand(sql, _db);
            cmd.Parameters.Add("imie", dto.Imie);
            cmd.Parameters.Add("nazwisko", dto.Nazwisko);
            cmd.Parameters.Add("pesel", dto.Pesel ?? (object)DBNull.Value);
            cmd.Parameters.Add("nip", dto.Nip ?? (object)DBNull.Value);
            cmd.Parameters.Add("email", dto.Email);
            cmd.Parameters.Add("telefon", dto.Telefon ?? (object)DBNull.Value);

            var idParam = new OracleParameter("id", OracleDbType.Int32)
            {
                Direction = System.Data.ParameterDirection.Output
            };
            cmd.Parameters.Add(idParam);

            await cmd.ExecuteNonQueryAsync();
            await _db.CloseAsync();

            return Ok(new { IdKlienta = Convert.ToInt32(idParam.Value.ToString()) });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            await _db.OpenAsync();
            var sql = @"SELECT ID_Klienta, Imie, Nazwisko, Pesel, Nip, Email, Telefon
                        FROM wypozyczalnia_owner.Klienci 
                        WHERE ID_Klienta = :id";

            using var cmd = new OracleCommand(sql, _db);
            cmd.Parameters.Add("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await _db.CloseAsync();
                return NotFound(new { Message = "Klient nie istnieje." });
            }

            var klient = new Klient
            {
                IdKlienta = reader.GetInt32(0),
                Imie = reader.GetString(1),
                Nazwisko = reader.GetString(2),
                Pesel = reader.IsDBNull(3) ? null : reader.GetString(3),
                Nip = reader.IsDBNull(4) ? null : reader.GetString(4),
                Email = reader.GetString(5),
                Telefon = reader.IsDBNull(6) ? null : reader.GetString(6)
            };

            await _db.CloseAsync();
            return Ok(klient);
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(int id)
        {
            await _db.OpenAsync();
            var sql = @"SELECT w.ID_Wypozyczenia, w.Status, 
                               w.Data_Wypozyczenia, w.Data_Zwrotu_Planowana,
                               w.Data_Zwrotu_Rzeczywista,
                               f.Kwota_Netto, f.Kwota_Brutto, f.Status_Platnosci
                        FROM wypozyczalnia_owner.Wypozyczenia w
                        LEFT JOIN wypozyczalnia_owner.Faktury f 
                          ON w.ID_Wypozyczenia = f.ID_Wypozyczenia
                        WHERE w.ID_Klienta = :id
                        ORDER BY w.ID_Wypozyczenia DESC";

            using var cmd = new OracleCommand(sql, _db);
            cmd.Parameters.Add("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            var historia = new List<Wypozyczenie>();
            while (await reader.ReadAsync())
            {
                historia.Add(new Wypozyczenie
                {
                    IdWypozyczenia = reader.GetInt32(0),
                    Status = reader.GetString(1),
                    DataWypozyczenia = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                    DataZwrotuPlanowana = reader.GetDateTime(3),
                    DataZwrotuRzeczywista = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    KwotaNetto = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    KwotaBrutto = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    StatusPlatnosci = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }

            await _db.CloseAsync();
            return Ok(historia);
        }
    }
}