using GoodHamburger.Api.Models;
using GoodHamburger.Api.Exceptions;

namespace GoodHamburger.Api.Services {
    public class OrderService : IOrderService {
        private readonly IOrderRepository _repo;
        private readonly List<MenuItem>   _menu;

        public OrderService(IOrderRepository repo, List<MenuItem> menu) {
            _repo = repo;
            _menu = menu;
        }

        // order validations according business rules
        public (MenuItem Sandwich, List<MenuItem> Extras) ValidateOrderItems(OrderDto dto, List<MenuItem> menu) {
            var sandwich = menu.FirstOrDefault(i => i.Id == dto.SandwichId && i.Type == ItemType.Sandwich);
            if(sandwich is null)
                throw new ApiException(
                    "Invalid sandwich ID.",
                    "http://localhost/problems/invalid-sandwich-id",
                    StatusCodes.Status400BadRequest
                );

            var extrasIds = dto.ExtrasIds ?? new List<int>();
            if (extrasIds.GroupBy(x => x).Any(g => g.Count() > 1)) {
                throw new ApiException(
                    "Duplicated extras are not allowed.",
                    "http://localhost/problems/duplicated-extras",
                    StatusCodes.Status400BadRequest
                );
            }

            var extras = extrasIds.Select(id => {
                var item = menu.FirstOrDefault(i => i.Id == id && (i.Type == ItemType.Fries || i.Type == ItemType.Drink));

                if(item is null) {
                    throw new ApiException(
                        $"Extra item ID {id} is invalid.",
                        "http://localhost/problems/invalid-extra-id",
                        StatusCodes.Status400BadRequest
                    );
                }

                return item;
            }).ToList();

            if(extras.Count(e => e.Type == ItemType.Fries) > 1 ||
               extras.Count(e => e.Type == ItemType.Drink) > 1) {
                throw new ApiException(
                    "You can only include one extra item per type.",
                    "http://localhost/problems/extras-inclusion",
                    StatusCodes.Status400BadRequest
                );
            }

            return (sandwich, extras);
        }

        // calculates order discount
        private static decimal CalculateDiscount(List<MenuItem> extras) {
            var hasFries = extras.Any(e => e.Type == ItemType.Fries);
            var hasDrink = extras.Any(e => e.Type == ItemType.Drink);
            
            return (hasFries, hasDrink) switch {
                (true, true)  => 0.20m,
                (true, false) => 0.10m,
                (false, true) => 0.15m,
                _             => 0m
            };
        }

        // calculates order total amount (with discount)
        private static decimal CalculateTotal(MenuItem sandwich, List<MenuItem> extras) {
            var subtotal = sandwich.Price + extras.Sum(e => e.Price);
            var discount = CalculateDiscount(extras);
            return subtotal * (1 - discount);
        }

        // creates a new order and persists it in the in-memory list
        public Order CreateOrder(OrderDto dto) {
            var (sandwich, extras) = ValidateOrderItems(dto, _menu);
            
            var subtotal = sandwich.Price + extras.Sum(e => e.Price);
            var discount = CalculateDiscount(extras);
            var total = CalculateTotal(sandwich, extras);

            var order = new Order {
                Id = _repo.GetNextId(),
                SandwichId = dto.SandwichId,
                ExtrasIds = dto.ExtrasIds,
                Subtotal = subtotal,
                DiscountApplied = discount,
                Total = total,
                CreatedAt = DateTime.UtcNow
            };

            _repo.Add(order);
            return order;
        }

        // updates an order according its id
        public Order UpdateOrder(int id, OrderDto dto) {
            var existing = _repo.GetById(id);
            
            if(existing is null) {
                throw new ApiException(
                    $"Order with ID {id} not found.",
                    "http://localhost/problems/order-not-found",
                    StatusCodes.Status404NotFound
                );
            }

            var (sandwich, extras) = ValidateOrderItems(dto, _menu);

            existing.SandwichId = dto.SandwichId;
            existing.ExtrasIds = dto.ExtrasIds;
            existing.Subtotal = sandwich.Price + extras.Sum(e => e.Price);
            existing.DiscountApplied = CalculateDiscount(extras);
            existing.Total = CalculateTotal(sandwich, extras);

            if(!_repo.Update(existing)) {
                throw new ApiException(
                    $"Order with ID {id} could not be updated.",
                    "http://localhost/problems/order-update-failed",
                    StatusCodes.Status500InternalServerError
                );
            }

            return existing;
        }

        // delete an order according its id
        public void DeleteOrder(int id) {
            if(!_repo.Remove(id)) {
                throw new ApiException(
                    $"Order with ID {id} not found.",
                    "urn:yourapp:problem:order-not-found",
                    StatusCodes.Status404NotFound
                );
            }
        }
    }
}