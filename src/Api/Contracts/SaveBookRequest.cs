namespace Api.Contracts;

// Вхідний DTO для POST і PUT: те, що клієнт може задати. Id сюди не входить — його видає сервер (POST)
// або він приходить з маршруту (PUT /api/books/{id}). Available теж немає: це наслідок видач, а не введення.
// Copies — бажана кількість примірників; PUT з тим самим тілом вдруге нічого не змінює (ідемпотентність).
// Правила — у SaveBookRequestValidator (FluentValidation), викликає їх ValidationFilter до обробника.
public sealed record SaveBookRequest(
    string Isbn,
    string Title,
    string? Author,
    int Year,
    int Copies);
