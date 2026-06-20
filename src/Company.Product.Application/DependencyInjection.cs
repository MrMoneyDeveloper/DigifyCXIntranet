using Company.Product.Application.Common;
using Company.Product.Application.UseCases.Orders.CreateOrder;
using Company.Product.Application.UseCases.Orders.GetOrderById;
using Company.Product.Application.UseCases.Orders.ListOrders;
using Microsoft.Extensions.DependencyInjection;

namespace Company.Product.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRequestValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        services.AddScoped<ICreateOrderHandler, CreateOrderHandler>();
        services.AddScoped<IGetOrderByIdHandler, GetOrderByIdHandler>();
        services.AddScoped<IListOrdersHandler, ListOrdersHandler>();

        return services;
    }
}
