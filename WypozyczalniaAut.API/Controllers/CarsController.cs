using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WypozyczalniaAut.API.Models;

namespace WypozyczalniaAut.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarsController : ControllerBase
    {
        private readonly OracleConnection _db;

        public CarsController(OracleConnection db)
        {
            _db = db;
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable([FromQuery] string? city)
        {
            await _db.OpenAsync();

            var query = @"SELECT ID_Samochodu, Marka, Model, Silnik, Sila,
                         Nr_Rejestracyjny, Przebieg, Miasto, Oddzial,
                         Kategoria, Cena_Doba
                  FROM wypozyczalnia_owner.v_dostepne_auta
                  WHERE (:city IS NULL OR Miasto = :city)";

            using var cmd = new OracleCommand(query, _db);
            cmd.Parameters.Add("city", string.IsNullOrEmpty(city)
                ? (object)DBNull.Value
                : city);

            using var reader = await cmd.ExecuteReaderAsync();
            var auta = new List<Auto>();
            while (await reader.ReadAsync())
            {
                auta.Add(new Auto
                {
                    IdSamochodu = reader.GetInt32(0),
                    Marka = reader.GetString(1),
                    Model = reader.GetString(2),
                    Silnik = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Sila = reader.IsDBNull(4) ? null : reader.GetString(4),
                    NrRejestracyjny = reader.GetString(5),
                    Przebieg = reader.GetInt32(6),
                    Miasto = reader.GetString(7),
                    Oddzial = reader.GetString(8),
                    Kategoria = reader.GetString(9),
                    CenaDoba = reader.GetDecimal(10)
                });
            }
            await _db.CloseAsync();
            return Ok(auta);
        }
    }
}