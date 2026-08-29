namespace bahar_chaykhana.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string DeliveryType { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Comment { get; set; }
        public string Status { get; set; } = "новый";
        public decimal Subtotal { get; set; }
        public decimal DeliveryCost { get; set; }
        public int BonusUsed { get; set; }
        public decimal Total { get; set; }
        public string? CancelToken { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
