using FluentValidation;

namespace Api.Validation;

// Дод. завдання 2: фільтр кінцевої точки. Знаходить серед аргументів обробника значення типу T,
// проганяє його через IValidator<T> з DI і, якщо є помилки, повертає 400 ValidationProblem —
// обробник маршруту тоді взагалі не викликається і бачить лише валідні дані.
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        T? argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["body"] = ["Тіло запиту обов'язкове."] });

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        return result.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(result.ToDictionary());
    }
}

public static class ValidationFilterExtensions
{
    // Фільтр + опис відповіді 400 в OpenAPI одним викликом.
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
