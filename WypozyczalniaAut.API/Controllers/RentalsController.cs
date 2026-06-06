using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WypozyczalniaAut.API.Models;

namespace WypozyczalniaAut.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RentalsController : ControllerBase
    {
        private readonly OracleConnection _db;

        public RentalsController(OracleConnection db)
        {
            _db = db;
        }

        [HttpPost("reserve")]
        public async Task<IActionResult> Reserve([FromBody] RezerwacjaDto dto)
        {
            await _db.OpenAsync();
            using var cmd = new OracleCommand("BEGIN wypozyczalnia_owner.pkg_wypozyczalnia.RezerwujPojazd(:idKlienta,:idSamochodu,:dataOd,:dataDo,:idWyp); END;", _db);
            cmd.Parameters.Add("idKlienta", dto.IdKlienta);
            cmd.Parameters.Add("idSamochodu", dto.IdSamochodu);
            cmd.Parameters.Add("dataOd", dto.DataOd);
            cmd.Parameters.Add("dataDo", dto.DataDo);

            var outParam = new OracleParameter("idWyp", OracleDbType.Int32)
            {
                Direction = System.Data.ParameterDirection.Output
            };
            cmd.Parameters.Add(outParam);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                await _db.CloseAsync();
                return Ok(new { IdWypozyczenia = Convert.ToInt32(outParam.Value.ToString()) });
            }
            catch (OracleException ex)
            {
                await _db.CloseAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("{id}/issue")]
        public async Task<IActionResult> Issue(int id, [FromBody] WydanieDto dto)
        {
            await _db.OpenAsync();
            using var cmd = new OracleCommand("BEGIN wypozyczalnia_owner.pkg_wypozyczalnia.WydajPojazd(:idWyp,:idPrac); END;", _db);
            cmd.Parameters.Add("idWyp", id);
            cmd.Parameters.Add("idPrac", dto.IdPracownika);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                await _db.CloseAsync();
                return Ok(new { Message = "Pojazd wydany pomyślnie." });
            }
            catch (OracleException ex)
            {
                await _db.CloseAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("{id}/return")]
        public async Task<IActionResult> Return(int id, [FromBody] ZwrotDto dto)
        {
            await _db.OpenAsync();
            using var cmd = new OracleCommand("BEGIN wypozyczalnia_owner.pkg_wypozyczalnia.ZwrocPojazd(:idWyp,:idPrac,:przebieg); END;", _db);
            cmd.Parameters.Add("idWyp", id);
            cmd.Parameters.Add("idPrac", dto.IdPracownika);
            cmd.Parameters.Add("przebieg", dto.PrzebiegKonc);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                await _db.CloseAsync();
                return Ok(new { Message = "Pojazd zwrócony pomyślnie." });
            }
            catch (OracleException ex)
            {
                await _db.CloseAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            await _db.OpenAsync();
            var sql = @"SELECT w.ID_Wypozyczenia, w.ID_Klienta, w.ID_Samochodu,
                               w.Status, w.Data_Wypozyczenia, 
                               w.Data_Zwrotu_Planowana, w.Data_Zwrotu_Rzeczywista,
                               f.Kwota_Netto, f.Kwota_Brutto, f.Status_Platnosci
                        FROM wypozyczalnia_owner.Wypozyczenia w
                        LEFT JOIN wypozyczalnia_owner.Faktury f 
                          ON w.ID_Wypozyczenia = f.ID_Wypozyczenia
                        WHERE w.ID_Wypozyczenia = :id";

            using var cmd = new OracleCommand(sql, _db);
            cmd.Parameters.Add("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await _db.CloseAsync();
                return NotFound(new { Message = "Wypożyczenie nie istnieje." });
            }

            var wyp = new Wypozyczenie
            {
                IdWypozyczenia = reader.GetInt32(0),
                IdKlienta = reader.GetInt32(1),
                IdSamochodu = reader.GetInt32(2),
                Status = reader.GetString(3),
                DataWypozyczenia = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                DataZwrotuPlanowana = reader.GetDateTime(5),
                DataZwrotuRzeczywista = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                KwotaNetto = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                KwotaBrutto = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                StatusPlatnosci = reader.IsDBNull(9) ? null : reader.GetString(9)
            };

            await _db.CloseAsync();
            return Ok(wyp);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            await _db.OpenAsync();
            var sql = @"SELECT w.ID_Wypozyczenia,
                       k.Imie || ' ' || k.Nazwisko AS Klient,
                       m.Marka || ' ' || m.Model AS Pojazd,
                       s.Nr_Rejestracyjny,
                       w.Data_Wypozyczenia,
                       w.Data_Zwrotu_Planowana
                FROM wypozyczalnia_owner.Wypozyczenia w
                JOIN wypozyczalnia_owner.Klienci k ON w.ID_Klienta = k.ID_Klienta
                JOIN wypozyczalnia_owner.Samochody s ON w.ID_Samochodu = s.ID_Samochodu
                JOIN wypozyczalnia_owner.Modele_Konfiguracje m ON s.ID_Modelu = m.ID_Modelu
                WHERE w.Status = 'Rezerwacja'
                ORDER BY w.Data_Zwrotu_Planowana";

            using var cmd = new OracleCommand(sql, _db);
            using var reader = await cmd.ExecuteReaderAsync();
            var lista = new List<object>();

            while (await reader.ReadAsync())
            {
                lista.Add(new
                {
                    IdWypozyczenia = reader.GetInt32(0),
                    Klient = reader.GetString(1),
                    Pojazd = reader.GetString(2),
                    NrRejestracyjny = reader.GetString(3),
                    DataWypozyczenia = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                    DataZwrotuPlanowana = reader.GetDateTime(5)
                });
            }

            await _db.CloseAsync();
            return Ok(lista);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            await _db.OpenAsync();
            var sql = @"SELECT w.ID_Wypozyczenia,
                       k.Imie || ' ' || k.Nazwisko AS Klient,
                       m.Marka || ' ' || m.Model AS Pojazd,
                       s.Nr_Rejestracyjny,
                       w.Data_Wypozyczenia,
                       w.Data_Zwrotu_Planowana
                FROM wypozyczalnia_owner.Wypozyczenia w
                JOIN wypozyczalnia_owner.Klienci k ON w.ID_Klienta = k.ID_Klienta
                JOIN wypozyczalnia_owner.Samochody s ON w.ID_Samochodu = s.ID_Samochodu
                JOIN wypozyczalnia_owner.Modele_Konfiguracje m ON s.ID_Modelu = m.ID_Modelu
                WHERE w.Status = 'Aktywne'
                ORDER BY w.Data_Zwrotu_Planowana";

            using var cmd = new OracleCommand(sql, _db);
            using var reader = await cmd.ExecuteReaderAsync();
            var lista = new List<object>();

            while (await reader.ReadAsync())
            {
                lista.Add(new
                {
                    IdWypozyczenia = reader.GetInt32(0),
                    Klient = reader.GetString(1),
                    Pojazd = reader.GetString(2),
                    NrRejestracyjny = reader.GetString(3),
                    DataWypozyczenia = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                    DataZwrotuPlanowana = reader.GetDateTime(5)
                });
            }

            await _db.CloseAsync();
            return Ok(lista);
        }
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelDto dto)
        {
            await _db.OpenAsync();

            // Sprawdź czy rezerwacja należy do tego klienta
            var sqlCheck = @"SELECT COUNT(*) FROM wypozyczalnia_owner.Wypozyczenia
                     WHERE ID_Wypozyczenia = :id 
                     AND ID_Klienta = :idKlienta
                     AND Status = 'Rezerwacja'";

            using var cmdCheck = new OracleCommand(sqlCheck, _db);
            cmdCheck.Parameters.Add("id", id);
            cmdCheck.Parameters.Add("idKlienta", dto.IdKlienta);

            var count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());
            if (count == 0)
            {
                await _db.CloseAsync();
                return BadRequest(new { Message = "Nie można anulować tej rezerwacji." });
            }

            var sql = @"UPDATE wypozyczalnia_owner.Wypozyczenia 
            SET Status = 'Anulowane'
            WHERE ID_Wypozyczenia = :id";

            using var cmd = new OracleCommand(sql, _db);
            cmd.Parameters.Add("id", id);
            await cmd.ExecuteNonQueryAsync();

            // COMMIT - Oracle nie ma autocommit
            var commitCmd = new OracleCommand("COMMIT", _db);
            await commitCmd.ExecuteNonQueryAsync();

            await _db.CloseAsync();
            return Ok(new { Message = "Rezerwacja anulowana." });
        }

       
    }
    public class CancelDto
        {
            public int IdKlienta { get; set; }
        }
}