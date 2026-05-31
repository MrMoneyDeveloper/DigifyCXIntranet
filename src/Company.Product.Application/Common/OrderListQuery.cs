namespace Company.Product.Application.Common;

public sealed record OrderListQuery(Guid? CustomerId, int Page, int PageSize);
