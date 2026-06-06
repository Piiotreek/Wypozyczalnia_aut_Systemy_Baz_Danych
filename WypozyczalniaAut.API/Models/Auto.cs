namespace WypozyczalniaAut.API.Models
{
	public class Auto
	{
		public int IdSamochodu { get; set; }
		public string Marka { get; set; } = string.Empty;
		public string Model { get; set; } = string.Empty;
		public string? Silnik { get; set; }
		public string? Sila { get; set; }
		public string NrRejestracyjny { get; set; } = string.Empty;
		public int Przebieg { get; set; }
		public string Miasto { get; set; } = string.Empty;
		public string Oddzial { get; set; } = string.Empty;
		public string Kategoria { get; set; } = string.Empty;
		public decimal CenaDoba { get; set; }
	}
}