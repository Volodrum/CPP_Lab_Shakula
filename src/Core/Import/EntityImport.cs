using Core.Dto;

namespace Core.Import;

// Другий рівень імпорту: DTO, що пройшли розбір файлу, перетворюються на сутності.
// Записи, які порушують інваріанти домену, не зупиняють імпорт, а потрапляють у Errors.
public static class EntityImport
{
    public static ImportResult<TEntity> ToEntities<TDto, TEntity>(
        ImportResult<TDto> source,
        Func<TDto, TEntity> fromDto)
        where TDto : LibraryItemDto
    {
        var items = new List<TEntity>();
        var errors = new List<string>(source.Errors);

        foreach (TDto dto in source.Items)
        {
            try
            {
                items.Add(fromDto(dto));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add($"запис {dto.Id}: {ex.Message}");
            }
        }

        return new ImportResult<TEntity>(items, errors);
    }
}
