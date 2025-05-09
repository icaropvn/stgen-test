using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using GoodHamburger.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp => new List<MenuItem> {
    new MenuItem { Id = 1, Name = "X Burger",   Price = 5.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 2, Name = "X Egg",      Price = 4.50m, Type = ItemType.Sandwich },
    new MenuItem { Id = 3, Name = "X Bacon",    Price = 7.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 4, Name = "Fries",      Price = 2.00m, Type = ItemType.Extra    },
    new MenuItem { Id = 5, Name = "Soft drink", Price = 2.50m, Type = ItemType.Extra    }
});

builder.Services.AddSingleton(sp => new List<Order>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

static OrderTotalDto CalculateOrderTotal(OrderDto dto, List<MenuItem> menu) {
    var sandwich = menu.FirstOrDefault(i => i.Id == dto.SandwichId && i.Type == ItemType.Sandwich) ?? throw new ArgumentException("Invalid Sandwich ID");

    var validFriesIds = new[] { 4 };
    var validDrinkIds = new[] { 5 };

    var fries = dto.FriesId.HasValue ? menu.FirstOrDefault(i => i.Id == dto.FriesId.Value && i.Type == ItemType.Extra && validFriesIds.Contains(i.Id)) : null;
    if(dto.FriesId.HasValue && fries is null)
        throw new ArgumentException("Invalid Fries ID");

    var drink = dto.DrinkId.HasValue ? menu.FirstOrDefault(i => i.Id == dto.DrinkId.Value && i.Type == ItemType.Extra && validDrinkIds.Contains(i.Id)) : null;
    if(dto.DrinkId.HasValue && drink is null)
        throw new ArgumentException("Invalid Drink ID");

    decimal subtotal = sandwich.Price + (fries?.Price ?? 0) + (drink?.Price ?? 0);
    decimal discount = (fries != null, drink != null) switch {
        (true, true)   => 0.20m,
        (false, true)  => 0.15m,
        (true, false)  => 0.10m,
        _              => 0m
    };
    
    decimal total = subtotal * (1 - discount);

    return new OrderTotalDto {
        Subtotal = subtotal,
        DiscountApplied = discount,
        Total = total
    };
}

if(app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errApp => {
    errApp.Run(async context => {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var err = feature?.Error;
        
        if(err is JsonException || err is BadHttpRequestException) {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails {
                Status = StatusCodes.Status400BadRequest,
                Title = "Malformed request",
                Detail = "Could not read the request body as valid JSON."
            });
        }
        else {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails {
                Status = 500,
                Title = "Internal server error"
            });
        }
    });
});

app.MapGet("/api/menu", (List<MenuItem> menu) =>
    Results.Ok(menu)
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

app.MapGet("/api/orders/{id:int}", (int id, List<Order> orders) => {
    var order = orders.FirstOrDefault(o => o.Id == id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

app.MapPost("/api/orders", (OrderDto dto, List<Order> orders, List<MenuItem> menu) => {
    OrderTotalDto paymentDetails;

    try {
        paymentDetails = CalculateOrderTotal(dto, menu);
    }
    catch(ArgumentException ex) {
        return Results.BadRequest(ex.Message);
    }

    var nextId = orders.Any() ? orders.Max(o => o.Id) + 1 : 1;

    var newOrder = new Order {
        Id = nextId,
        SandwichId = dto.SandwichId,
        FriesId = dto.FriesId,
        DrinkId = dto.DrinkId,
        Total = paymentDetails.Total,
        DiscountApplied = paymentDetails.DiscountApplied,
        CreatedAt = DateTime.UtcNow
    };

    orders.Add(newOrder);

    return Results.Created($"/api/orders/{newOrder.Id}", newOrder);
})
.Produces<Order>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.WithName("CreateOrder");

app.MapGet("/api/orders/{id:int}/calculate", (int id, List<Order> orders, List<MenuItem> menu) => {
    var order = orders.FirstOrDefault(o => o.Id == id);
    if(order is null)
        return Results.NotFound($"Order ID {id} not found");
    
    var dto = new OrderDto {
        SandwichId = order.SandwichId,
        FriesId = order.FriesId,
        DrinkId = order.DrinkId
    };

    OrderTotalDto paymentDetails;

    try {
        paymentDetails = CalculateOrderTotal(dto, menu);
    }
    catch (ArgumentException ex) {
        return Results.BadRequest(ex.Message);
    }

    return Results.Ok(paymentDetails);
})
.Produces<OrderTotalDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status400BadRequest)
.WithName("CalculateOrderPayment");

app.MapPut("/api/orders/{id:int}", (int id, OrderDto dto, List<Order> orders, List<MenuItem> menu) => {
    var order = orders.FirstOrDefault(o => o.Id == id);
    if(order is null)
        return Results.NotFound($"Order ID {id} not found");

    OrderTotalDto paymentDetails;

    try {
        paymentDetails = CalculateOrderTotal(dto, menu);
    }
    catch(ArgumentException ex) {
        return Results.BadRequest(ex.Message);
    }

    order.SandwichId = dto.SandwichId;
    order.FriesId = dto.FriesId;
    order.DrinkId = dto.DrinkId;
    order.Total = paymentDetails.Total;
    order.DiscountApplied = paymentDetails.DiscountApplied;

    return Results.Ok(order);
})
.Produces<Order>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.WithName("UpdateOrder");;

app.MapDelete("/api/orders/{id:int}", (int id, List<Order> orders) => {
    var order = orders.FirstOrDefault(o => o.Id == id);
    if(order is null)
        return Results.NotFound($"Order ID {id} not found");
    
    orders.Remove(order);

    return Results.NoContent();
})
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound)
.WithName("DeleteOrder");

app.Run();