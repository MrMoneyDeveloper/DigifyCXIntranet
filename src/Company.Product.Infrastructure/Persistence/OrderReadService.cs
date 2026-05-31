using Company.Product.Application.Abstractions.Persistence;
using Company.Product.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Company.Product.Infrastructure.Persistence;

public sealed class OrderReadService : IOrderReadService
{
    private readonly AppDbContext _dbContext;

    public OrderReadService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderReadModel?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new OrderReadModel(
                o.Id,
                o.CustomerId,
                o.CreatedUtc,
                o.Status.ToString(),
                o.TotalAmount,
                o.Items
                    .OrderBy(i => i.Name)
                    .Select(i => new OrderItemReadModel(i.Id, i.Sku, i.Name, i.UnitPrice, i.Quantity, i.LineTotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<OrderListItemReadModel>> GetOrdersAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.Orders.AsNoTracking();

        if (query.CustomerId.HasValue)
        {
            baseQuery = baseQuery.Where(o => o.CustomerId == query.CustomerId.Value);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(o => o.CreatedUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new OrderListItemReadModel(
                o.Id,
                o.CustomerId,
                o.CreatedUtc,
                o.Status.ToString(),
                o.TotalAmount))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListItemReadModel>(items, query.Page, query.PageSize, totalCount);
    }
}
