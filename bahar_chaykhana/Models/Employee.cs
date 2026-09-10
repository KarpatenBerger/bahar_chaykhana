namespace bahar_chaykhana.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty; // ДОБАВЛЕНО
        public string PasswordHash { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}