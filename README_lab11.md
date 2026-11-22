# CrossApp — Лабораторна робота 11

> Повернутися до [README.md](README.md). На тижні 12 до API додано другий клієнт — Blazor (`src/Web`), CORS-політику і параметр `?sort=`; `BookDto`, `SaveBookRequest`, `PagedResponse<T>` перенесено в спільний проєкт `src/Contracts`.

## Лабораторна робота 11. EF Core + SQLite: міграції, зв'язок 1:N, заміна сховища

**Мета**: перевести `src/Api` на постійне сховище, що однаково працює у Windows, Linux і macOS, не змінюючи HTTP-маршрутів і контракту API. Сховище замінено через уже наявний інтерфейс Core, а схема БД створюється і змінюється міграціями, які лежать у git.

**Варіант (бібліотека)**: `Book 1:N Copy` (інвентарний номер, чи виданий), унікальний ISBN; плюс `Copy 1:N Loan` (журнал видач) другою міграцією.

Додаткові завдання, виконані в цій роботі:

1. **Наскрізний async-контракт** — `IBookStoreAsync` у Core (`ListAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `RemoveAsync`, `InTransactionAsync`). `EfBookStore` реалізує його через `ToListAsync` / `SaveChangesAsync`, `CatalogService` викликає лише асинхронні методи. Синхронний `IBookStore` тижня 5 лишився без змін.
2. **Друга міграція `AddLoans`** — таблиця `Loans` з FK на `Copies` (`RESTRICT`). Це показує, як схема еволюціонує, як працює `Down` (тест відкату) і що генерує `migrations script`.
3. **Стартові дані** — `DbSeeder` заповнює лише порожню БД (19 видань, 52 примірники, 21 видача); повторний запуск нічого не дублює.
4. **Керований конфлікт замість 500** — `UNIQUE` / `FOREIGN KEY` з SQLite (`DbUpdateException`) перекладається в `DomainException` → **409**. Тест: 10 паралельних POST з одним ISBN дають рівно один 201, решта отримують 409.
5. **Інтеграційні тести на справжній SQLite** — новий `tests/Infrastructure.Tests` (20 тестів) і `tests/Api.Tests`, переведений на тимчасові файли SQLite (21 тест, із них 3 нові, зокрема тест «перезапуску» API).

### Запуск і міграції

Інструмент (один раз на машину): `dotnet tool install --global dotnet-ef --version "10.*"`; перевірка: `dotnet ef --version` → `10.0.15`.

| Команда (з кореня репозиторію) | Що робить |
| :----------------------------- | :-------- |
| `dotnet ef database update --project src/Infrastructure --startup-project src/Api` | Створює/оновлює `src/Api/data/app.db` до останньої міграції |
| `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api` | Нова міграція після зміни моделі (`Persistence/*`) |
| `dotnet ef migrations list --project src/Infrastructure --startup-project src/Api` | `20261116173406_InitialCreate`, `20261116173414_AddLoans` |
| `dotnet ef migrations script --project src/Infrastructure --startup-project src/Api -o schema.sql` | SQL-скрипт усіх міграцій (для Production) |
| `dotnet ef database update InitialCreate ...` | Відкат до `InitialCreate` (виконує `Down` міграції `AddLoans`) |
| `dotnet run --project src/Api` | API на `http://localhost:5080`; у Development міграції й сідування виконуються при старті |
| `sqlite3 src/Api/data/app.db ".tables"` | `Books  Copies  Loans  __EFMigrationsHistory  __EFMigrationsLock` |
| `dotnet test` | 95 тестів: 54 Core + 20 Infrastructure + 21 Api |

**Рядок підключення** — ключ `ConnectionStrings:AppDb` (`src/Api/appsettings.json`), за замовчуванням `Data Source=data/app.db`. Відносний шлях рахується від `ContentRootPath` (`src/Api`), а не від поточного каталогу, тому `dotnet run` і `dotnet ef` працюють з одним і тим самим файлом. Перевизначення: змінна середовища `ConnectionStrings__AppDb="Data Source=/tmp/library.db"`.

**Як скинути БД:**

```bash
dotnet ef database drop --force --project src/Infrastructure --startup-project src/Api   # або просто видалити src/Api/data/app.db
dotnet ef database update --project src/Infrastructure --startup-project src/Api         # схема з міграцій
dotnet run --project src/Api                                                             # Development: засіє SampleData
```

`Catalog__Seed=false` вимикає сідування, тоді каталог після скидання порожній. Поза Development міграції при старті не запускаються; їх можна ввімкнути через `Database__MigrateOnStartup=true`, але в Production правильніше застосовувати `database update` або SQL-скрипт.

### 1. Структура і напрямок залежностей

```
CrossApp/
├── src/
│   ├── Core/                           ← без EF Core (Core.csproj без жодного PackageReference)
│   │   ├── Abstractions/IBookStore.cs        ← без змін (тиждень 5)
│   │   ├── Abstractions/IBookStoreAsync.cs   ← НОВЕ: async-контракт (дод. завд. 1)
│   │   ├── Storage/SyncBookStoreAdapter.cs   ← НОВЕ: IBookStore → IBookStoreAsync (InMemory/File)
│   │   └── Services/CatalogService.cs        ← async над IBookStoreAsync, кожна зміна в транзакції
│   │       Services/CatalogWriteGate.cs      ← НОВЕ: замок «одна зміна за раз» (Singleton)
│   ├── Infrastructure/                 ← НОВЕ: усе, що знає про EF Core
│   │   ├── Persistence/Rows.cs               ← BookRow, CopyRow, LoanRow (persistence-моделі)
│   │   ├── Persistence/AppDbContext.cs       ← DbContext + Fluent API
│   │   ├── Persistence/RowMapping.cs         ← Row ↔ домен через Book.Create / FromDto
│   │   ├── Persistence/DbSeeder.cs           ← стартові дані в порожню БД
│   │   ├── Persistence/SqliteConnectionStrings.cs ← відносний Data Source → абсолютний від ContentRoot
│   │   ├── EfBookStore.cs                    ← IBookStore + IBookStoreAsync над AppDbContext
│   │   └── Migrations/                       ← InitialCreate, AddLoans, AppDbContextModelSnapshot (у git)
│   └── Api/                            ← Program.cs: AddDbContext + AddScoped; маршрути без змін
└── tests/
    ├── Core.Tests/                     ← без змін (54)
    ├── Infrastructure.Tests/           ← НОВЕ (20): міграції, FK, Down, EfBookStore, async, транзакції
    └── Api.Tests/                      ← ApiFactory: кожен тест — свій файл SQLite (21)
```

`Core` ← `Infrastructure` (`Microsoft.EntityFrameworkCore.Sqlite` 10.0.15, `…Design`) ← `Api` (`…Design` окремо, бо в Infrastructure він `PrivateAssets=all` і транзитивно не передається). Тест `Core_DoesNotDependOnEntityFramework` перевіряє і `Core.csproj`, і посилання збірки Core.

### 2. Схема

| Таблиця | Стовпці | Ключі й обмеження |
| :------ | :------ | :---------------- |
| `Books` | `Id` TEXT(32) NOCASE, `Isbn` TEXT(20), `IsbnKey` TEXT(13), `Title` TEXT(200), `Year` INTEGER, `Author` TEXT(200) NULL | PK `Id`; **UNIQUE** `IX_Books_IsbnKey` |
| `Copies` | `Id` TEXT(32) NOCASE, `BookId` TEXT(32), `IsIssued` INTEGER, `RegisteredAt` TEXT | PK `Id`; **FK** `BookId → Books.Id` **ON DELETE CASCADE**; індекс `IX_Copies_BookId` |
| `Loans` | `Id`, `CopyId`, `ReaderId`, `IssuedOn` TEXT (DateOnly), `ReturnedOn` NULL, `Status` TEXT (`Open`/`Lost`/`Closed`), `LoanDays` | PK `Id`; **FK** `CopyId → Copies.Id` **ON DELETE RESTRICT**; індекси `CopyId`, `ReaderId` |

`IsbnKey` — це ISBN без дефісів. Унікальний індекс стоїть на ньому, а не на `Isbn`, бо `978-0-13-235088-4` і `9780132350884` — одне видання (у домені так само міркує `Book.HasIsbn`). `COLLATE NOCASE` на ключах зберігає поведінку `InMemoryBookStore` (`b-001` = `B-001`).

```csharp
// src/Infrastructure/Persistence/AppDbContext.cs (фрагмент)
book.HasIndex(b => b.IsbnKey).IsUnique();
book.HasMany(b => b.Copies).WithOne(c => c.Book)
    .HasForeignKey(c => c.BookId).IsRequired()
    .OnDelete(DeleteBehavior.Cascade);
copy.HasMany(c => c.Loans).WithOne(l => l.Copy)
    .HasForeignKey(l => l.CopyId).IsRequired()
    .OnDelete(DeleteBehavior.Restrict);
```

### 3. Реєстрація в DI (єдине, що змінилося для маршрутів)

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, o) => o.UseSqlite(SqliteConnectionStrings.Resolve(
    sp.GetRequiredService<IConfiguration>().GetConnectionString("AppDb"),
    sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath)));      // Scoped
builder.Services.AddScoped<EfBookStore>();
builder.Services.AddScoped<IBookStore>(sp => sp.GetRequiredService<EfBookStore>());
builder.Services.AddScoped<IBookStoreAsync>(sp => sp.GetRequiredService<IBookStore>().AsAsync());
builder.Services.AddSingleton<CatalogWriteGate>();
builder.Services.AddScoped(sp => new CatalogService(sp.GetRequiredService<IBookStoreAsync>(),
                                                    sp.GetRequiredService<CatalogWriteGate>()));
```

| Сервіс | Тиждень 10 | Тиждень 11 | Чому |
| :----- | :--------- | :--------- | :--- |
| `AppDbContext` | — | **Scoped** | Не потокобезпечний; одиниця роботи одного запиту |
| `IBookStore` / `IBookStoreAsync` | Singleton (`SynchronizedBookStore`) | **Scoped** (`EfBookStore`) | Живе поверх `DbContext`; дані тепер у файлі, а не в об'єкті |
| `CatalogService` | Singleton | **Scoped** | Singleton над Scoped був би «полоненою залежністю» |
| `CatalogWriteGate` | (поле сервісу) | **Singleton** | Один замок на процес; між процесами захищають UNIQUE і транзакція |

### 4. Перевірка збереження між запусками (крок 8)

```text
$ rm src/Api/data/app.db; dotnet ef database update --project src/Infrastructure --startup-project src/Api
Applying migration '20261116173406_InitialCreate'.
Applying migration '20261116173414_AddLoans'.
Done.
$ sqlite3 src/Api/data/app.db ".tables"
Books                  Loans                  __EFMigrationsLock
Copies                 __EFMigrationsHistory

$ dotnet run --project src/Api        # Development: Migrate() + сідування 19 видань
$ curl -i -X POST http://localhost:5080/api/books -H "Content-Type: application/json" --data-binary @samples/api/book.json
HTTP/1.1 201 Created
Location: /api/books/B-111ebbad
{"id":"B-111ebbad","isbn":"978-617-12-3456-7","title":"Кобзар","author":"Тарас Шевченко","year":1840,"copies":2,"available":2}
$ curl -i -X POST ... (той самий ISBN)
HTTP/1.1 409 Conflict
{"title":"Операція суперечить правилам бібліотеки.","status":409,"detail":"Видання з ISBN 978-617-12-3456-7 уже є в каталозі: B-111ebbad.", …}

$ sqlite3 -header -column src/Api/data/app.db "SELECT Id, BookId, IsIssued, RegisteredAt FROM Copies WHERE BookId='B-111ebbad'"
Id          BookId      IsIssued  RegisteredAt
C-b4760ead  B-111ebbad  0         2026-11-16 17:37:55.1479695+00:00
C-f0f72d26  B-111ebbad  0         2026-11-16 17:37:55.149637+00:00

# Ctrl+C, повторний dotnet run --project src/Api
$ curl -i http://localhost:5080/api/books/B-111ebbad
HTTP/1.1 200 OK
{"id":"B-111ebbad","isbn":"978-617-12-3456-7","title":"Кобзар","author":"Тарас Шевченко","year":1840,"copies":2,"available":2}
$ curl -s "http://localhost:5080/api/books?pageSize=1"
{"items":[…],"total":20,"page":1,"pageSize":1}     ← 19 стартових + 1 новий, сідування не повторилося
```

### 5. Чому міграції, а не `EnsureCreated`

`EnsureCreated` уміє лише одне: створити схему в порожній БД за поточною моделлю. Якщо таблиці вже є, він нічого не робить і про зміни моделі не знає, тому додати `Loans` до бази, де вже лежать видання, він не зможе: доведеться видаляти файл разом з даними. Міграція — це клас з `Up` (перехід до нової версії схеми) і `Down` (відкат). Номер застосованої версії записується в `__EFMigrationsHistory`, тож `database update` на будь-якій копії (чистій, вчорашній, продакшен) виконує лише ті кроки, яких там ще немає. Ми це й побачили: `AddLoans` додав одну таблицю, не чіпаючи `Books`/`Copies` з даними. Тест `MigrateDown_ToInitialCreate_DropsOnlyLoans` перевіряє відкат. Міграції — це код, який проходить рев'ю в git разом з моделлю, і з них можна згенерувати SQL-скрипт для адміністратора. `EnsureCreated` годиться для прототипів і одноразових тестових баз, але в одному проєкті з міграціями його не використовують: створена ним база не має історії, і `Migrate` спробує створити таблиці ще раз.

### 6. Самоперевірка

| Питання | Результат |
| :------ | :-------- |
| У `Core.csproj` немає пакетів EF Core? | Так, немає жодного `PackageReference` (тест `Core_DoesNotDependOnEntityFramework`) |
| `Migrations` у git, `data/app.db` — ні? | Так, `git status` не показує `app.db`: у `.gitignore` є `**/data/`, `*.db`, `*.db-shm`, `*.db-wal` |
| Видалили `app.db` → `database update` відтворив схему без правок коду? | Так (розділ 4; тест `Migrate_OnEmptyFile_CreatesTablesAndHistory`) |
| Дані живуть після перезапуску; `Book` відновлюється через `Book.Create`? | Так (розділ 4; `RowMapping.ToDomain`; тести `Data_SurvivesApiRestart`, `CorruptedRow_FailsInBookCreate_NotSilently`) |
| Повторний POST з тим самим ISBN — керована помилка, а не 500? | 409 (розділ 4; тест `ConcurrentPosts_SameIsbn_OneCreated_Rest409`) |
| Маршрути тижня 10 не змінилися? | Так: `BookEndpoints.cs`, DTO і валідатори без змін, 18 старих тестів Api проходять |

### 7. Definition of Done (Лабораторна робота 11)

1. `InitialCreate` (і `AddLoans`) у git; `database update` на чистій копії працює.
2. CRUD Api читає і пише в SQLite, дані живуть між запусками.
3. 1:N описаний у Fluent API і видимий у міграції (`FK_Copies_Books_BookId`, `ON DELETE CASCADE`).
4. Рядок підключення взято з `ConnectionStrings:AppDb`, шлях будується через `Path.Combine` від `ContentRootPath`.
5. `DbContext` Scoped, сховище реалізує інтерфейс з Core, у Core немає EF Core.
6. `*.db` у `.gitignore`, README з командами; `dotnet build` (0 попереджень) і `dotnet test` (95/95) зелені; коміт `lab11`.

### 8. Відповіді на питання на захисті

1. **DbContext і Scoped.** Одиниця роботи: з'єднання з БД, трекер змін завантажених сутностей і формування SQL на `SaveChanges`. Він не потокобезпечний: два паралельні запити через один екземпляр дають «A second operation was started on this context». Scoped дає кожному HTTP-запиту власний контекст, а кеш трекера не росте весь час роботи застосунку.
2. **Міграції vs `EnsureCreated`** — розділ 5.
3. **Клас міграції.** `Up` містить `CreateTable` / `AddColumn` / `CreateIndex`, `Down` робить зворотні дії (`DropTable`), `.Designer.cs` зберігає знімок моделі на момент міграції, а `AppDbContextModelSnapshot` — поточний (з ним порівнюється наступний `migrations add`). `__EFMigrationsHistory` — список застосованих `MigrationId`, за ним `update` вирішує, що ще треба виконати.
4. **LINQ to Entities vs LINQ to Objects.** Перший працює з `IQueryable`: дерево виразів провайдер перекладає в SQL, і фільтр виконується в SQLite. Другий працює з `IEnumerable` у пам'яті. В `EfBookStore` `Where/OrderBy/Select` (з JOIN `Copies ⋈ Books`) виконуються в БД, а після `ToList` перетворення `RowMapping.ToDomain` працює як LINQ to Objects, бо фабрику домену в SQL не перекласти.
5. **Чому `BookRow`, а не `Book`.** `Book` має приватний конструктор, get-only властивості й перевірки в `Create`. Щоб EF мапив його напряму, довелося б послабити домен або заповнювати поля рефлексією в обхід інваріантів. `BookRow` — простий клас із сетерами й навігацією `Copies`, а повернення в домен іде лише через `Book.Create`, тож зіпсований рядок дає виняток, а не невалідний об'єкт.
6. **AsNoTracking.** Його варто вживати, коли рядок лише читається для побудови DTO чи доменного об'єкта (усі `List`/`GetById`). Він вимикає реєстрацію сутностей у ChangeTracker і знімки для виявлення змін, тож читання швидше й займає менше пам'яті, але такі об'єкти не можна просто змінити й зберегти через `SaveChanges`.
7. **1:N у Fluent API.** `HasMany(b => b.Copies).WithOne(c => c.Book).HasForeignKey(c => c.BookId)`. `DeleteBehavior.Cascade` означає, що видалення видання видаляє і його примірники (у схемі `ON DELETE CASCADE`). Для видач обрано `Restrict`, бо історію видавання не можна стирати мовчки.
8. **Файл БД vs міграції.** `app.db` — це дані конкретної машини: бінарний файл, який не зливається в git і відтворюється командою `database update`. Міграції — це код схеми, однаковий для всіх, тому вони в git. `SaveChanges` переглядає трекер, генерує INSERT/UPDATE/DELETE для доданих, змінених і видалених сутностей і виконує їх в одній транзакції.

### 9. Що змінилося в коді попередніх лабораторних

- `CatalogService` працює над `IBookStoreAsync` і кожну зміну загортає в `InTransactionAsync`. Замок винесено в `CatalogWriteGate`. Конструктор `CatalogService(IBookStore)` лишився (через `SyncBookStoreAdapter`), тому тести Core не змінено.
- `Api/CatalogOptions`: прибрано вибір `Memory | File`, лишився `Seed`. У `appsettings.json` секцію `Storage` замінено на `ConnectionStrings:AppDb` і `Database:MigrateOnStartup`.
- `Api.Tests`: `new WebApplicationFactory<Program>()` → `new ApiFactory()` (тимчасовий файл SQLite на тест). Тіла 18 тестів тижня 10 не змінилися.
- `SynchronizedBookStore`, `InMemoryBookStore`, `FileBookStore` і Cli не змінювалися; Cli, як і раніше, працює з файловим сховищем.
- Опис лабораторної 10 перенесено в [README_lab10.md](README_lab10.md).
