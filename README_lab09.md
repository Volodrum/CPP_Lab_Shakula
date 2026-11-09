# CrossApp — лабораторна робота 9

> Актуальний опис проєкту (лабораторна 10) — у [README.md](README.md). Команди тижня 9 (`config`, `export`, `import`) працюють без змін.

**Мета**: зробити застосунок кросплатформним — жодних абсолютних Windows-шляхів у коді, жодних паролів у Git; налаштування з `appsettings.json` з перевизначенням змінними середовища; експорт та імпорт усієї колекції у JSON.

**Варіант (бібліотека)**: колекція видань (isbn, title, author, year) з примірниками й видачами + ключ `Library:LoanDays` (строк видачі).

### Запуск

| Команда | Що відбувається |
| :------ | :-------------- |
| `dotnet run --project src/Cli -- config` | Звідки прочитано налаштування: середовище, рівень журналу, шлях до даних, `Library:LoanDays`, наявність ApiKey |
| `dotnet run --project src/Cli -- export` | Уся колекція (19 видань, 52 примірники, 21 видача) → файл даних із конфігурації |
| `dotnet run --project src/Cli -- export my.json` | Те саме у вказаний файл (шлях з консолі — відносно поточного каталогу) |
| `dotnet run --project src/Cli -- import [шлях]` | Читає колекцію назад (за замовчуванням — файл даних), друкує підсумки |
| `dotnet run --project src/Cli -- report-all --file` | Звіти тижня 7 над `FileBookStore`, що лежить у тому самому файлі даних |
| `dotnet run --project src/Cli -- config --Library:LoanDays=30` | Аргумент командного рядка перекриває всі інші джерела |
| `dotnet test` | 42 тести `tests/Core.Tests` (28 тижня 8 + 14 нових у `StorageConfigTests`) |

### 1. Структура після лабораторної

```
CrossApp/
├── samples/                      ← вхідні файли лаб 3–8 (раніше data/); копіюються в bin/…/samples
├── src/
│   ├── Core/                     ← без пакетів конфігурації: домен не знає про JSON-файли налаштувань і user-secrets
│   │   ├── Configuration/AppOptions.cs   ← AppOptions, StorageOptions (+ResolveFullPath), LoggingOptions, LibraryOptions
│   │   ├── Storage/LibraryJson.cs        ← формат файлу колекції: camelCase, кирилиця без \uXXXX, .tmp + File.Move
│   │   ├── Storage/FileBookStore.cs      ← шлях ззовні, читання/запис через LibraryJson
│   │   └── Domain/Loan.cs, Dto/LoanDto.cs ← строк видачі LoanDays зберігається в кожній видачі
│   └── Cli/
│       ├── appsettings.json              ← у git, без секретів (профіль Production)
│       ├── appsettings.Development.json  ← детальніший журнал, дані поруч зі збіркою
│       ├── AppSettings.cs                ← ConfigurationBuilder, CROSSAPP_DATA_PATH, ConsoleLog
│       ├── CollectionCommands.cs         ← config / export / import
│       └── Cli.csproj                    ← пакети Microsoft.Extensions.Configuration.*, <UserSecretsId>
├── tests/Core.Tests/StorageConfigTests.cs
└── data/                                 ← не існує в git: створюється програмою, ігнорується (.gitignore)
```

### 2. Профілі Development і Production

Профіль обирає змінна `CROSSAPP_ENVIRONMENT` (без неї — `Development`). `appsettings.json` задає Production-значення, `appsettings.{Environment}.json` перекриває лише те, що відрізняється.

| | Development (за замовчуванням) | Production (`CROSSAPP_ENVIRONMENT=Production`) |
| :-- | :-- | :-- |
| Журнал `Logging:MinimumLevel` | `Debug` — друкує cwd, каталог застосунку, ланцюжок джерел | `Warning` — лише попередження |
| Дані `Storage:Root` + `DataPath` | `App` + `data` → `src/Cli/bin/Debug/net10.0/data/library.json` | `LocalAppData` + `CrossApp` → `%LOCALAPPDATA%\CrossApp\library.json` (Linux: `~/.local/share/CrossApp/library.json`) |
| `Library:LoanDays` | 21 | 14 |
| user-secrets | підключені (`secrets.json` у профілі користувача) | не підключені: секрет лише зі змінної середовища |

### 3. Таблиця ключів

