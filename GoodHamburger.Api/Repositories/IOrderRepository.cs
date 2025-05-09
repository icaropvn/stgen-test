using GoodHamburger.Api.Models;

public interface IOrderRepository {
  IEnumerable<Order> GetAll();
  Order? GetById(int id);    // ← adicione esta linha (ou sem ? + exceção interna)
  void Add(Order order);
  void Update(Order order);
  void Remove(Order order);
  int GetNextId();
}