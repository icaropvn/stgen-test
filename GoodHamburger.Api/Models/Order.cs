namespace GoodHamburger.Api.Models {
    public class Order {
        public int Id { get; set; }
        public int SandwichId { get; set; }
        public int? FriesId { get; set; }
        public int? DrinkId { get; set; }
        public decimal Total { get; set; }
        public decimal DiscountApplied { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}