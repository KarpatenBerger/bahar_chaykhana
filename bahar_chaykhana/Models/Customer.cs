namespace bahar_chaykhana.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int BonusBalance { get; set; }
        public DateTime RegisteredAt { get; set; }
    }
}
