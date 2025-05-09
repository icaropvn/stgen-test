using GoodHamburger.Api.Models;

namespace GoodHamburger.Api.Services {
    public interface IOrderService {
        (MenuItem Sandwich, List<MenuItem> Extras) ValidateOrderItems(OrderDto dto, List<MenuItem> menu);
        Order CreateOrder(OrderDto dto);
        Order UpdateOrder(int id, OrderDto dto);
        void DeleteOrder(int id);
    }
}