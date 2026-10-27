# CrossApp

Наскрізний навчальний проєкт з дисципліни **«Крос-платформне програмування»**.

- **Студент**: Шакула Володимир, група ФЕІ-32с
- **Предметна область**: Бібліотека (варіант Б)
- **Репозиторій**: <https://github.com/Volodrum/CPP_Lab_Shakula>
- **Середовище вимірювань**: Windows 11 Home x64 (10.0.26200), .NET SDK 10.0.400, runtime .NET 10.0.11

> Опис лабораторних 1–4 (з виводами консолі) — у [README_labs1-4.md](README_labs1-4.md), лабораторної 5 — у [README_lab05.md](README_lab05.md), лабораторної 6 — у [README_lab06.md](README_lab06.md), лабораторної 7 — у [README_lab07.md](README_lab07.md).

---

## Лабораторна робота 8. async/await, скасування, доменні винятки, unit-тести Core

**Мета**: реалізувати асинхронне виконання тривалих операцій введення-виведення, підтримати кооперативне скасування, розмежувати доменні й технічні помилки та покрити тестами ключову логіку `Core`.

**Варіант (бібліотека)**: async-імпорт каталогу видань з JSON; `DomainException` «примірник уже видано»; тест на повернення невиданого примірника.

### Запуск

| Команда | Що відбувається |
| :------ | :-------------- |
| `dotnet test` | Усі тести `tests/Core.Tests` (28 випадків) |
| `dotnet test --collect:"XPlat Code Coverage"` | Те саме + звіт покриття Cobertura у `tests/Core.Tests/TestResults/…` (дод. 4) |
| `dotnet run --project src/Cli -- --import-async` | Імпорт `data/catalog.json` (17 видань) у сховище в пам'яті |
| `dotnet run --project src/Cli -- --import-async --file` | Те саме у `FileBookStore`; другий запуск — 0 записів (ідемпотентність) |
| `dotnet run --project src/Cli -- --import-async data/catalog_bad.json` | Зіпсований запис 3 → «Дані відхилено: Запис 3: …» |
| `dotnet run --project src/Cli -- --import-async --big` | Генерує 1 000 000 видань у `%TEMP%`, імпортує з прогресом; **Ctrl+C** → «Імпорт скасовано.» |
| `dotnet run --project src/Cli -- --import-async --big --timeout 2` | Той самий файл із тайм-аутом 2 с (зв'язані токени, дод. 2) |
| `… --import-async [шлях] --stream` | Потоковий імпорт через `IAsyncEnumerable<Book>` (дод. 1) |
| `… --import-async --big 200000` | Розмір згенерованого файлу задається числом після `--big` |

Як перевірити скасування: запустити `--import-async --big` і натиснути Ctrl+C, поки малюється прогрес (або коли з'явився рядок «Імпорт …»), — програма друкує «Імпорт скасовано.» і завершується з кодом 1 без стектрейсу. Без натискання: `--big --timeout 2` (файл генерується до старту тайм-ауту; на цій машині розбір і додавання мільйона записів тривають довше за 2 с). У тестах скасування перевіряють `ImportAsync_WhenAlreadyCancelled_Throws`, `ImportAsync_WhenCancelledDuringLoop_StopsAfterCurrentRecord` і два тести `TimeoutPolicy_*`.

---

### 1. Структура після лабораторної

```text
src/Core/
├── DomainException.cs              # НОВЕ: порушення правила бібліотеки
├── StorageException.cs             # НОВЕ: технічна помилка джерела (обгортає JsonException)
├── Abstractions/ICatalogImporter.cs  # НОВЕ: асинхронний контракт LoadAsync(path, ct)
├── Storage/JsonCatalogImporter.cs  # НОВЕ: FileStream(useAsync) + DeserializeAsync; StreamAsync (дод. 1)
├── Services/LendingService.cs      # ЗМІНЕНО: ImportAsync(importer, path, [progress], ct), ImportAsync(IAsyncEnumerable)
├── Services/TimeoutPolicy.cs       # НОВЕ: зв'язані токени користувач + тайм-аут (дод. 2)
├── Domain/BookCopy.cs, Loan.cs     # ЗМІНЕНО: InvalidOperationException → DomainException
└── Import/EntityImport.cs          # ЗМІНЕНО: перехоплює DomainException замість InvalidOperationException
src/Cli/
├── Program.cs                      # ЗМІНЕНО: --import-async: top-level await, Ctrl+C, catch за типами
├── AsyncImport.cs                  # НОВЕ: розбір ключів, ConsoleProgress (дод. 3), генератор великого файлу
└── EventsDemo.cs, ServiceDemo.cs, SinkDemo.cs  # ЗМІНЕНО: catch DomainException
tests/Core.Tests/                   # НОВЕ: xUnit, посилається лише на src/Core
├── LendingServiceTests.cs          # інваріанти, DomainException vs ArgumentException, подія
├── ReportServiceTests.cs           # звіти тижня 7: [Theory], топ-N, агрегати
├── ImportTests.cs                  # скасування, ідемпотентність, тимчасові файли, тайм-аут
├── TestData.cs                     # фіксований фонд із 4 видань / 9 примірників (не SampleData)
└── TestDoubles.cs                  # FakeImporter, NeverEndingImporter, CallbackProgress
data/catalog.json, catalog_bad.json # НОВЕ: 17 видань; 5 видань із роком 2099 у записі 3
```

Проєкт тестів створено командами з кроку 1 (`dotnet new xunit -n Core.Tests -o tests/Core.Tests -f net10.0`, `dotnet sln add …`, `dotnet add tests/Core.Tests reference src/Core/Core.csproj`); `Core.Tests.csproj` має єдиний `ProjectReference` — на `Core`.

### 2. Які операції стали асинхронними і чому

**Асинхронні** — лише ті, де потік справді чекав би на ввід-вивід:

- `JsonCatalogImporter.LoadAsync` / `StreamAsync` — читання файлу каталогу. Файл може бути великим (мільйон записів — ~100 МБ), а згодом на місці файлу буде HTTP (тиждень 13). `FileStream(..., useAsync: true)` + `JsonSerializer.DeserializeAsync` дають справжній overlapped I/O: поки диск віддає байти, потік не зайнятий.
- `LendingService.ImportAsync` — асинхронний, бо чекає `LoadAsync` («асинхронність на всіх рівнях»). Сам цикл додавання синхронний (робота з пам'яттю), але між записами перевіряє токен.
- `Program.cs` — top-level `await`, тож `Main` став `async Task<int>`; `.Result`/`.Wait()` немає.
- `BigCatalog.WriteAsync` (Cli) — запис згенерованого файлу через `SerializeAsync`.

**Лишились синхронними**: доменні операції (`AddBook`, `IssueCopy`, `ReturnCopy` …) і `IBookStore` — вони працюють зі словником у пам'яті, чекати нічого; обгортка `Task` дала б лише накладні витрати. `ReportService` — чисті обчислення над колекціями. `FileBookStore` поки пише синхронно (`File.WriteAllText` після кожної зміни): контракт `IBookStore` асинхронним стане разом з EF Core на тижні 11 (`SaveChangesAsync`), щоб не міняти його двічі. Старі імпортери тижнів 2–4 (`BookCsvImporter`, `BookJsonImporter`) не чіпав — на них тримаються режими `--import`/`--domain`.

### 3. Фрагменти коду

`LoadAsync` ([JsonCatalogImporter.cs](src/Core/Storage/JsonCatalogImporter.cs)):

```csharp
ct.ThrowIfCancellationRequested(); // уже скасовано — файл навіть не відкриваємо
await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
dtos = await JsonSerializer.DeserializeAsync<List<BookDto>>(stream, Options, ct)
    ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку.");
for (var i = 0; i < dtos.Count; i++)
{
    ct.ThrowIfCancellationRequested();
    result.Add(ToBook(dtos[i], i + 1)); // ArgumentException → DomainException($"Запис {i + 1}: …", ex)
}
```

Обробка скасування ([Program.cs](src/Cli/Program.cs), `ImportCatalogAsync`):

```csharp
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += onCtrlC;                 // (_, e) => { e.Cancel = true; cts.Cancel(); }
try { int added = await Import(cts.Token); Say($"Імпортовано записів: {added}"); }
catch (OperationCanceledException) { Say("Імпорт скасовано."); }        // першим: це не збій
catch (TimeoutException ex) { Say($"Імпорт скасовано: {ex.Message}"); }
catch (DomainException ex) { Say($"Дані відхилено: {ex.Message}"); }
catch (StorageException ex) { Say($"Файл пошкоджено: {ex.Message}"); }
finally { Console.CancelKeyPress -= onCtrlC; }
```

Ланцюжок токена: `cts.Token` (Cli) → `TimeoutPolicy.RunAsync` (зв'язаний токен, якщо задано `--timeout`) → `LendingService.ImportAsync(importer, path, progress, ct)` → `importer.LoadAsync(path, ct)` → `DeserializeAsync(stream, Options, ct)`; плюс `ct.ThrowIfCancellationRequested()` у циклі `LoadAsync` і в циклі `ImportAsync`. Для потоку: `ImportAsync(books, ct)` → `books.WithCancellation(ct)` → параметр `[EnumeratorCancellation] ct` у `StreamAsync` → `DeserializeAsyncEnumerable(…, ct)`.

### 4. Таблиця тестів

| Тест | Що перевіряє | Очікуваний результат |
| :--- | :----------- | :------------------- |
| `IssueCopy_WhenCopyAlreadyIssued_ThrowsDomainException_AndKeepsState` | Варіант: «примірник уже видано» | `DomainException` з «вже виданий»; у сховищі одна видача, за R-2 — нуль |
| `ReturnCopy_WhenLoanAlreadyClosed_ThrowsDomainException_AndKeepsState` | Варіант: повернення невиданого примірника через сервіс | `DomainException`; дата першого повернення не змінилась, примірник на полиці |
| `Return_OfCopyOnShelf_ThrowsDomainException` | Те саме правило на рівні `BookCopy` | `DomainException`; `IsIssued == false` |
| `AddBook_WithYearOutOfRange_ThrowsArgumentOutOfRange_AndAddsNothing` (×2: 1449, 3000) | Поганий аргумент ≠ доменна помилка | `ArgumentOutOfRangeException`; сховище порожнє |
| `AddBook_WithIsbnAlreadyInCatalog_ThrowsDomainException` (×2: з дефісами і без) | Унікальність ISBN у каталозі | `DomainException`; у сховищі одне видання |
| `IssueCopy_OverReaderLimit_ThrowsDomainException` | Ліміт 5 незакритих видач | `DomainException`; активних видач рівно 5 |
| `AddBook_RaisesChangedExactlyOnce` | Подія тижня 6 | Рівно одна подія `BookAdded` з id нової книги |
| `IssueCopy_WhenRejected_RaisesNoEvent` | Подія лише після успішної зміни | Список подій порожній |
| `LowStock_CountsOnlyBelowThreshold` (×4: 0→0, 1→2, 4→3, 5→4) | Звіт на кількох наборах (`[Theory]`) | Кількість рядків дорівнює очікуваній |
| `TopBooks_IsDeterministic` | Топ-2 за кількістю примірників | `["B-BIG", "B-MID"]` |
| `TopBooks_OnTie_OrdersByBookId_RegardlessOfSourceOrder` | Другий ключ `ThenBy` | `["B-1", "B-2"]` і для прямого, і для оберненого джерела |
| `Totals_OnTestData_ComputesAggregates` | Агрегати | `(4, 9, 2.25, 5)` |
| `Totals_OnEmptySource_ReturnsZeros` | Порожнє джерело | `(0, 0, 0, 0)` без винятку |
| `ImportAsync_WhenAlreadyCancelled_Throws` | Заздалегідь скасований токен | `OperationCanceledException` (файл `any.json` навіть не відкривається) |
| `ImportAsync_WhenCancelledDuringLoop_StopsAfterCurrentRecord` | `ThrowIfCancellationRequested` у циклі | `OperationCanceledException`; додано рівно 1 з 4 |
| `ImportAsync_RaisesEventPerAddedBook_AndIsIdempotent` | Подія на кожен запис + ідемпотентність | Перший імпорт 4, другий 0; подій `BookAdded` — 4 |
| `ImportAsync_SkipsBookWithIsbnAlreadyInCatalog` | Інший id, той самий ISBN | Додано 0 |
| `LoadAsync_FileWithInvalidRecord_ThrowsDomainException_WithRecordNumber` | Тимчасовий файл із роком 2099 у записі 2 | `DomainException` «Запис 2: …», `InnerException` — `ArgumentOutOfRangeException` |
| `ImportAsync_FromFileWithInvalidRecord_AddsNothing` | Атомарність імпорту списком | `DomainException`; сховище порожнє |
| `StreamImport_StopsAtInvalidRecord_KeepingEarlierOnes` | Потоковий імпорт (дод. 1) | `DomainException` «Запис 3: …»; два перші записи вже додано |
| `LoadAsync_MalformedJson_ThrowsStorageException_KeepingJsonInner` | Технічна помилка не маскується під доменну | `StorageException`, `InnerException` — `JsonException` |
| `TimeoutPolicy_WhenTimeoutElapses_ThrowsTimeoutException` | Зв'язані токени: спрацював тайм-аут (дод. 2) | `TimeoutException`; токен користувача не скасовано |
| `TimeoutPolicy_WhenUserCancels_ThrowsOperationCanceled` | …а тут — користувач | `OperationCanceledException`, не `TimeoutException` |

Разом 22 тестові методи, 28 випадків (`[Theory]` дає окремий випадок на кожен `[InlineData]`). Файлова система — лише в чотирьох тестах `ImportTests` з тимчасовим файлом `core-tests-<guid>.json` у `Path.GetTempPath()`, який видаляється у `finally`.

### 5. Вивід

`dotnet test` (двічі поспіль — однаковий результат):

```text
Test run for ...\tests\Core.Tests\bin\Debug\net10.0\Core.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
Passed!  - Failed:     0, Passed:    28, Skipped:     0, Total:    28, Duration: 190 ms - Core.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    28, Skipped:     0, Total:    28, Duration: 168 ms - Core.Tests.dll (net10.0)
```

`dotnet run --project src/Cli -- --import-async` (рядок прогресу перемальовується через `\r`, у терміналі видно останній стан):

```text
Сховище: InMemoryBookStore
Імпорт data\catalog.json; Ctrl+C — скасувати
 Прогрес: 100% [####################]
Імпортовано записів: 17
Подій Changed: 17; видань у каталозі: 36
```

`--import-async --file`, перший і другий запуск (ідемпотентність):

```text
Сховище: FileBookStore
Файл: ...\src\Cli\bin\Debug\net10.0\data\library.json (є з попереднього запуску)
Імпорт data\catalog.json; Ctrl+C — скасувати
 Прогрес: 100% [####################]
Імпортовано записів: 17
Подій Changed: 17; видань у каталозі: 18
...
Імпортовано записів: 0
Подій Changed: 0; видань у каталозі: 18
```

Скасування тайм-аутом — `--import-async --big --timeout 2`:

```text
Сховище: InMemoryBookStore
Генерую 1,000,000 видань у C:\Users\Admin\AppData\Local\Temp\crossapp-catalog-1000000.json …
Імпорт C:\Users\Admin\AppData\Local\Temp\crossapp-catalog-1000000.json, тайм-аут 2 с; Ctrl+C — скасувати
Імпорт скасовано: Операцію перервано тайм-аутом 2 с.
```

Скасування Ctrl+C (`--import-async --big`, натиснути під час імпорту): гілка `catch (OperationCanceledException)` друкує рядок `Імпорт скасовано.` — без стектрейсу, код виходу 1. Тимчасовий файл видаляється у `finally` в обох випадках.

Зіпсовані й відсутні файли:

```text
> --import-async data/catalog_bad.json
Дані відхилено: Запис 3: Рік видання має бути в межах 1450..2026 (Parameter 'year')

> --import-async data/catalog_bad.json --stream
Дані відхилено: Запис 3: Рік видання має бути в межах 1450..2026 (Parameter 'year')

> --import-async data/sample.csv
Файл пошкоджено: Файл sample.csv не є коректним JSON-списком видань: 'i' is an invalid start of a value. Path: $ | LineNumber: 0 | BytePositionInLine: 0.

> --import-async data/nope.json
Файл не знайдено: data/nope.json
```

### 6. DomainException і ArgumentException у проєкті

`ArgumentException` (і `ArgumentOutOfRangeException`, `ArgumentNullException`) означає, що *викликач* передав методу значення, яке поза його контрактом, незалежно від стану бібліотеки: порожній ISBN або ISBN неправильної довжини (`IsbnRules.Normalize`), рік видання 2099 (`Book.Create`), порожній `readerId` (`IssueCopy`), дата повернення раніше дати видачі (`Loan.Close`). Такий виклик неправильний за будь-якого стану бібліотеки. `DomainException` означає, що аргументи коректні, але операцію забороняє *поточний стан* бібліотеки: «Примірник C-001 вже виданий, повторна видача неможлива» (`BookCopy.Issue`), «Примірник … і так на полиці, повернути його неможливо» (`BookCopy.Return`), «Видача … вже закрита» (`Loan`), дубль ISBN у каталозі, ліміт 5 видач читача, вилучення видання з примірниками, «Немає примірника з id=…» (`LendingService`). Той самий виклик `IssueCopy("C-001", "R-2", …)` учора був би успішним. Межа проходить і через імпорт: у `Book.FromDto` рік 2099 — це `ArgumentOutOfRangeException`, але для `JsonCatalogImporter` це вже погані дані каталогу, тож він загортає їх у `DomainException("Запис 3: …", ex)`, а початковий виняток лишає в `InnerException`. Третя категорія — технічні збої (`FileNotFoundException`, `JsonException` → `StorageException`, `InvalidDataException` у `FileBookStore`, `InvalidOperationException` сховища на дубль id): їх не видаю за доменні, бо з правилами бібліотеки вони не пов'язані, і реакція на них інша («перевірте файл», а не «примірник уже видано»).

### 7. Додаткові завдання

1. **`IAsyncEnumerable<Book>` + `[EnumeratorCancellation]`.** `JsonCatalogImporter.StreamAsync` віддає видання по одному з `JsonSerializer.DeserializeAsyncEnumerable`; `LendingService.ImportAsync(IAsyncEnumerable<Book>, ct)` перебирає їх через `await foreach (… in books.WithCancellation(ct))`, і цей токен через атрибут потрапляє в параметр `StreamAsync`. Оскільки `yield return` не можна писати в `try` з `catch`, `MoveNextAsync` викликається вручну в маленькому `try`, щоб загорнути `JsonException`. Ціна потоковості — неатомарність: тест `StreamImport_StopsAtInvalidRecord_KeepingEarlierOnes` фіксує, що записи до поганого вже в каталозі, а `ImportAsync_FromFileWithInvalidRecord_AddsNothing` — що варіант зі списком не додає нічого. Запуск: `--import-async --stream`.
2. **Зв'язані токени.** `TimeoutPolicy.RunAsync` створює `CancellationTokenSource(timeout)` і `CreateLinkedTokenSource(userToken, timeoutToken)`; якщо скасування спричинив тайм-аут, а не користувач (`timeoutSource.IsCancellationRequested && !userToken.IsCancellationRequested`), кидає `TimeoutException` з `OperationCanceledException` в `InnerException`. Тести `TimeoutPolicy_WhenTimeoutElapses_ThrowsTimeoutException` і `TimeoutPolicy_WhenUserCancels_ThrowsOperationCanceled` на `NeverEndingImporter` (`Task.Delay(Infinite, ct)`) доводять, яке з джерел спрацювало. Запуск: `--timeout 2`.
3. **`IProgress<int>`.** `ImportAsync(importer, path, progress, ct)` повідомляє відсоток опрацьованих записів лише тоді, коли він змінився (на мільйоні записів — 101 виклик, а не мільйон). Підписник `ConsoleProgress` у Cli малює `Прогрес:  42% [########            ]` в одному рядку через `\r`. Це власний синхронний клас, а не `Progress<T>`: той у консолі викликає обробник через пул потоків, і «Імпортовано записів» могло б надрукуватися раніше за останні відсотки. Тест `ImportAsync_WhenCancelledDuringLoop_…` використовує той самий прийом (`CallbackProgress`), щоб скасувати імпорт після першого запису.
4. **Покриття** (`dotnet test --collect:"XPlat Code Coverage"`, coverlet із шаблону xunit): усього по `Core` — **37,6 % рядків, 20,3 % гілок**. Новий код тижня 8 покритий добре: `JsonCatalogImporter.LoadAsync` 89 %, `StreamAsync` 78 %, `TimeoutPolicy` 91 %, `LendingService.ImportAsync` 82–100 %, `DomainException`/`StorageException` 100 %; `BookCopy` 91 %, `Book` 85 %, `LendingService` загалом 75 %. Непокриті (0 %) — імпортери CSV/JSON тижнів 2–4, `FileBookStore`, `CachingBookStore`, `SampleData`, `EnvironmentInfo`, `ReportBenchmarks`, а також частина `ReportService` (40 %: `Overdue`, `ByDecade`, `Join`-звіти) і `Loan.FromDto` (52 %). Поки це прийнятно: CSV-імпортери — код лаб 2–4, який перевірено вручну виводами з README_labs1-4 і який не входить у ланцюжок тижнів 10–11; `FileBookStore` — інфраструктура, яку на тижні 11 замінить EF Core (тестуватиметься інтеграційно, а не модульно); `SampleData`/`EnvironmentInfo`/`ReportBenchmarks` — демо-дані й діагностика без бізнес-правил. Наступні кандидати на тести — `ReportService.Overdue` і `Loan.FromDto` (правила узгодженості статусу й дат).

### 8. Самоперевірка

1. `grep -rnE "\.Result\b|\.Wait\(\)|async void|Task\.Run" src tests --include=*.cs` — єдиний збіг — коментар у `JsonCatalogImporter.cs` («без Task.Run навколо File.ReadAllText»). Обробник `Console.CancelKeyPress` — звичайна синхронна лямбда; тести — `async Task`.
2. `dotnet test` двічі поспіль — 28/28 обидва рази; кожен тест створює власне `InMemoryBookStore`, `TestData` щоразу повертає нові об'єкти, статичного змінного стану немає.
3. Жоден тест не читає `data/`; тимчасові файли мають унікальні імена й видаляються у `finally` (`WithTempFileAsync`).
4. Кожен тест має щонайменше один `Assert` на конкретний тип (`Assert.Throws<DomainException>`, а не `<Exception>`); `ThrowsAnyAsync<OperationCanceledException>` — свідомо, бо скасування може прийти як `TaskCanceledException`.
5. Токен передано в кожен `await`: `LoadAsync(path, ct)`, `DeserializeAsync(…, ct)`, `DeserializeAsyncEnumerable(…, ct)`, `WithCancellation(ct)`, `SerializeAsync(…, ct)`, `Task.Delay(…, ct)`; у циклах — `ThrowIfCancellationRequested`.

`dotnet build` — `0 Warning(s)`, `0 Error(s)`; режими `--events`, `--service`, `--sinks`, `--import`, `--domain`, `--info`, `report-all [--file|--cache|--empty]` працюють, як раніше.

### 9. Definition of Done (Лабораторна робота 8)

- [x] Проєкт `tests/Core.Tests` у solution, посилання лише на `Core`.
- [x] Асинхронне читання через `FileStream(useAsync: true)` + `DeserializeAsync`, без `Task.Run`.
- [x] `CancellationToken` у сигнатурі, перевіряється в циклі і передається далі (розділ 3).
- [x] Cli реагує на Ctrl+C і тайм-аут повідомленням, а не стектрейсом.
- [x] Власний `DomainException` для бізнес-помилок (`BookCopy`, `Loan`, `LendingService`).
- [x] ≥ 5 тестів (22 методи / 28 випадків): інваріанти, подія, звіт, скасування; `dotnet test` зелений.
- [x] Немає `async void`, `.Result`, `.Wait()`.
- [x] README: як запустити тести і як перевірити скасування. Коміт `lab08` — після перевірки.
- [x] Виконано всі 4 додаткові завдання.

### 10. Відповіді на питання на захисті

1. **Що насправді робить await? Чи створює він потік?** Не створює. `await` перевіряє, чи задача вже завершена; якщо ні — реєструє решту методу як продовження (continuation) і повертає керування викликачу, а потік вільний для іншої роботи. Коли ОС повідомить про завершення вводу-виводу (порт завершення I/O), продовження виконається на потоці з пулу (у консолі контексту синхронізації немає). Під час читання файлу каталогу жоден потік не «сидить» і не чекає на диск.
2. **Чому Task.Run навколо File.ReadAllText — не асинхронний ввід-вивід?** `File.ReadAllText` блокує потік на весь час читання; `Task.Run` лише переносить це блокування з поточного потоку на потік пулу. Зайнятий потік так і лишається, а на сервері (тиждень 10) це забирає потоки в обробки запитів. Справжній async — `FileStream(useAsync: true)`/`ReadAllTextAsync`: запит іде в ОС, потік звільняється.
3. **Чим небезпечні .Result і .Wait() у консольному застосунку?** Вони блокують потік до завершення задачі (витрачаючи його даремно) і загортають виняток в `AggregateException`. У середовищах із контекстом синхронізації (WinForms/WPF, старий ASP.NET) — класичний deadlock: потік чекає задачу, а продовження задачі чекає цей самий потік. У консолі deadlock рідший, але звичка переходить у Blazor/Api. Тому — top-level `await`.
4. **Чому async void заборонений і чому обробники подій — виняток?** `async void` не повертає `Task`: його не можна дочекатися, не можна дізнатися, коли він завершився, а виняток із нього летить у контекст синхронізації/пул і валить процес — `try/catch` у місці виклику його не бачить. Обробники подій мусять мати сигнатуру `void (object, EventArgs)`, тому там `async void` допустимий, але всередині слід ловити винятки самому. У моєму коді його немає зовсім: обробник `CancelKeyPress` синхронний (лише `cts.Cancel()`), тести — `async Task`.
5. **OperationCanceledException — це помилка? Як обробляєте і чому окремим catch?** Ні, це очікуваний результат: користувач сам попросив зупинитися. Тому `catch (OperationCanceledException)` стоїть першим і друкує «Імпорт скасовано.», а не «помилку»; якби першим був `catch (Exception)`, скасування виглядало б як збій. У `Core` OCE ніде не ловиться і не загортається в `DomainException` — проходить наскрізь (`catch (ArgumentException)` у `ToBook` його не зачіпає).
6. **Хто створює CancellationToken, а хто його перевіряє?** Створює і скасовує власник `CancellationTokenSource` — `Program.cs` (Ctrl+C), `TimeoutPolicy` (тайм-аут), тест (`CancelAsync`). Перевіряють операції, що його отримали: `LendingService.ImportAsync` і `JsonCatalogImporter` (`ThrowIfCancellationRequested` у циклах), а також BCL-методи, яким його передано (`DeserializeAsync`, `Task.Delay`). Сам токен може лише «слухати», скасувати через нього не можна.
7. **Коли кидати DomainException, а коли ArgumentException?** Див. розділ 6. Коротко: `ArgumentException` — значення неправильне за будь-якого стану (`Book.Create(…, year: 2099)` → `ArgumentOutOfRangeException`); `DomainException` — значення правильне, але правило бібліотеки забороняє операцію зараз (`IssueCopy` виданого примірника → «Примірник … вже виданий»).
8. **Навіщо зберігати inner exception при обгортанні JsonException?** У `JsonException` — точне місце помилки (`Path: $[3].year | LineNumber: 12 | BytePositionInLine: 18`) і стек серіалізатора. Без `InnerException` лишилося б «файл поганий» без жодної підказки, де саме; у лозі (тиждень 14) потрібен саме ланцюжок. Тест `LoadAsync_MalformedJson_…` перевіряє, що inner на місці.
9. **Чому в тестах сховище в пам'яті, а не файлове?** Тест перевіряє правило сервісу, а не диск: `InMemoryBookStore` швидкий, не залежить від прав доступу, шляхів, стану файлу з попереднього запуску і від паралельних тестів. Кожен тест отримує свіже сховище — порядок запуску неважливий. Це те, заради чого на тижні 5 з'явився `IBookStore`. Файлове сховище — інфраструктура, її перевіряють інтеграційними тестами окремо.
10. **Що дає [Theory] з [InlineData] порівняно з кількома [Fact]?** Одна логіка перевірки — багато наборів даних: `LowStock_CountsOnlyBelowThreshold` перевіряє межу 0/1/4/5 одним методом замість чотирьох копій. Кожен `[InlineData]` — окремий випадок у звіті (`threshold: 4, expected: 3`), тож видно, який саме набір упав; додати набір — один рядок.
11. **Як перевірили тестом, що подія піднялася рівно один раз?** Підписав лямбду, яка додає `EventArgs` у `List<LibraryChangedEventArgs>`, виконав `AddBook` і викликав `Assert.Single(events)`: він падає і на 0, і на 2+ подіях, а заодно повертає елемент для перевірки `Kind == BookAdded` і `EntityId`. Тесту не потрібні ні консоль, ні диск: подія — це просто виклик делегата, а сховище — у пам'яті. Парний тест `IssueCopy_WhenRejected_RaisesNoEvent` перевіряє `Assert.Empty` після відмови.

### 11. Що змінилося в коді попередніх лабораторних

- Бізнес-правила в `BookCopy`, `Loan`, `LendingService` кидають `DomainException` замість `InvalidOperationException`; тексти повідомлень не змінилися. Перевірки аргументів лишилися `ArgumentException`. `InvalidOperationException` лишився тільки у сховищ (дубль id).
- `EventsDemo`, `ServiceDemo`, `SinkDemo`, `EntityImport` перехоплюють `DomainException` (`ServiceDemo` — ще й `InvalidOperationException` для демонстрації «дубль id у сховищі»).
- Розділ лабораторної 7 перенесено в [README_lab07.md](README_lab07.md).
