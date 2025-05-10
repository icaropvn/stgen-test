using GoodHamburger.Api.Models;

public class InMemoryOrderRepository : IOrderRepository {
    private readonly List<Order> _orders = new();

    public IEnumerable<Order> GetAll() => _orders;

    public Order? GetById(int id) => _orders.FirstOrDefault(o => o.Id == id);

    public void Add(Order order) => _orders.Add(order);

    public bool Update(Order order) {
        var index = _orders.FindIndex(o => o.Id == order.Id);
        
        if(index == -1)
            return false;

        _orders[index] = order;
        return true;
    }

    public bool Remove(int id) {
        var existing = GetById(id);
        
        if(existing is null)
            return false;

        _orders.Remove(existing);
        return true;
    }
  
    public int GetNextId() => _orders.Count > 0 ? _orders.Max(o => o.Id) + 1 : 1;
}