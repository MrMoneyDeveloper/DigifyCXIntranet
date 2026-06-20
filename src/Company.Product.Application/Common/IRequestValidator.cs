namespace Company.Product.Application.Common;

public interface IRequestValidator<in TRequest>
{
    IReadOnlyDictionary<string, string[]> Validate(TRequest request);
}
