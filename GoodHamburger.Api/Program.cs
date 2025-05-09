using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using GoodHamburger.Api.Models;
using GoodHamburger.Api.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp => new List<MenuItem> {
    new MenuItem { Id = 1, Name = "X Burger", Price = 5.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 2, Name = "X Egg", Price = 4.50m, Type = ItemType.Sandwich },
    new MenuItem { Id = 3, Name = "X Bacon", Price = 7.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 4, Name = "Fries", Price = 2.00m, Type = ItemType.Fries },
    new MenuItem { Id = 5, Name = "Soft drink", Price = 2.50m, Type = ItemType.Drink }
});

builder.Services.AddSingleton(sp => new List<Order>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

static (MenuItem Sandwich, List<MenuItem> Extras) ValidateOrderItems(OrderDto dto, List<MenuItem> menu) {
    var sandwich = menu.FirstOrDefault(i => i.Id == dto.SandwichId && i.Type == ItemType.Sandwich);
    if(sandwich is null) {
        throw new ApiException(
            "Invalid sandwich ID.",
            "http://localhost/problems/invalid-sandwich-id",
            StatusCodes.Status400BadRequest
        );
    }

    var extrasIds = dto.ExtrasIds ?? new List<int>();
    if(extrasIds.GroupBy(x => x).Any(g => g.Count() > 1)) {
        throw new ApiException(
            "Duplicate extras are not allowed.",
            "http://localhost/problems/duplicated-extras",
            StatusCodes.Status400BadRequest
        );
    }
    
    var extras = extrasIds.Select(id => {
        var item = menu.FirstOrDefault(i =>
            i.Id == id && (i.Type == ItemType.Fries || i.Type == ItemType.Drink));
            if(item is null) {
                throw new ApiException(
                    $"Extra type item ID {id} is invalid.",
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

static OrderTotalDto CalculateOrderAmount(MenuItem sandwich, List<MenuItem> extras) {
    var subtotal = sandwich.Price + extras.Sum(e => e.Price);

    var hasFries = extras.Any(e => e.Type == ItemType.Fries);
    var hasDrink = extras.Any(e => e.Type == ItemType.Drink);

    decimal discount = (hasFries, hasDrink) switch {
        (true, true)  => 0.20m,
        (false, true) => 0.15m,
        (true, false) => 0.10m,
        _             => 0m
    };

    return new OrderTotalDto {
        Subtotal = subtotal,
        DiscountApplied = discount,
        Total = subtotal * (1 - discount)
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

        var statusCode = StatusCodes.Status500InternalServerError;
        var typeUri = "http://localhost/problems/internal-server-error";
        var detail = "An unexpected error occurred.";
        
        if(err is ApiException apiEx) {
            statusCode = apiEx.StatusCode;
            typeUri = apiEx.TypeUri;
            detail = apiEx.Message;
        }

        var pd = new ProblemDetails {
            Type = typeUri,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(pd);
    });
});

app.MapGet("/api/menu", (List<MenuItem> menu) =>
    Results.Ok(menu)
)
.Produces<List<MenuItem>>(StatusCodes.Status200OK)
.WithName("GetMenu");

app.MapGet("/api/menu/sandwiches", (List<MenuItem> menu) =>
    menu.Where(i => i.Type == ItemType.Sandwich)
)
.Produces<IEnumerable<MenuItem>>(StatusCodes.Status200OK)
.WithName("GetSandwiches");

app.MapGet("/api/menu/extras", (List<MenuItem> menu) =>
    menu.Where(i => i.Type == ItemType.Fries || i.Type == ItemType.Drink)
)
.Produces<IEnumerable<MenuItem>>(StatusCodes.Status200OK)
.WithName("GetExtras");

app.MapGet("/api/orders", (List<Order> orders) =>
    Results.Ok(orders)
)
.Produces<List<Order>>(StatusCodes.Status200OK)
.WithName("GetOrders");

app.MapGet("/api/orders/{id:int}", (int id, List<Order> orders) => {
    var order = orders.FirstOrDefault(o => o.Id == id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
})
.Produces<Order>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithName("GetOrderById");

app.MapPost("/api/orders", (OrderDto dto, List<Order> orders, List<MenuItem> menu) => {
    OrderTotalDto amountDetails;

    var (sandwich, extras) = ValidateOrderItems(dto, menu);
    amountDetails = CalculateOrderAmount(sandwich, extras);

    var nextId = orders.Count != 0 ? orders.Max(o => o.Id) + 1 : 1;
    var newOrder = new Order {
        Id = nextId,
        SandwichId = dto.SandwichId,
        ExtrasIds = dto.ExtrasIds,
        Subtotal = amountDetails.Subtotal,
        DiscountApplied = amountDetails.DiscountApplied,
        Total = amountDetails.Total,
        CreatedAt = DateTime.UtcNow
    };

    orders.Add(newOrder);

    return Results.Created($"/api/orders/{newOrder.Id}", newOrder);
})
.Produces<Order>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.WithName("CreateOrder");

app.MapPut("/api/orders/{id:int}", (int id, OrderDto dto, List<Order> orders, List<MenuItem> menu) => {
    var order = orders.FirstOrDefault(o => o.Id == id);
    if(order is null)
        return Results.NotFound($"Order ID {id} not found");

    var (sandwich, extras) = ValidateOrderItems(dto, menu);
    OrderTotalDto amountDetails = CalculateOrderAmount(sandwich, extras);

    order.SandwichId = dto.SandwichId;
    order.ExtrasIds = dto.ExtrasIds;
    order.Subtotal = amountDetails.Subtotal;
    order.DiscountApplied = amountDetails.DiscountApplied;
    order.Total = amountDetails.Total;

    return Results.Ok(order);
})
.Produces<Order>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.WithName("UpdateOrder");

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