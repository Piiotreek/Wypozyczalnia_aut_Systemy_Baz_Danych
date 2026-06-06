namespace WypozyczalniaAut.API.Models
{
	public class Wypozyczenie
	{
		public int IdWypozyczenia { get; set; }
		public int IdKlienta { get; set; }
		public int IdSamochodu { get; set; }
		public DateTime? DataWypozyczenia { get; set; }
		public DateTime DataZwrotuPlanowana { get; set; }
		public DateTime? DataZwrotuRzeczywista { get; set; }
		public string Status { get; set; } = string.Empty;
		public decimal? KwotaNetto { get; set; }
		public decimal? KwotaBrutto { get; set; }
		public string? StatusPlatnosci { get; set; }
	}

	public class RezerwacjaDto
	{
		public int IdKlienta { get; set; }
		public int IdSamochodu { get; set; }
		public DateTime DataOd { get; set; }
		public DateTime DataDo { get; set; }
	}

	public class WydanieDto
	{
		public int IdPracownika { get; set; }
	}

	public class ZwrotDto
	{
		public int IdPracownika { get; set; }
		public int PrzebiegKonc { get; set; }
	}
}