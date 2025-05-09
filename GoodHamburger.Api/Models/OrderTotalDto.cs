namespace GoodHamburger.Api.Models {
    public class OrderTotalDto {
        public decimal Subtotal { get; set; }
        public decimal DiscountApplied { get; set; }
        public decimal Total { get; set; }
    }
}