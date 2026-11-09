using Api.Contracts;
using Core.Domain;
using Core.Services;
using FluentValidation;

namespace Api.Validation;

// Дод. завдання 2: правила вхідного DTO на FluentValidation. Межі беруться з домену (Book.MinYear,
// IsbnRules, CatalogService.MaxCopies), щоб API і Core не розійшлися. Ключ помилки — ім'я властивості
// (Isbn, Title, ...): Blazor-форма тижня 12 покаже повідомлення біля поля з тим самим іменем.
public sealed class SaveBookRequestValidator : AbstractValidator<SaveBookRequest>
{
    public const int TitleMaxLength = 200;
    public const int AuthorMaxLength = 120;

    public SaveBookRequestValidator()
    {
        RuleFor(r => r.Isbn)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ISBN обов'язковий.")
            .Must(IsbnRules.IsValid).WithMessage("ISBN має містити 10 або 13 цифр (дефіси дозволені).");

        RuleFor(r => r.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage($"Назва обов'язкова, до {TitleMaxLength} символів.")
            .MaximumLength(TitleMaxLength).WithMessage($"Назва обов'язкова, до {TitleMaxLength} символів.");

        RuleFor(r => r.Author)
            .MaximumLength(AuthorMaxLength).WithMessage($"Автор — до {AuthorMaxLength} символів.");

        // Верхня межа рахується під час кожної перевірки, а не один раз у конструкторі: валідатор — Singleton.
        RuleFor(r => r.Year)
            .Must(year => year >= Book.MinYear && year <= DateTime.Today.Year)
            .WithMessage(_ => $"Рік видання має бути в межах {Book.MinYear}..{DateTime.Today.Year}.");

        RuleFor(r => r.Copies)
            .InclusiveBetween(0, CatalogService.MaxCopies)
            .WithMessage($"Кількість примірників має бути від 0 до {CatalogService.MaxCopies}.");
    }
}
