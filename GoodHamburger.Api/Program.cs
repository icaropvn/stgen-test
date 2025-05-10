using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using GoodHamburger.Api.Models;
using GoodHamburger.Api.Exceptions;
using GoodHamburger.Api.Services;
using Microsoft.AspNetCore.WebUtilities;

var builder = WebApplication.CreateBuilder(args);

// seed in-memory menu items
builder.Services.AddSingleton(sp => new List<MenuItem> {
    new MenuItem { Id = 1, Name = "X Burger", Price = 5.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 2, Name = "X Egg", Price = 4.50m, Type = ItemType.Sandwich },
    new MenuItem { Id = 3, Name = "X Bacon", Price = 7.00m, Type = ItemType.Sandwich },
    new MenuItem { Id = 4, Name = "Fries", Price = 2.00m, Type = ItemType.Fries },
    new MenuItem { Id = 5, Name = "Soft drink", Price = 2.50m, Type = ItemType.Drink }
});

// register repository and service as singletons for dependency injection
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddSingleton<IOrderService, OrderService>();

// add Swagger support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// global exception handler
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

// -------- APPLICATION ENDPOINTS --------

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

app.MapGet("/api/orders", (IOrderRepository repo) =>
    Results.Ok(repo.GetAll())
)
.Produces<IEnumerable<Order>>(StatusCodes.Status200OK)
.WithName("GetOrders");

app.MapGet("/api/orders/{id:int}", (int id, IOrderRepository repo) => {
    var order = repo.GetAll().FirstOrDefault(o => o.Id == id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
})
.Produces<Order>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithName("GetOrderById");

app.MapPost("/api/orders", (OrderDto dto, IOrderService svc) => {
    var newOrder = svc.CreateOrder(dto);
    return Results.Created($"/api/orders/{newOrder.Id}", newOrder);
})
.Produces<Order>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.WithName("CreateOrder");

app.MapPut("/api/orders/{id:int}", (int id, OrderDto dto, IOrderService svc) => {
    var updated = svc.UpdateOrder(id, dto);
    return updated is not null ? Results.Ok(updated) : Results.NotFound();
})
.Produces<Order>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.WithName("UpdateOrder");

app.MapDelete("/api/orders/{id:int}", (int id, IOrderService svc) => {
    svc.DeleteOrder(id);
    return Results.NoContent();
})
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound)
.WithName("DeleteOrder");

app.Run();