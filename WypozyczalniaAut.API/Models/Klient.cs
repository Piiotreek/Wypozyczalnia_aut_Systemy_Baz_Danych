namespace WypozyczalniaAut.API.Models
{
    public class Klient
    {
        public int IdKlienta { get; set; }
        public string Imie { get; set; } = string.Empty;
        public string Nazwisko { get; set; } = string.Empty;
        public string? Pesel { get; set; }
        public string? Nip { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Telefon { get; set; }
    }

    public class KlientCreateDto
    {
        public string Imie { get; set; } = string.Empty;
        public string Nazwisko { get; set; } = string.Empty;
        public string? Pesel { get; set; }
        public string? Nip { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Telefon { get; set; }
    }
}