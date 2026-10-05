using System.Text.Json.Serialization;

namespace Core.Dto;

// Тиждень 5: файлове сховище пише видання, примірники й видачі в один JSON-список LibraryItemDto.
// Поле "kind" підказує серіалізатору, який саме record відновити.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(BookDto), "book")]
[JsonDerivedType(typeof(BookCopyDto), "copy")]
[JsonDerivedType(typeof(LoanDto), "loan")]
public abstract record LibraryItemDto(string Id);