| Ключ у JSON | Змінна середовища | За замовчуванням | Призначення |
| :---------- | :---------------- | :--------------- | :---------- |
| `EnvironmentName` | `CROSSAPP_EnvironmentName` | `Production` | Назва профілю в самому файлі (для перевірки, який файл підхопився) |
| `Logging:MinimumLevel` | `CROSSAPP_Logging__MinimumLevel` | `Warning` / `Debug` | `Debug`, `Information`, `Warning`, `Error` |
| `Storage:Root` | `CROSSAPP_Storage__Root` | `LocalAppData` / `App` | Базовий каталог для відносного `DataPath` |
| `Storage:DataPath` | `CROSSAPP_Storage__DataPath` або **`CROSSAPP_DATA_PATH`** | `CrossApp` / `data` | Каталог даних; абсолютний шлях використовується як є |
| `Storage:FileName` | `CROSSAPP_Storage__FileName` | `library.json` | Лише ім'я файлу, без каталогів |
| `Library:LoanDays` | `CROSSAPP_Library__LoanDays` | 14 / 21 | Строк нових видач, 1..365 днів |
| `Integration:ApiKey` | `CROSSAPP_Integration__ApiKey` | — (немає у файлах) | Секрет: user-secrets або змінна; друкується лише «присутній, довжина N» |
| — | `CROSSAPP_ENVIRONMENT` | `Development` | Який `appsettings.{Environment}.json` додати |

Порядок джерел (кожне наступне перекриває попередні): `appsettings.json` → `appsettings.{Environment}.json` → user-secrets (лише Development) → змінні `CROSSAPP_*` → аргументи `--Ключ=значення` → `CROSSAPP_DATA_PATH` поверх `Storage:DataPath`.

### 4. Приклади змінних середовища

PowerShell (діє до закриття вікна):

```powershell
$env:CROSSAPP_DATA_PATH = "$env:TEMP\crossapp-data"
$env:CROSSAPP_Library__LoanDays = "30"
$env:CROSSAPP_ENVIRONMENT = "Production"
$env:CROSSAPP_Integration__ApiKey = "from-env"
dotnet run --project src/Cli -- export
Remove-Item Env:CROSSAPP_DATA_PATH, Env:CROSSAPP_Library__LoanDays, Env:CROSSAPP_ENVIRONMENT, Env:CROSSAPP_Integration__ApiKey
```

bash (змінна лише для однієї команди):

```bash
CROSSAPP_DATA_PATH=/tmp/data dotnet run --project src/Cli -- export
CROSSAPP_ENVIRONMENT=Production CROSSAPP_Integration__ApiKey=from-env dotnet run --project src/Cli -- config
CROSSAPP_Storage__FileName=backup.json dotnet run --project src/Cli -- export
```

### 5. Секрет `Integration:ApiKey`

```bash
dotnet user-secrets init --project src/Cli                               # один раз: <UserSecretsId> у Cli.csproj (комітиться)
dotnet user-secrets set "Integration:ApiKey" "local-dev-key" --project src/Cli
dotnet user-secrets list --project src/Cli                               # Integration:ApiKey = local-dev-key
```

Значення лежить поза репозиторієм: `%APPDATA%\Microsoft\UserSecrets\f2f9bd1f-6226-437a-94a8-f58165074554\secrets.json` (Windows) або `~/.microsoft/usersecrets/f2f9bd1f-6226-437a-94a8-f58165074554/secrets.json` (Linux/macOS). `git status` після `set` чистий, `git grep local-dev-key` нічого не знаходить. Файл не шифрується — це зручність розробника, а не сховище для продакшену; там секрет приходить змінною `CROSSAPP_Integration__ApiKey` або зі спеціалізованого сховища (Key Vault тощо).

### 6. Фрагменти коду

**Шлях без літералів** (`src/Core/Configuration/AppOptions.cs`):

```csharp
public string ResolveFullPath(string baseDir)
{
    if (string.IsNullOrWhiteSpace(FileName) || Path.GetFileName(FileName) != FileName)
        throw new ArgumentException($"Storage:FileName має бути іменем файлу без каталогів, а не '{FileName}'.");

    var root = Path.IsPathRooted(DataPath) ? DataPath : Path.Combine(baseDir, DataPath);
    Directory.CreateDirectory(root);
    return Path.GetFullPath(Path.Combine(root, FileName));
}
```

