using System.Text.Json;
using Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Errors;

// Дод. завдання 3: один обробник винятків для всього API (викликає його UseExceptionHandler).
// Кожен виняток стає ProblemDetails (RFC 9457) з людським текстом; тип винятку, stack trace і шляхи
// з диска клієнт не бачить ніколи — вони йдуть лише в журнал сервера.
//   DomainException                         → 409 Conflict  (правило бібліотеки: дубль ISBN, видані примірники)
//   BadHttpRequestException + JsonException → 400 з errors  ("copies":"abc" — помилка біля поля Copies)
//   BadHttpRequestException                 → 400           (порожнє тіло, page=abc)
//   решта                                   → 500           (загальний текст + traceId для пошуку в журналі)
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        // Клієнт сам закрив з'єднання — відповідати нікому.
        if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
            return true;

        ProblemDetails problem = exception switch
        {
            DomainException domain => Conflict(domain),
            BadHttpRequestException { InnerException: JsonException json } => InvalidJson(json),
            BadHttpRequestException bad => BadRequest(bad),
            _ => Unexpected(exception)
        };

        http.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });
    }

    private ProblemDetails Conflict(DomainException exception)
    {
        logger.LogInformation("Доменна відмова: {Message}", exception.Message);
        return new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Операція суперечить правилам бібліотеки.",
            Detail = exception.Message // повідомлення доменних винятків пишуться для людини і без технічних деталей
        };
    }

    // Тіло — JSON, але поле не того типу: "$.copies" → ключ "Copies", як у валідатора.
    private static HttpValidationProblemDetails InvalidJson(JsonException exception)
    {
        string field = FieldFromPath(exception.Path);
        return new HttpValidationProblemDetails(new Dictionary<string, string[]>
        {
            [field] = [field == "body"
                ? "Тіло запиту не є коректним JSON-об'єктом."
                : "Значення має неправильний тип або формат."]
        })
        {
            Status = StatusCodes.Status400BadRequest
        };
    }

    private static ProblemDetails BadRequest(BadHttpRequestException exception) => new()
    {
        Status = exception.StatusCode,
        Title = "Запит не вдалося розібрати.",
        Detail = "Тіло порожнє або не JSON, чи параметр запиту має неправильний тип (наприклад, page=abc)."
    };

    private ProblemDetails Unexpected(Exception exception)
    {
        logger.LogError(exception, "Неочікувана помилка під час обробки запиту");
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Внутрішня помилка сервера.",
            Detail = "Запит не виконано. Повторіть пізніше; якщо помилка повторюється — повідомте traceId адміністратору."
        };
    }

    private static string FieldFromPath(string? path)
    {
        // "$.copies" → "Copies"; "$" або null (тіло — не об'єкт) → "body"; вкладене "$.a.b" → "A.b".
        string trimmed = (path ?? "$").TrimStart('$').TrimStart('.');
        return trimmed.Length == 0 ? "body" : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }
}
