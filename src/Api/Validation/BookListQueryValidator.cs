using Api.Contracts;
using Core.Services;
using FluentValidation;

namespace Api.Validation;

// Параметри пагінації: page=0 чи pageSize=1000 — це 400 з переліком полів, а не тихе виправлення.
public sealed class BookListQueryValidator : AbstractValidator<BookListQuery>
{
    public BookListQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Номер сторінки починається з 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, CatalogService.MaxPageSize)
            .WithMessage($"Розмір сторінки має бути від 1 до {CatalogService.MaxPageSize}.");

        RuleFor(q => q.Search).MaximumLength(100).WithMessage("Рядок пошуку — до 100 символів.");
        RuleFor(q => q.Author).MaximumLength(100).WithMessage("Фільтр автора — до 100 символів.");
    }
}
