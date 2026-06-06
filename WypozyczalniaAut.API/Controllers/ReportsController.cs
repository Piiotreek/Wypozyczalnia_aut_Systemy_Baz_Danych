using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace WypozyczalniaAut.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly OracleConnection _db;

        public ReportsController(OracleConnection db)
        {
            _db = db;
        }

        [HttpGet("monthly")]
        public async Task<IActionResult> Monthly([FromQuery] int year, [FromQuery] int month)
        {
            await _db.OpenAsync();
            var sql = @"SELECT COUNT(*), 
                               NVL(SUM(f.Kwota_Netto), 0),
                               NVL(SUM(f.Kwota_Brutto), 0)
                        FROM wypozyczalnia_owner.Wypozyczenia w
                        JOIN wypozyczalnia_owner.Faktury f 
                          ON w.ID_Wypozyczenia = f.ID_Wypozyczenia
                        WHERE EXTRACT(YEAR FROM w.Data_Zwrotu_Rzeczywista) = :rok
                          AND EXTRACT(MONTH FROM w.Data_Zwrotu_Rzeczywista) = :miesiac
                          AND w.Status = 'Zakonczone'";

            using var cmd = new OracleCommand(sql, _db);
            cmd.Parameters.Add("rok", year);
            cmd.Parameters.Add("miesiac", month);

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            var summary = new
            {
                Rok = year,
                Miesiac = month,
                LiczbaWypozyczen = reader.GetInt32(0),
                PrzychdoNetto = reader.GetDecimal(1),
                PrzychdoBrutto = reader.GetDecimal(2)
            };

            await reader.CloseAsync();

            // Niezapłacone faktury
            var sqlNiezaplacone = @"SELECT f.ID_Faktury, 
                                           k.Imie || ' ' || k.Nazwisko AS Klient,
                                           k.Email,
                                           f.Kwota_Brutto, 
                                           f.Status_Platnosci,
                                           f.Data_Wystawienia
                                    FROM wypozyczalnia_owner.Faktury f
                                    JOIN wypozyczalnia_owner.Wypozyczenia w 
                                      ON f.ID_Wypozyczenia = w.ID_Wypozyczenia
                                    JOIN wypozyczalnia_owner.Klienci k 
                                      ON w.ID_Klienta = k.ID_Klienta
                                    WHERE f.Status_Platnosci != 'Oplacona'
                                      AND EXTRACT(YEAR  FROM f.Data_Wystawienia) = :rok
                                      AND EXTRACT(MONTH FROM f.Data_Wystawienia) = :miesiac";

            using var cmd2 = new OracleCommand(sqlNiezaplacone, _db);
            cmd2.Parameters.Add("rok", year);
            cmd2.Parameters.Add("miesiac", month);

            using var reader2 = await cmd2.ExecuteReaderAsync();
            var niezaplacone = new List<object>();
            while (await reader2.ReadAsync())
            {
                niezaplacone.Add(new
                {
                    IdFaktury = reader2.GetInt32(0),
                    Klient = reader2.GetString(1),
                    Email = reader2.GetString(2),
                    KwotaBrutto = reader2.GetDecimal(3),
                    StatusPlatnosci = reader2.GetString(4),
                    DataWystawienia = reader2.GetDateTime(5)
                });
            }

            await _db.CloseAsync();
            return Ok(new { Podsumowanie = summary, NiezaplaconeFaktury = niezaplacone });
        }

        [HttpPut("invoices/{id}/paid")]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            await _db.OpenAsync();
            var sql = @"UPDATE wypozyczalnia_owner.Faktury 
                        SET Status_Platnosci = 'Oplacona'
                        WHERE ID_Faktury = :id";

            using var cmd = new OracleCommand(sql, _db);
            cmd.Parameters.Add("id", id);

            var rows = await cmd.ExecuteNonQueryAsync();
            await _db.CloseAsync();

            if (rows == 0)
                return NotFound(new { Message = "Faktura nie istnieje." });

            return Ok(new { Message = "Faktura oznaczona jako opłacona." });
        }
    }
}