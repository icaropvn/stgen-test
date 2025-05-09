using GoodHamburger.Api.Models;

public class InMemoryOrderRepository : IOrderRepository {
    private readonly List<Order> _orders = new();

    public IEnumerable<Order> GetAll() => _orders;
    public Order? GetById(int id) => _orders.FirstOrDefault(o => o.Id == id);
    public void Add(Order order) => _orders.Add(order);

    public void Update(Order order) {
        var idx = _orders.FindIndex(o => o.Id == order.Id);
        
        if(idx == -1)
            throw new KeyNotFoundException($"Order with ID {order.Id} not found.");
        
        _orders[idx] = order;
    }

    public void Remove(Order order) {
        if(!_orders.Remove(order))
            throw new KeyNotFoundException($"Order with ID {order.Id} not found.");
    }
  
    public int GetNextId() => _orders.Count > 0 ? _orders.Max(o => o.Id) + 1 : 1;
}