`Path.Combine` бере роздільник поточної ОС (`\` у Windows, `/` у Linux) і не дублює його, якщо частина вже закінчується роздільником; конкатенація `baseDir + "\\" + "data"` на Linux дала б файл з іменем `net10.0\data` у поточному каталозі.

**Збирання конфігурації** (`src/Cli/AppSettings.cs`):

```csharp
var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory) // не cwd: файли лежать поруч зі збіркою
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: false);
if (envName == "Development")
    builder.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
IConfiguration config = builder
    .AddEnvironmentVariables(prefix: "CROSSAPP_")
    .AddCommandLine(ConfigArguments(args))   // лише --Ключ=значення
    .Build();
AppOptions options = config.Get<AppOptions>() ?? new AppOptions();
```

**Атомарний запис** (`src/Core/Storage/LibraryJson.cs`):

```csharp
string tmp = fullPath + ".tmp";
File.WriteAllText(tmp, JsonSerializer.Serialize(dtos, Options));
File.Move(tmp, fullPath, overwrite: true);
```

### 7. Вивід

Запуск з кореня solution і з `src/Cli` — той самий файл даних (база — `AppContext.BaseDirectory`, а не робочий каталог):

```
PS> dotnet run --project src/Cli -- export
[debug] Середовище: Development (CROSSAPP_ENVIRONMENT не задано)
[debug] Робочий каталог (Environment.CurrentDirectory): C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp
[debug] Каталог застосунку (AppContext.BaseDirectory): C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net10.0\
[debug] Джерела: appsettings.json → appsettings.Development.json → secrets.json → EnvironmentVariables → CommandLine
Файл даних : C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net10.0\data\library.json
ApiKey: присутній, довжина 13
Експортовано 92 записи (видань 19, примірників 52, видач 21).
[debug] Тимчасовий файл library.json.tmp після File.Move: немає

PS src\Cli> dotnet run -- export
[debug] Робочий каталог (Environment.CurrentDirectory): C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli
[debug] Каталог застосунку (AppContext.BaseDirectory): C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net10.0\
Файл даних : C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net10.0\data\library.json
```

До і після `CROSSAPP_DATA_PATH`:

```
PS> $env:CROSSAPP_DATA_PATH = "$env:TEMP\crossapp-data"; dotnet run --project src/Cli -- export
Файл даних : C:\Users\Admin\AppData\Local\Temp\crossapp-data\library.json
ApiKey: присутній, довжина 13
Експортовано 92 записи (видань 19, примірників 52, видач 21).

PS> Remove-Item Env:CROSSAPP_DATA_PATH; dotnet run --project src/Cli -- import
Файл даних : C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net10.0\data\library.json
ApiKey: присутній, довжина 13
Імпортовано 92 записи.
 видань 19, примірників 52, видач 21; прострочено на сьогодні: 14
 перше видання: B-001 [978-0-201-61622-4] «Прагматичний програміст», 1999 — Ендрю Гант
```

Production і секрет зі змінної середовища:

```
PS> $env:CROSSAPP_ENVIRONMENT = "Production"; dotnet run --project src/Cli -- config
Файл даних : C:\Users\Admin\AppData\Local\CrossApp\library.json
ApiKey: не заданий
Середовище      : Production (EnvironmentName з файлу: Production)
Рівень журналу  : Warning
Storage:Root    : LocalAppData → C:\Users\Admin\AppData\Local
Storage:DataPath: CrossApp
Storage:FileName: library.json
Library:LoanDays: 14

PS> $env:CROSSAPP_Integration__ApiKey = "from-env"; dotnet run --project src/Cli -- export
Файл даних : C:\Users\Admin\AppData\Local\CrossApp\library.json
ApiKey: присутній, довжина 8
Експортовано 92 записи (видань 19, примірників 52, видач 21).
```

Фрагмент `data/library.json` (кирилиця як є, camelCase, `kind` — тип запису):

```json
[
  { "kind": "book", "id": "B-001", "isbn": "978-0-201-61622-4", "title": "Прагматичний програміст", "year": 1999, "author": "Ендрю Гант" },
  { "kind": "copy", "id": "C-001", "isbn": "978-0-201-61622-4", "isIssued": true },
  { "kind": "loan", "id": "L-001", "copyId": "C-001", "readerId": "R-001", "issuedOn": "2026-09-01", "returnedOn": null, "status": "Open", "loanDays": 14 }
]
```

Помилки — повідомлення без stack trace:

```
PS> dotnet run --project src/Cli -- import samples/catalog_bad.json
Файл пошкоджено: Файл …\samples\catalog_bad.json не є файлом колекції: кожен запис має починатися з "kind" (book | copy | loan).

