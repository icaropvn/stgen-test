using GoodHamburger.Api.Models;

public interface IOrderRepository {
  IEnumerable<Order> GetAll();
  Order? GetById(int id);    // ← adicione esta linha (ou sem ? + exceção interna)
  void Add(Order order);
  bool Update(Order order);
  bool Remove(int id);
  int GetNextId();
}