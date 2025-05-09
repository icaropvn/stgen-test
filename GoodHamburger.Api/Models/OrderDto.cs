namespace GoodHamburger.Api.Models {
    public class OrderDto {
        public int SandwichId { get; set; }
        public List<int> ExtrasIds { get; set; } = new();
    }
}