PS> dotnet run --project src/Cli -- import broken.json          # зайва кома
Файл пошкоджено: Файл …\broken.json не є коректним JSON колекції (рядок 1, позиція 92): ',' is an invalid start of a property name. …

PS> $env:CROSSAPP_Logging__MinimumLevel = "Loud"; dotnet run --project src/Cli -- config
Помилка конфігурації: Failed to convert configuration value 'Loud' at 'Logging:MinimumLevel' to type 'Core.Configuration.LogLevel'.
 причина: Loud is not a valid value for LogLevel.
```

### 8. Самоперевірка

| Питання | Результат |
| :------ | :-------- |
| Пошук `"C:\"`, `"/home/"`, `"\\"` у `src` | порожньо (`git grep -F "C:\" -- src`) |
| Запуск з двох робочих каталогів | той самий `…\bin\Debug\net10.0\data\library.json` |
| Видалили `data/` | `ResolveFullPath` створює каталог заново, експорт без винятку |
| Зіпсований JSON | «Файл пошкоджено: … (рядок 1, позиція 92) …», код виходу 1 |
| `git status` після `user-secrets set`; кирилиця | чистий; у файлі «Прагматичний програміст», жодного `\u04…` |

### 9. Definition of Done (Лабораторна робота 9)

1. Експорт і імпорт колекції в JSON — команди `export` / `import`, `LibraryJson` (тести `LibraryJson_RoundTrip_…`).
2. Немає абсолютних шляхів і паролів у коді — пошук порожній; секрет лише в user-secrets.
3. `appsettings.json` у репозиторії, без секретів — `src/Cli/appsettings.json`.
4. `CROSSAPP_DATA_PATH` перекриває шлях із файлу — розділ 7.
5. Секрет читається з user-secrets або env; запуск з довільного cwd працює — розділи 5 і 7.
6. `dotnet build` і `dotnet test` зелені (42/42); коміт `lab09`.

### 10. Відповіді на питання на захисті

1. **`Environment.CurrentDirectory` vs `AppContext.BaseDirectory`** — перший залежить від того, звідки запустили процес (`dotnet run` з кореня чи з `src/Cli`, ярлик, служба), другий — каталог зі збіркою, сталий для цієї збірки.
2. **Чому `Path.Combine`** — бере роздільник ОС, не подвоює його, а абсолютну другу частину повертає як є; `"\\"` у Linux — звичайний символ імені файлу.
3. **`SpecialFolder.LocalApplicationData`** — для даних користувача, що мають пережити оновлення/перевстановлення програми (каталог збірки може бути лише для читання, як `Program Files`); тут — профіль Production.
4. **Порядок джерел** — `appsettings.json` → `appsettings.{Env}.json` → user-secrets → змінні середовища → командний рядок; пізніше додане перекриває.
5. **`Storage:DataPath` у змінній** — `Storage__DataPath`, з префіксом — `CROSSAPP_Storage__DataPath`.
6. **Де user-secrets** — `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` / `~/.microsoft/usersecrets/<id>/secrets.json`; відкритий текст на диску розробника, тому не для продакшену.
7. **`PropertyNamingPolicy = CamelCase` при десеріалізації** — очікує в JSON `isbn`, а не `Isbn`; щоб старі файли теж читались, додано `PropertyNameCaseInsensitive = true`.
8. **Кирилиця як `\u0426`** — типовий енкодер екранує все поза Basic Latin заради безпечного вбудовування в HTML; дані не губляться, а для читабельного файлу — `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`.
9. **`.tmp` + `File.Move`** — запис іде в тимчасовий файл, а перейменування на тому самому томі атомарне: у разі збою посеред запису лишається попередній цілий файл.

### 11. Що змінилося в коді попередніх лабораторних

- `data/` з вхідними файлами перейменовано на `samples/`; `data/` тепер лише runtime-каталог і в `.gitignore`. Зразки копіюються в `bin/…/samples`, тому `--domain`, `--import`, `--import-async` без шляху працюють з будь-якого каталогу.
- `Loan.LoanPeriodDays` (константа) → `Loan.DefaultLoanDays` + властивість `LoanDays` у кожній видачі; `LendingService(store, sink, loanDays)` бере строк з `Library:LoanDays`. Старі файли без `loanDays` читаються зі строком 14.
- `FileBookStore` пише camelCase через `LibraryJson` атомарно; пошкоджений файл дає `StorageException` з позицією помилки.
