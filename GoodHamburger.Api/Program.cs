using GoodHamburger.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<List<MenuItem>>(sp => new List<MenuItem> {
    new MenuItem { Id = 1, Name = "X Burger",   Price = 5.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 2, Name = "X Egg",      Price = 4.50m, Type = ItemType.Sandwich },
    new MenuItem { Id = 3, Name = "X Bacon",    Price = 7.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 4, Name = "Fries",      Price = 2.00m, Type = ItemType.Extra    },
    new MenuItem { Id = 5, Name = "Soft drink", Price = 2.50m, Type = ItemType.Extra    }
});

builder.Services.AddSingleton<List<Order>>(sp => new List<Order>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/api/menu", (List<MenuItem> menu) =>
    menu
);

app.MapGet("/api/menu/sandwiches", (List<MenuItem> menu) =>
    menu.Where(i => i.Type == ItemType.Sandwich)
);

app.MapGet("/api/menu/extras", (List<MenuItem> menu) =>
    menu.Where(i => i.Type == ItemType.Extra)
);

app.MapGet("/api/orders", (List<Order> orders) =>
    Results.Ok(orders)
);

app.MapGet("/api/orders/{id:int}", (int id, List<Order> orders) =>
{
    var order = orders.FirstOrDefault(o => o.Id == id);
    
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

app.MapPost("/api/orders", (OrderDto dto, List<Order> orders, List<MenuItem> menu) =>
{
    var sandwich = menu.FirstOrDefault(i => i.Id == dto.SandwichId && i.Type == ItemType.Sandwich);
    if(sandwich is null)
        return Results.BadRequest("Invalid Sandwich");

    var validFriesIds = new[] { 4 };
    var validDrinkIds = new[] { 5 };

    var fries = dto.FriesId.HasValue ? menu.FirstOrDefault(i => i.Id == dto.FriesId.Value && i.Type == ItemType.Extra && validFriesIds.Contains(i.Id)) : null;
    
    var drink = dto.DrinkId.HasValue ? menu.FirstOrDefault(i => i.Id == dto.DrinkId.Value && i.Type == ItemType.Extra && validDrinkIds.Contains(i.Id)) : null;

    if(dto.FriesId.HasValue && fries is null)
        return Results.BadRequest("Invalid Fries");
    if(dto.DrinkId.HasValue && drink is null)
        return Results.BadRequest("Invalid Drink");

    decimal subtotal = sandwich.Price + (fries?.Price ?? 0) + (drink?.Price ?? 0);
    decimal discount = 0;
    
    if(fries != null && drink != null)
        discount = 0.20m;
    else if(drink != null)
        discount = 0.15m;
    else if(fries != null)
        discount = 0.10m;

    var nextId = orders.Count > 0 ? orders.Max(o => o.Id) + 1 : 1;
    var order = new Order
    {
        Id = nextId,
        SandwichId      = dto.SandwichId,
        FriesId         = dto.FriesId,
        DrinkId         = dto.DrinkId,
        Total           = subtotal * (1 - discount),
        DiscountApplied = discount,
        CreatedAt       = DateTime.UtcNow
    };
    orders.Add(order);

    return Results.Created($"/api/orders/{order.Id}", order);
})
.Produces<Order>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.WithName("CreatedOrder");

app.Run();


// app.MapGet("/api/menu", async (AppDbContext db) =>
//     await db.MenuItems.ToListAsync()
// );

// app.MapGet("/api/menu/sandwiches", async (AppDbContext db) =>
//     await db.MenuItems.Where(i => i.Type == ItemType.Sandwich).ToListAsync()
// );

// app.MapGet("/api/menu/extras", async (AppDbContext db) =>
//     await db.MenuItems.Where(i => i.Type == ItemType.Extra).ToListAsync()
// );