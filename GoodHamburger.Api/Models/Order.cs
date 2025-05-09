namespace GoodHamburger.Api.Models {
    public class Order {
        public int Id { get; set; }
        public int SandwichId { get; set; }
        public List<int> ExtrasIds { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal DiscountApplied { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}