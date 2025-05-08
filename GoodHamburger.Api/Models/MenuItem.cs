namespace GoodHamburger.Api.Models {
    public class MenuItem {
        public int Id { get; set;}
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public ItemType Type { get; set; }
    }
}