using Company.Product.Api.Authorization;
using Company.Product.Application.UseCases.Orders.CreateOrder;
using Company.Product.Application.UseCases.Orders.GetOrderById;
using Company.Product.Application.UseCases.Orders.ListOrders;
using Company.Product.Contracts.Requests.Orders;
using Company.Product.Contracts.Responses.Common;
using Company.Product.Contracts.Responses.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Product.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = ApiPolicies.OrdersWrite)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        [FromServices] ICreateOrderHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            request.CustomerId,
            request.Items.Select(i => new CreateOrderItemInput(i.Sku, i.Name, i.UnitPrice, i.Quantity)).ToList());

        var result = await handler.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.OrderId }, new { result.OrderId });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApiPolicies.OrdersRead)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderResponse>> GetById(
        Guid id,
        [FromServices] IGetOrderByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetOrderByIdQuery(id), cancellationToken);

        var response = new OrderResponse
        {
            Id = result.Order.Id,
            CustomerId = result.Order.CustomerId,
            CreatedUtc = result.Order.CreatedUtc,
            Status = result.Order.Status,
            TotalAmount = result.Order.TotalAmount,
            Items = result.Order.Items
                .Select(i => new OrderItemResponse
                {
                    Id = i.Id,
                    Sku = i.Sku,
                    Name = i.Name,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    LineTotal = i.LineTotal
                })
                .ToList()
        };

        return Ok(response);
    }

    [HttpGet]
    [Authorize(Policy = ApiPolicies.OrdersRead)]
    [ProducesResponseType(typeof(PagedResponse<OrderListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<OrderListItemResponse>>> List(
        [FromQuery] Guid? customerId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IListOrdersHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListOrdersQuery(customerId, page, pageSize), cancellationToken);

        var response = new PagedResponse<OrderListItemResponse>
        {
            Items = result.Orders.Items
                .Select(order => new OrderListItemResponse
                {
                    Id = order.Id,
                    CustomerId = order.CustomerId,
                    CreatedUtc = order.CreatedUtc,
                    Status = order.Status,
                    TotalAmount = order.TotalAmount
                })
                .ToList(),
            Page = result.Orders.Page,
            PageSize = result.Orders.PageSize,
            TotalCount = result.Orders.TotalCount
        };

        return Ok(response);
    }
}
