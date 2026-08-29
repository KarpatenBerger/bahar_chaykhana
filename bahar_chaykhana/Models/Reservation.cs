namespace bahar_chaykhana.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateOnly ReservationDate { get; set; }
        public TimeOnly ReservationTime { get; set; }
        public int Guests { get; set; }
        public string? Comment { get; set; }
        public string Status { get; set; } = "ожидает";
        public string? CancelToken { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
