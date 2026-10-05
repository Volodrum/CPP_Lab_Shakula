# CrossApp

Наскрізний навчальний проєкт з дисципліни **«Крос-платформне програмування»**.

- **Студент**: Шакула Володимир, група ФЕІ-32с
- **Предметна область**: Бібліотека (варіант Б)
- **Репозиторій**: <https://github.com/Volodrum/CPP_Lab_Shakula>
- **Середовище вимірювань**: Windows 11 Home x64 (10.0.26200), .NET SDK 10.0.400, runtime .NET 10.0.11 та .NET 8.0.21

## Зміст

- [Загальні відомості](#загальні-відомості)
- [Лабораторна робота 1. Solution CrossApp, Cli, вибір домену](#лабораторна-робота-1-solution-crossapp-cli-вибір-домену)
- [Лабораторна робота 2. Бібліотека Core + Cli, multi-targeting, публікація](#лабораторна-робота-2-бібліотека-core--cli-multi-targeting-публікація)
- [Лабораторна робота 3. Базові типи домену: records, pattern matching, імпорт CSV/JSON](#лабораторна-робота-3-базові-типи-домену-records-pattern-matching-імпорт-csvjson)
- [Лабораторна робота 4. Доменна модель: сутності, інваріанти, інкапсуляція](#лабораторна-робота-4-доменна-модель-сутності-інваріанти-інкапсуляція)
- [Швидка перевірка всіх лабораторних](#швидка-перевірка-всіх-лабораторних)

---

## Загальні відомості

### Предметна область: Бібліотека (Library Management)

- **Призначення**: облік видач примірників видань читачам, фіксація повернень та контроль термінів користування.
- **Основні сутності**:
  - `Book` — бібліографічний опис видання (ISBN, назва, автор, рік видання).
  - `BookCopy` — фізичний примірник книги (інвентарний номер, статус наявності).
  - `Reader` — читач бібліотеки (номер читацького квитка, ПІБ, контактні дані).
  - `Loan` — операція видачі (читач, примірник, дата видачі, дата повернення).

### Поточна структура solution (після лабораторної 4)

```text
CrossApp/
├── CrossApp.sln
├── README.md
├── .gitignore
├── data/
│   ├── sample.csv              # лаб. 3: 10 коректних + 3 пошкоджених рядки
│   ├── sample_valid.csv        # лаб. 3: лише коректні рядки
│   ├── sample.json             # лаб. 3: JSON-імпорт (дод. завдання 1)
│   ├── mixed.csv               # лаб. 3: книги та читачі в одному файлі (дод. завдання 2)
│   └── books_domain.csv        # лаб. 4: рядки, що проходять парсер, але не інваріанти
└── src/
    ├── Core/                   # Class Library, net10.0; net8.0
    │   ├── EnvironmentInfo.cs  # лаб. 2: збір відомостей про середовище
    │   ├── EnvironmentReport.cs
    │   ├── Dto/                # лаб. 3–4: records (формат даних)
    │   ├── Import/             # лаб. 3–4: імпортери CSV/JSON, EntityImport
    │   ├── Domain/             # лаб. 4: сутності Book, BookCopy, Loan
    │   ├── Services/           # лаб. 4: LendingService
    │   └── Storage/            # тиждень 5: сховища (поки порожньо)
    └── Cli/                    # Console App, net10.0, посилається на Core
        ├── Program.cs          # точка входу: --info, імпорт файлу, --domain
        └── DomainDemo.cs       # лаб. 4: демонстрація доменної моделі
```

### Історія комітів

| Коміт | Дата | Лабораторна | Зміст |
| :---- | :--- | :---------: | :---- |
| `7ebba94` | 2026-09-07 | 1 | Solution `CrossApp`, проєкт `Cli`, вивід відомостей про середовище, ключ `--json` |
| `3a7bd6c` | 2026-09-07 | 1 | Перший README (шаблонний домен «Склад») |
| `a58177b` | 2026-09-07 | 1 | Перехід на варіант Б — домен «Бібліотека» |
| `4c38a6e` | 2026-09-07 | 1 | `.gitignore` (bin/, obj/), кирилиця в JSON без екранування |
| `1447cca` | 2026-09-08 | 2 | Бібліотека `Core`, multi-targeting `net10.0; net8.0`, публікація FDD/SCD/single-file/trimmed |
| `9424007` | 2026-09-21 | 3 | Records у `Core/Dto`, pattern matching, імпорт CSV/JSON, `ImportResult<T>` |
| `8af2483` | 2026-09-21 | 3 | Роздільник CSV змінено на `,` |
| `bdd8956` | 2026-09-28 | 4 | Доменна модель: `Book`, `BookCopy`, `Loan`, інваріанти, `LendingService` |
| `88bdef5` | 2026-09-28 | 3–4 | Повернуто роздільник CSV `;` |

### Як відтворити стан попередньої лабораторної

Кожна лабораторна розширює попередню, тому частина старих команд працює лише на відповідному коміті (наприклад, `--json` існував у лабораторних 1–2, а з лабораторної 3 `Program.cs` займається імпортом файлів). Переключитися на коміт лабораторної (без зміни гілки):

```bash
git switch --detach 1447cca
```

Повернутися до актуального стану:

```bash
git switch main
```

> Усі виводи консолі в цьому документі отримано реальним запуском. Абсолютні шляхи наведено для каталогу проєкту `C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp`.

---

## Лабораторна робота 1. Solution CrossApp, Cli, вибір домену

**Коміти**: `7ebba94`, `3a7bd6c`, `a58177b`, `4c38a6e` (фінальний стан — `4c38a6e`)

### 1. Що зроблено

1. Створено solution `CrossApp.sln` із solution-каталогом `src` та консольним проєктом `src/Cli` (`net10.0`, `ImplicitUsings`, `Nullable` увімкнено).
2. `Program.cs` збирає та виводить відомості про середовище виконання:
   - `RuntimeInformation.OSDescription` та `Environment.OSVersion` — опис ОС двома способами;
   - `RuntimeInformation.ProcessArchitecture` — архітектура процесу;
   - `Environment.Version` та `RuntimeInformation.FrameworkDescription` — версія CLR та назва runtime;
   - `AppContext.BaseDirectory` та `Environment.CurrentDirectory` — каталог збірки й робочий каталог.
3. `Console.OutputEncoding = Encoding.UTF8` — коректне відображення кирилиці в консолі Windows.
4. Обрано предметну область: спочатку в README був шаблонний «Склад» (`3a7bd6c`), далі — варіант Б, **«Бібліотека»** (`a58177b`).
5. **Додаткове завдання — вивід у JSON** за ключем `--json`: анонімний об'єкт серіалізується `System.Text.Json` з `WriteIndented = true`. У коміті `4c38a6e` додано `Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)`, бо стандартний енкодер екранує кирилицю (`"\u0411\u0456\u0431..."`).
6. До `.gitignore` додано `bin/` та `obj/` — результати збірки не потрапляють у репозиторій.

### 2. Ключовий фрагмент коду (`src/Cli/Program.cs`)

```csharp
var info = new
{
    Title = "CrossApp – практикум з крос-платформного програмування",
    Student = student,
    OsDescription = RuntimeInformation.OSDescription,
    OsVersion = Environment.OSVersion.ToString(),
    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
    DotNetVersion = Environment.Version.ToString(),
    FrameworkDescription = RuntimeInformation.FrameworkDescription,
    AppBaseDirectory = AppContext.BaseDirectory,
    CurrentDirectory = Environment.CurrentDirectory,
    Domain = domain
};

if (args.Contains("--json"))
{
    var jsonOptions = new JsonSerializerOptions {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };
    Console.WriteLine(JsonSerializer.Serialize(info, jsonOptions));
}
```

### 3. Запуск (на коміті `4c38a6e`)

```bash
git switch --detach 4c38a6e
```
```bash
dotnet build
```
```bash
dotnet run --project src/Cli
```
```bash
dotnet run --project src/Cli -- --json
```

### 4. Вивід консолі

Звичайний запуск:

```text
CrossApp – практикум з крос-платформного програмування
Студент: Шакула Володимир, група ФЕІ-32с
------------------------------------------------------------
ОС (OSDescription)     : Microsoft Windows 10.0.26200
ОС (Environment)       : Microsoft Windows NT 10.0.26200.0
Архітектура процесу    : X64
Версія .NET (CLR)      : 10.0.11
Runtime                : .NET 10.0.11
Каталог застосунку     : C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net10.0\
Поточний каталог       : C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp
------------------------------------------------------------
Предметна область      : Бібліотека (видання, примірники, читачі, видачі та повернення)
```

Запуск з `--json`:

```json
{
  "Title": "CrossApp – практикум з крос-платформного програмування",
  "Student": "Шакула Володимир, група ФЕІ-32с",
  "OsDescription": "Microsoft Windows 10.0.26200",
  "OsVersion": "Microsoft Windows NT 10.0.26200.0",
  "ProcessArchitecture": "X64",
  "DotNetVersion": "10.0.11",
  "FrameworkDescription": ".NET 10.0.11",
  "AppBaseDirectory": "C:\\Users\\Admin\\Downloads\\CPP\\Lab_1\\CrossApp\\src\\Cli\\bin\\Debug\\net10.0\\",
  "CurrentDirectory": "C:\\Users\\Admin\\Downloads\\CPP\\Lab_1\\CrossApp",
  "Domain": "Бібліотека (видання, примірники, читачі, видачі та повернення)"
}
```

Каталог застосунку (`AppContext.BaseDirectory`) — це `bin/Debug/net10.0/`, де лежить зібрана збірка, а поточний каталог (`Environment.CurrentDirectory`) — той, з якого запущено `dotnet run`. Саме тому шляхи до файлів даних у наступних лабораторних задаються відносно поточного каталогу (`data/sample.csv`).

---

## Лабораторна робота 2. Бібліотека Core + Cli, multi-targeting, публікація

**Коміт**: `1447cca`

### 1. Архітектура рішення

Проєкт побудовано за принципом відокремлення базової логіки від точки входу:

```text
CrossApp/
├── CrossApp.sln
├── README.md
├── .gitignore
└── src/
    ├── Core/
    │   ├── Core.csproj               # Class Library, Multi-targeting (net10.0; net8.0)
    │   ├── EnvironmentReport.cs      # Модель даних (record) зі звітом про середовище
    │   ├── EnvironmentInfo.cs        # Збір відомостей про платформу, ОС, архітектуру та RID
    │   ├── Dto/                      # Майбутні record-типи даних (тиждень 3)
    │   ├── Domain/                   # Сутності та бізнес-правила (тиждень 4)
    │   └── Storage/                  # Реалізації сховищ даних (тиждень 5)
    └── Cli/
        ├── Cli.csproj                # Console App (net10.0), посилається на Core через ProjectReference
        └── Program.cs                # Точка входу: візуалізація (текст / JSON)
```

#### Принципи проєктування

1. **Жодної бізнес-логіки в `Program.cs`**: консольний застосунок відповідає лише за запуск, форматування виводу (текстового або JSON) та передачу результатів користувачу. Усі системні виклики та збір інформації реалізовано в `Core`.
2. **Одностороння залежність**: `Cli` посилається на `Core` (`Cli -> Core`). `Core` не посилається на `Cli`, що запобігає циклічним залежностям та дозволить підключити цей же `Core` до майбутнього Web API або Blazor.
3. **Multi-targeting**: бібліотека `Core` збирається для `net10.0` та `net8.0` (`<TargetFrameworks>net10.0;net8.0</TargetFrameworks>`).

### 2. Ключові фрагменти коду

`src/Core/EnvironmentReport.cs`:

```csharp
public sealed record EnvironmentReport(
    string OsDescription,
    string FrameworkDescription,
    string ProcessArchitecture,
    string DetectedRid,
    string ReportedRid,
    string BaseDirectory,
    string BuildTargetNote);
```

`src/Core/EnvironmentInfo.cs` — умовна компіляція показує, під яку цільову платформу зібрано `Core`, а RID визначається вручну і порівнюється з `RuntimeInformation.RuntimeIdentifier`:

```csharp
public static EnvironmentReport Collect()
{
#if NET10_0_OR_GREATER
    const string targetNote = ".NET 10.0 (Сучасний реліз)";
#elif NET8_0_OR_GREATER
    const string targetNote = ".NET 8.0 (LTS)";
#else
    const string targetNote = "Старіша версія .NET";
#endif

    return new EnvironmentReport(
        RuntimeInformation.OSDescription,
        RuntimeInformation.FrameworkDescription,
        RuntimeInformation.ProcessArchitecture.ToString(),
        DetectRid(),
        RuntimeInformation.RuntimeIdentifier,
        AppContext.BaseDirectory,
        targetNote);
}

private static string DetectRid()
{
    string os =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "unknown";

    string arch = RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.X86 => "x86",
        Architecture.Arm64 => "arm64",
        Architecture.Arm => "arm",
        _ => "unknown"
    };

    return $"{os}-{arch}";
}
```

### 3. Збірка та запуск

Команди з ключем `--json` та запуск без аргументів виконуються на коміті `1447cca` (з лабораторної 3 запуск без аргументів імпортує `data/sample.csv`, а відомості про середовище виводить ключ `--info`).

```bash
git switch --detach 1447cca
```
```bash
dotnet build
```
```bash
dotnet sln list
```
```bash
dotnet run --project src/Cli
```
```bash
dotnet run --project src/Cli -- --json
```

Вивід `dotnet sln list`:

```text
Project(s)
----------
src\Cli\Cli.csproj
src\Core\Core.csproj
```

Вивід `dotnet run --project src/Cli`:

```text
CrossApp – Інформація про середовище
Студент: Шакула Володимир, група ФЕІ-32с
--------------------------------------------------------
ОС (OSDescription)  : Microsoft Windows 10.0.26200
Runtime             : .NET 10.0.11
Архітектура процесу : X64
RID (визначено)     : win-x64
RID (від .NET)      : win-x64
Каталог застосунку  : C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net10.0\
Цільова збірка Core : .NET 10.0 (Сучасний реліз)
--------------------------------------------------------
Предметна область   : Бібліотека (видання, примірники, читачі, видачі та повернення)
```

Вивід `dotnet run --project src/Cli -- --json`:

```json
{
  "Title": "CrossApp – Інформація про середовище",
  "Student": "Шакула Володимир, група ФЕІ-32с",
  "Domain": "Бібліотека (видання, примірники, читачі, видачі та повернення)",
  "Environment": {
    "OsDescription": "Microsoft Windows 10.0.26200",
    "FrameworkDescription": ".NET 10.0.11",
    "ProcessArchitecture": "X64",
    "DetectedRid": "win-x64",
    "ReportedRid": "win-x64",
    "BaseDirectory": "C:\\Users\\Admin\\Downloads\\CPP\\Lab_1\\CrossApp\\src\\Cli\\bin\\Debug\\net10.0\\",
    "BuildTargetNote": ".NET 10.0 (Сучасний реліз)"
  }
}
```

### 4. Multi-targeting: net10.0 vs net8.0

Збірка `Core` для обох цільових платформ:

```bash
dotnet build src/Core/Core.csproj
```

У `src/Core/bin/Debug/` з'являються два підкаталоги: `net10.0/` та `net8.0/`.

`Cli.csproj` має одну цільову платформу (`net10.0`), тому звичайний `dotnet run -f net8.0` завершується помилкою `NETSDK1005 ... doesn't have a target for 'net8.0'`. Щоб запустити застосунок під .NET 8, цільову платформу перевизначають властивістю MSBuild на один запуск (файли проєкту не змінюються):

```bash
dotnet run --project src/Cli -f net8.0 -p:TargetFrameworks=net8.0
```

```text
CrossApp – Інформація про середовище
Студент: Шакула Володимир, група ФЕІ-32с
--------------------------------------------------------
ОС (OSDescription)  : Microsoft Windows 10.0.26200
Runtime             : .NET 8.0.21
Архітектура процесу : X64
RID (визначено)     : win-x64
RID (від .NET)      : win-x64
Каталог застосунку  : C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\src\Cli\bin\Debug\net8.0\
Цільова збірка Core : .NET 8.0 (LTS)
--------------------------------------------------------
Предметна область   : Бібліотека (видання, примірники, читачі, видачі та повернення)
```

Порівняно з запуском під .NET 10 змінилися три рядки: `Runtime` (`.NET 8.0.21`), каталог застосунку (`net8.0\`) і `Цільова збірка Core` — його значення обирає директива `#if NET10_0_OR_GREATER` / `#elif NET8_0_OR_GREATER` під час компіляції.

Наступний звичайний `dotnet run` / `dotnet build` автоматично повертає restore для `net10.0`.

### 5. Порівняння режимів публікації

Команди для публікації у каталог `pub/`:

```bash
dotnet publish src/Cli -c Release -r win-x64 --self-contained false -o pub/fdd
```
```bash
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -o pub/scd
```
```bash
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o pub/singlefile
```
```bash
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true -o pub/trimmed
```

Вимірювання розміру виконано за допомогою PowerShell:
`(Get-ChildItem -Recurse <каталог> | Measure-Object Length -Sum).Sum / 1MB`

| RID         | Режим публікації              |    Розмір    | Кількість файлів | Потрібен .NET Runtime | Опис та примітки                                                                                                                                   |
| :---------- | :---------------------------- | :----------: | :--------------: | :-------------------: | :------------------------------------------------------------------------------------------------------------------------------------------------- |
| **win-x64** | **Framework-Dependent (FDD)** | **0.20 МБ**  |        7         |   **Так** (.NET 10)   | Містить лише бінарні файли застосунку (`Cli.dll`, `Core.dll`, легкий apphost `Cli.exe` ~158 КБ). Використовує встановлений у системі .NET Runtime. |
| **win-x64** | **Self-Contained (SCD)**      | **76.68 МБ** |       194        |        **Ні**         | Включає повне середовище виконання .NET (CoreCLR), JIT-компілятор, бібліотеки BCL та залежності. Працює на машинах без встановленого .NET.          |
| **win-x64** | **Single-File (SCD)**         | **70.16 МБ** |        3         |        **Ні**         | Усі runtime-бібліотеки запаковано в один автономний виконуваний файл `Cli.exe` (+ `.pdb` налагоджувальні символи).                                 |
| **win-x64** | **Trimmed Single-File (SCD)** | **12.58 МБ** |        3         |        **Ні**         | IL Linker/Trimmer видаляє невикористаний код і метадані. Розмір зменшено на **~83%** порівняно зі звичайним SCD.                                   |

Запуск опублікованого застосунку напряму:

```bash
./pub/fdd/Cli.exe
```

### 6. Різниця між Self-Contained та Framework-Dependent

- **Framework-Dependent (FDD)** публікує виключно скомпільований код застосунку та його залежності, покладаючись на глобально інстальований у системі .NET Runtime; це забезпечує мінімальний розмір розгортання (~0.2 МБ) та швидке оновлення середовища безпековими патчами ОС.
- **Self-Contained (SCD)** містить як код застосунку, так і копію середовища виконання .NET (CoreCLR + BCL) під конкретний ідентифікатор платформи (RID); застосунок може запускатися автономно на будь-якому комп'ютері без попереднього налаштування .NET, однак платить за це істотним розміром (~77 МБ) та необхідністю самостійно оновлювати runtime.

### 7. Аналіз Trimming та попередження IL2026

Під час публікації з параметром `-p:PublishTrimmed=true` компілятор видає попередження:

```text
Program.cs(30,23): warning IL2026: Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
```

Попередження справджується: тримований застосунок без аргументів працює, а з ключем `--json` падає під час виконання:

```bash
./pub/trimmed/Cli.exe --json
```

```text
Unhandled exception. System.InvalidOperationException: Reflection-based serialization has been disabled for this application. Either use the source generator APIs or explicitly configure the 'JsonSerializerOptions.TypeInfoResolver' property.
   at System.Text.Json.ThrowHelper.ThrowInvalidOperationException_JsonSerializerIsReflectionDisabled()
   at System.Text.Json.JsonSerializerOptions.ConfigureForJsonSerializer()
   ...
   at System.Text.Json.JsonSerializer.Serialize[TValue](TValue value, JsonSerializerOptions options)
   at Program.<Main>$(String[] args) in ...\src\Cli\Program.cs:line 30
```

**Чому Trimming небезпечний для коду з рефлексією**:
Trimmer аналізує статичні виклики та видаляє невикористані метадані типів, властивостей і методів. Бібліотека `System.Text.Json` за замовчуванням використовує рефлексію в runtime для обходу властивостей об'єктів. Якщо тип чи його властивості не були явно зафіксовані в коді, тример видалить їх, а для тримованих застосунків рефлексивну серіалізацію вимкнено взагалі — звідси `System.InvalidOperationException` під час виконання. Для безпечної роботи у тримованих середовищах слід використовувати C# Source Generators (`[JsonSerializable]` + `JsonSerializerContext`).

### 8. Definition of Done (Лабораторна робота 2)

1. **`dotnet sln list` показує два проєкти?**
   Так: `src/Cli/Cli.csproj` та `src/Core/Core.csproj`.
2. **У `Program.cs` є хоч один виклик `RuntimeInformation`?**
   Ні. Усі виклики `RuntimeInformation`, визначення RID та аналіз середовища інкапсульовано виключно в `Core.EnvironmentInfo`.
3. **Чи збирається Core окремо?**
   Так: `dotnet build src/Core/Core.csproj` успішно збирає бібліотеку одночасно для `net10.0` та `net8.0`.
4. **Чи впаде збірка, якщо додати посилання `Core -> Cli`?**
   Так. Виникне помилка циклічної залежності (`Circular dependency detected between projects`).
5. **Чи запускається застосунок з каталогу `pub/` напряму?**
   Так. Перевірено прямим запуском `.\pub\fdd\Cli.exe`, `.\pub\scd\Cli.exe`, `.\pub\singlefile\Cli.exe`.
6. **Каталоги bin/obj/pub не в репозиторії?**
   Так. Каталоги `bin/`, `obj/`, `pub/`, `publish/` внесені до `.gitignore` і виключені з відстеження git.

---

## Лабораторна робота 3. Базові типи домену: records, pattern matching, імпорт CSV/JSON

**Коміти**: `9424007`, `8af2483`, `88bdef5`

**Тема**: Базові типи домену: records, pattern matching, імпорт CSV/JSON  
**Мета**: Описати перші дані проєкту сучасним C# (records, nullable, pattern matching) і завантажити їх з файлу так, щоб пошкоджені рядки не перешкоджали імпорту цілісних рядків.

---

### 1. Опис Record-типів та обґрунтування Nullable

Усі типи предметної області оголошено у просторі імен `Core.Dto`:

```csharp
namespace Core.Dto;

public abstract record LibraryItemDto(string Id);

public sealed record BookDto(
    string Id,
    string Isbn,
    string Title,
    int Year,
    string? Author = null) : LibraryItemDto(Id);

public sealed record ReaderDto(
    string Id,
    string FullName,
    string TicketNumber,
    string? Phone = null) : LibraryItemDto(Id);

public sealed record ImportResult<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Errors)
{
    public int Total => Items.Count + Errors.Count;
    public double SuccessPercentage => Total == 0 ? 100.0 : (double)Items.Count / Total * 100.0;
    public double ErrorPercentage => Total == 0 ? 0.0 : (double)Errors.Count / Total * 100.0;
    public string Summary => $"Усього: {Total}, прийнято: {Items.Count}, пропущено: {Errors.Count}, помилок: {ErrorPercentage:F1}%";
}
```

#### Обґрунтування вибору Nullable для кожного поля:
- **`Id` (`string`)**: Non-nullable. Унікальний первинний ключ сутності (наприклад, `"B-001"`, `"R-001"`). Запис без ідентифікатора не має сенсу у сховищі.
- **`Isbn` (`string`)**: Non-nullable. Міжнародний стандартний номер книги є обов'язковим атрибутом друкованого видання в бібліотечному каталозі.
- **`Title` (`string`)**: Non-nullable. Назва видання — ключове інформаційне поле, книга не може існувати без назви.
- **`Year` (`int`)**: Non-nullable тип значення (`int`). Рік публікації книги є обов'язковим для обліку та перевіряється на валідний діапазон (`1450..поточний рік`).
- **`Author` (`string?`)**: **Nullable**. Деякі видання (збірники статей, енциклопедії, стародавні фоліанти, словники, анонімні праці) не мають одного визначеного автора або інформація про автора може бути відсутня. Значення за замовчуванням `= null`.
- **`FullName` (`string`)**: Non-nullable. Прізвище, ім'я та по батькові читача обов'язкові для реєстрації.
- **`TicketNumber` (`string`)**: Non-nullable. Номер читацького квитка — обов'язковий ідентифікатор читача в системі.
- **`Phone` (`string?`)**: **Nullable**. Номер телефону читача є необов'язковим контактним атрибутом. Значення за замовчуванням `= null`.

#### Чому тут `record`, а не `class` (одним реченням):
> Тип `record` обрано тому, що вхідні дані рядка файлу є незмінними DTO, які потребують рівності за значеннями для швидкого порівняння, безпечного копіювання через `with` та автоматичної деконструкції без необхідності писати шаблонний код.

---

### 2. Логіка розбору та використані патерни (Pattern Matching)

Розбір рядків реалізовано у методі `ParseLine` класу `Core.Import.BookCsvImporter`:

```csharp
private static ParseOutcome ParseLine(string line)
{
    string[] parts = line.Split(Separator, StringSplitOptions.TrimEntries);

    return parts switch
    {
        { Length: < 4 } => new ParseFailed($"очікую щонайменше 4 колонки, отримав {parts.Length}"),
        ["", ..] => new ParseFailed("ID порожній"),
        [_, "", _, _, ..] or [_, _, "", _, ..] => new ParseFailed("ISBN або назва порожні"),
        [_, _, _, var yearStr, ..] when !int.TryParse(yearStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) || y < 1450 || y > DateTime.Now.Year
            => new ParseFailed($"рік '{yearStr}' не є цілим числом або виходить за допустимі межі (1450..{DateTime.Now.Year})"),
        [var id, var isbn, var title, var yearStr]
            => new ParseOk(new BookDto(id, isbn, title, int.Parse(yearStr, CultureInfo.InvariantCulture))),
        [var id, var isbn, var title, var yearStr, var author]
            => new ParseOk(new BookDto(id, isbn, title, int.Parse(yearStr, CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(author) ? null : author)),
        _ => new ParseFailed($"занадто багато колонок: {parts.Length}")
    };
}
```

#### Перелік та призначення використаних патернів:
1. **Патерн властивостей + реляційний патерн (`{ Length: < 4 }`)**: перевіряє довжину розбитого масиву; спрацьовує, якщо в рядку менше ніж 4 поля.
2. **Патерн списку зі зрізом (`["", ..]`)**: перевіряє, що перший елемент (ID) не є порожнім рядком.
3. **Константний патерн списку з логічним оператором `or` (`[_, "", _, _, ..] or [_, _, "", _, ..]`)**: виявляє порожнє значення у другій (ISBN) або третій (назва) колонці.
4. **Патерн оголошення змінної списку + охоронна умова `when` (`[_, _, _, var yearStr, ..] when ...`)**: витягує рядок року та перевіряє парсинг цілого числа за допомогою `int.TryParse` з `CultureInfo.InvariantCulture` та перевіркою допустимого історичного діапазону.
5. **Патерни фіксованого списку (`[var id, var isbn, var title, var yearStr]` та `[..., var author]`)**: деконструюють та створюють екземпляр `BookDto` для рядків із 4 або 5 колонками.
6. **Патерн відкидання (`_`)**: універсальна гілка за замовчуванням, яка сигналізує про надмірну кількість колонок.

---

### 3. Формат вхідних даних

- **Роздільник**: Крапка з комою (`;`), що унеможливлює конфлікти з комами у назвах видань або списках авторів.
  Роздільник задано константою `private const char Separator = ';';` у `BookCsvImporter` та `LibraryCsvImporter`. У коміті `8af2483` його тимчасово змінювали на кому, у `88bdef5` повернуто `;` (разом з усіма файлами `data/*.csv`).
- **Кодування**: **UTF-8** (`File.ReadAllLines(path, Encoding.UTF8)`).
- **Заголовок**: Рядок `id;isbn;title;year;author` (програма коректно розпізнає наявність заголовка на першому рядку і пропускає його, або імпортує дані з першого рядка, якщо заголовок відсутній).
- **Коментарі та порожні рядки**: Рядки, що починаються з `#`, та порожні рядки ігноруються.

---

### 4. Додаткові завдання (Optional Tasks)

#### 1. Другий імпортер – JSON (`Core.Import.BookJsonImporter`)
- Використовує `System.Text.Json` з налаштуванням `PropertyNameCaseInsensitive = true`.
- Використовує колекційний вираз та оператор `??`:
  ```csharp
  List<BookDto> list = JsonSerializer.Deserialize<List<BookDto>>(json, JsonOptions) ?? [];
  ```
- Валідує кожен об'єкт за допомогою pattern matching на властивостях (`case { Id: "" }:`, `case { Year: var y } when ...:`).
- `Program.cs` обирає імпортер за розширенням `.json` через вираз `switch`.

#### 2. Розпізнавання різнорідних рядків (`Core.Import.LibraryCsvImporter`)
- Розпізнає записи за префіксом типу:
  - `B;...` – книги (`BookDto`)
  - `R;...` – читачі (`ReaderDto`)
- **Один вираз `switch` повертає два різні типи результатів**, об'єднані базовим типом `LibraryItemDto`.
- Вхідний тестовий файл: `data/mixed.csv`.

#### 3. Статистика імпорту в один рядок
- Реалізовано через обчислювану властивість `Summary` в `ImportResult<T>`:
  `Статистика імпорту: Усього: 13, прийнято: 10, пропущено: 3, помилок: 23.1%`

---

### 5. Демонстрація роботи та вивід консолі

#### Сценарій 1: Запуск за замовчуванням (`data/sample.csv`, 10 коректних + 3 пошкоджених)
```bash
dotnet run --project src/Cli
```
```text
Завантажено записів: 10
 B-001  978-0-13-235088-4  Чистий код                     2008  Роберт Мартін
 B-002  978-0-201-61622-4  Прагматичний програміст        1999  Ендрю Гант
 B-003  978-0-13-449416-6  Чиста архітектура              2017  Роберт Мартін
 B-004  978-0-596-52068-7  Шаблони проектування           2004  Ерік Фрімен
 B-005  978-0-321-12521-7  Рефакторинг                    2018  Мартін Фаулер
Пропущено рядків: 3
 ! рядок 12: очікую щонайменше 4 колонки, отримав 3
 ! рядок 13: рік 'давній' не є цілим числом або виходить за допустимі межі (1450..2026)
 ! рядок 14: ISBN або назва порожні
Статистика імпорту: Усього: 13, прийнято: 10, пропущено: 3, помилок: 23.1%
```

#### Сценарій 2: Коректний файл (`data/sample_valid.csv`)
```bash
dotnet run --project src/Cli -- data/sample_valid.csv
```
```text
Завантажено записів: 10
 B-001  978-0-13-235088-4  Чистий код                     2008  Роберт Мартін
 B-002  978-0-201-61622-4  Прагматичний програміст        1999  Ендрю Гант
 B-003  978-0-13-449416-6  Чиста архітектура              2017  Роберт Мартін
 B-004  978-0-596-52068-7  Шаблони проектування           2004  Ерік Фрімен
 B-005  978-0-321-12521-7  Рефакторинг                    2018  Мартін Фаулер
Статистика імпорту: Усього: 10, прийнято: 10, пропущено: 0, помилок: 0.0%
```

#### Сценарій 3: Неіснуючий файл (перевірка коду завершення)
```bash
dotnet run --project src/Cli -- non_existent.csv
echo $LASTEXITCODE
```
```text
Файл не знайдено: C:\Users\Admin\Downloads\CPP\Lab_1\CrossApp\non_existent.csv
1
```

#### Сценарій 4: Імпорт JSON-файлу (Додаткове завдання 1)
```bash
dotnet run --project src/Cli -- data/sample.json
```
```text
Завантажено записів: 5
 B-101  978-0-13-235088-4  Clean Code                     2008  Robert C. Martin
 B-102  978-0-201-61622-4  The Pragmatic Programmer       1999  Andrew Hunt
 B-103  978-0-13-449416-6  Clean Architecture             2017  Robert C. Martin
 B-104  978-0-13-597444-5  CLR via C#                     2012  Jeffrey Richter
 B-105  978-1-61729-453-2  C# in Depth                    2019  Jon Skeet
Пропущено рядків: 1
 ! елемент 6 (B-106): рік '2099' поза допустимими межами (1450..2026)
Статистика імпорту: Усього: 6, прийнято: 5, пропущено: 1, помилок: 16.7%
```

#### Сценарій 5: Різнорідні рядки за префіксами B та R (Додаткове завдання 2)
```bash
dotnet run --project src/Cli -- data/mixed.csv
```
```text
Завантажено записів: 6
 [Книга] B-001  978-0-13-235088-4  Чистий код                 2008  Роберт Мартін
 [Книга] B-002  978-0-201-61622-4  Прагматичний програміст    1999  Ендрю Гант
 [Книга] B-003  978-0-13-449416-6  Чиста архітектура          2017  Роберт Мартін
 [Читач] R-001  Т-1001             Олександр Сидоренко        +380501234567
 [Читач] R-002  Т-1002             Марія Коваленко            +380679876543
Пропущено рядків: 3
 ! рядок 8: очікую щонайменше 5 колонок для книги, отримав 4
 ! рядок 9: ПІБ або номер читацького квитка порожні
 ! рядок 10: невідомий тип запису 'X' (очікується 'B' або 'R')
Статистика імпорту: Усього: 9, прийнято: 6, пропущено: 3, помилок: 33.3%
```

---

### 6. Відповіді на питання для захисту

1. **Чим `record` відрізняється від `class` і чому ви обрали `record` для рядка файлу?**  
   Для `record` компілятор автоматично генерує рівність за значеннями (`value equality`), обчислення хеш-коду на основі полів, форматований `ToString()` та метод деконструкції `Deconstruct`. У позиційному синтаксисі властивості є незмінними (`init`), що ідеально підходить для DTO рядка файлу: гарантується імутабельність та відсутність небажаних мутацій даних після імпорту.
2. **Що таке `init-only` властивість і як зробити «змінену копію» `record`?**  
   Модифікатор `init` дозволяє встановлювати значення властивості лише під час конструювання або ініціалізації об'єкта. Змінена копія створюється за допомогою синтаксису `with`: `BookDto updated = book with { Year = 2024 };`, при цьому вихідний об'єкт залишається незмінним.
3. **Як працює рівність двох records? Порівняйте з класом.**  
   У класах порівняння оператором `==` або методом `Equals` за замовчуванням перевіряє рівність посилань (`reference equality` — чи це один і той самий об'єкт у пам'яті). У `record` компілятор генерує реалізацію `IEquatable<T>`, яка по черзі порівнює значення всіх властивостей.
4. **Які патерни ви використали і що робить кожен?**  
   - `{ Length: < 4 }` — патерн властивостей з реляційним патерном для перевірки довжини масиву;
   - `[_, "", _, _, ..]` — константний патерн списку для виявлення порожніх колонок;
   - `[_, _, _, var yearStr, ..] when ...` — патерн списку з оголошенням змінної та охоронною умовою `when`;
   - `[var id, var isbn, var title, var yearStr]` — позиційний патерн списку для розпакування елементів у змінні;
   - `_` — патерн відкидання (`discard`) для обробки всіх інших непередбачених випадків.
5. **Що таке list pattern `[var a, var b]` і коли він не спрацює?**  
   Патерн `[var a, var b]` збігається **лише з масивом чи колекцією, яка містить рівно два елементи**. Він не спрацює, якщо довжина колекції менша або більша за 2. Для довільної кількості решти елементів використовують зріз `..` (`[var a, var b, ..]`).
6. **Навіщо охоронна умова `when`, якщо є патерн властивостей?**  
   Патерн властивостей перевіряє статичну форму та прості значення об'єкта (наприклад `{ Length: 5 }`). Охоронна умова `when` дозволяє виконувати довільний динамічний C#-код після збігу патерна (наприклад, виклик методів парсингу `int.TryParse`, порівняння з результатами функцій, перевірку складних бізнес-правил).
7. **Чому імпорт повертає `ImportResult`, а не кидає виняток на першій помилці?**  
   Файли можуть містити сотні чи тисячі рядків даних. Якщо через один некоректний рядок перервати всю операцію винятком, користувач втратить усі валідні дані. `ImportResult<T>` забезпечує стійкість (`fault tolerance`), завантажуючи всі коректні записи та повертаючи вичерпний список помилок із номерами рядків.
8. **Що означає `string?` у вашому типі і що зміниться, якщо забрати «?»?**  
   `string?` вказує компілятору, що властивість може набувати значення `null` (відсутній автор або номер телефону), і компілятор попередить про необхідність перевірки перед розіменуванням. Якщо прибрати `?`, компілятор вимагатиме обов'язкового ненульового рядка і сигналізуватиме попередженням CS8618 при спробі передати `null`.
9. **Навіщо `CultureInfo.InvariantCulture` при розборі чисел і дат?**  
   Різні мовні локалі використовують різні роздільники цілої та дробової частини (наприклад, крапка в `en-US` та кома в `uk-UA`). Якщо розбирати рядок "12.50" без явного вказання `CultureInfo.InvariantCulture`, на україномовній системі виникне помилка `FormatException` або число буде некоректно інтерпретоване як `1250`.
10. **Що станеться з вашим парсером, якщо файл збережено з роздільником-комою?**  
    Роздільник визначено константою `private const char Separator = ';';`. Якщо файл міститиме кому, виклик `line.Split(';')` поверне масив з одного елемента (весь рядок). Спрацює перша гілка патерна `{ Length: < 4 }`, і рядок буде відхилено з діагностикою: `рядок N: очікую щонайменше 4 колонки, отримав 1`. Для зміни роздільника достатньо змінити константу `Separator`.
11. **Чому розбір у `Core`, а не в `Program.cs`? Хто ще буде його викликати?**  
    У наступних лабораторних роботах логіка імпорту повторно викликатиметься сховищами даних (`FileCatalogStore` на 5 тижні), модулями звітності (LINQ на 7 тижні) та модульними юніт-тестами (`xUnit` на 8 тижні). Консольний застосунок `Cli` відповідає виключно за введення аргументів та вивід результату.

---

### 7. Definition of Done (Лабораторна робота 3)

- [x] 2-3 record-типи створено в `Core/Dto` (`BookDto`, `ReaderDto`, `LibraryItemDto`), nullable використано обґрунтовано (`Author?`, `Phone?`).
- [x] Розбір рядка виконано через вираз `switch` із щонайменше 6 різними видами патернів.
- [x] Імпорт CSV повертає успішні дані разом із помилками (`ImportResult<T>`).
- [x] У репозиторії створено `data/sample.csv` (10 валідних + 3 пошкоджених рядки), `data/sample_valid.csv`, `data/sample.json`, `data/mixed.csv`.
- [x] `Cli` виводить кількість, перші записи, перелік помилок з номерами рядків та статистику.
- [x] Реалізовано всі 3 додаткові завдання: JSON-імпортер, розпізнавання різнорідних рядків (книги/читачі), статистика імпорту.
- [x] Використано file-scoped namespace, прибрано зайві using.
- [x] Код успішно будується для `net10.0` та `net8.0` без попереджень і помилок.

---

## Лабораторна робота 4. Доменна модель: сутності, інваріанти, інкапсуляція

**Коміти**: `bdd8956`, `88bdef5`

**Тема**: Доменна модель: сутності, інваріанти, інкапсуляція  
**Мета**: Створити модель із чіткими правилами: об'єкт неможливо створити в некоректному стані або перевести в нього, а порушення правила спричиняє зрозумілий виняток.

---

### 1. Структура після лабораторної

```text
src/Core/
├── Dto/                        # ФОРМАТ даних (records тижня 3 лишились без змін)
│   ├── BookDto.cs, ReaderDto.cs, LibraryItemDto.cs, ImportResult.cs
│   ├── BookCopyDto.cs          # НОВЕ: формат збереження примірника
│   └── LoanDto.cs              # НОВЕ: формат збереження видачі
├── Domain/                     # НОВЕ: сутності з поведінкою
│   ├── Book.cs                 # видання (незмінне після створення)
│   ├── BookCopy.cs             # примірник: Issue() / Return()
│   ├── Loan.cs                 # видача: Open() / MarkLost() / Close()
│   ├── LoanStatus.cs           # enum Open / Lost / Closed
│   └── IsbnRules.cs            # спільна перевірка ISBN (internal)
├── Import/
│   ├── BookCsvImporter.cs, BookJsonImporter.cs, LibraryCsvImporter.cs
│   └── EntityImport.cs         # НОВЕ: ImportResult<Dto> → ImportResult<Entity> (дод. завдання 1)
└── Services/
    └── LendingService.cs       # НОВЕ: правило на кілька сутностей (дод. завдання 2)
src/Cli/
├── Program.cs                  # ключ --domain запускає демонстрацію
└── DomainDemo.cs               # сценарій «успіх» + сценарій «порушення інваріантів»
data/books_domain.csv           # рядки, що проходять парсер, але не проходять інваріанти
```

Запуск демонстрації:

```bash
dotnet run --project src/Cli -- --domain
```

### 2. Обрані сутності та зв'язки

```text
Book (Id, Isbn, Title, Year, Author?)
  │ 1
  │  ISBN
  │ *
BookCopy (Id, Isbn, IsIssued)  ◄──── Copy ────  Loan (Id, ReaderId, IssuedOn, ReturnedOn?, Status)
                                  1        *
```

- **`Book`** — запис каталогу. Після створення не змінюється: ISBN, назва і рік — факт про видання.
- **`BookCopy`** — фізичний примірник видання (зв'язок з `Book` через ISBN). Має стан «на полиці / виданий», змінюється лише методами `Issue()` і `Return()`.
- **`Loan`** — головна сутність процесу: видача конкретного примірника читачу. `Loan.Open(...)` сам викликає `copy.Issue()`, а `Close(...)` — `Copy.Return()`, тому стан видачі й примірника не розходяться.
- **`LendingService`** — координує кілька видач (ліміт на читача).

### 3. Перелік інваріантів

| # | Правило | Тип винятку | Де перевіряється |
|---|---------|-------------|------------------|
| 1 | Ідентифікатор (книги / примірника / видачі / читача) не порожній | `ArgumentException` | `Book.Create`, `BookCopy.Register`/`FromDto`, `Loan.Open`/`FromDto` |
| 2 | ISBN не порожній і містить 10 або 13 цифр (дефіси дозволені, ISBN-10 може закінчуватись на `X`) | `ArgumentException` | `IsbnRules.Normalize` ← `Book.Create`, `BookCopy.Register`/`FromDto` |
| 3 | Назва книги не порожня | `ArgumentException` | `Book.Create` |
| 4 | Рік видання в межах `1450..поточний рік` | `ArgumentOutOfRangeException` | `Book.Create` |
| 5 | Дата видачі задана (не `default`) | `ArgumentOutOfRangeException` | `Loan.Open`, `Loan.FromDto` |
| 6 | Не можна видати вже виданий примірник | `InvalidOperationException` | `BookCopy.Issue` (викликається з `Loan.Open`) |
| 7 | Не можна повернути примірник, який і так на полиці | `InvalidOperationException` | `BookCopy.Return` |
| 8 | Дата повернення не раніше дати видачі | `ArgumentOutOfRangeException` | `Loan.Close`, `Loan.FromDto` |
| 9 | Не можна змінити закриту видачу (повторне закриття / позначка «втрачено») | `InvalidOperationException` | `Loan.EnsureCanMoveTo` |
| 10 | Допустимі лише переходи `Open → Lost`, `Open → Closed`, `Lost → Closed` | `InvalidOperationException` | `Loan.EnsureCanMoveTo` (switch expression) |
| 11 | DTO видачі узгоджений: статус відомий, `Closed` ⇔ є дата повернення, активна видача ⇒ примірник виданий, `CopyId` збігається | `ArgumentException` | `Loan.FromDto` |
| 12 | У читача не більше 5 незакритих видач | `InvalidOperationException` | `LendingService.IssueCopy` |

**Інкапсуляція**: ключові поля — `{ get; }` (присвоюються лише в конструкторі), стан — `{ get; private set; }` (`IsIssued`, `Status`, `ReturnedOn`). Конструктори приватні, створення лише через `Book.Create`, `BookCopy.Register`, `Loan.Open` або `FromDto`. Колекція видач у `LendingService` — `private readonly List<Loan>`, назовні `IReadOnlyList<Loan>` через `AsReadOnly()`.

### 4. Фрагменти коду

Фабричний метод (`Loan.Open`): спочатку всі перевірки аргументів, потім зміна стану примірника, і лише тоді створення об'єкта:

```csharp
public static Loan Open(string id, BookCopy copy, string readerId, DateOnly issuedOn)
{
    ValidateArguments(id, copy, readerId, issuedOn);

    // Примірник сам відмовить, якщо вже виданий; об'єкт видачі тоді не створюється.
    copy.Issue();

    return new Loan(id.Trim(), copy, readerId.Trim(), issuedOn, returnedOn: null, LoanStatus.Open);
}
```

Метод зміни стану (`BookCopy.Issue`) і перевірка переходів (`Loan.EnsureCanMoveTo`):

```csharp
public void Issue()
{
    if (IsIssued)
        throw new InvalidOperationException(
            $"Примірник {Id} вже виданий, повторна видача неможлива");

    IsIssued = true;
}

private void EnsureCanMoveTo(LoanStatus target)
{
    string? error = (Status, target) switch
    {
        (LoanStatus.Open, LoanStatus.Lost or LoanStatus.Closed) => null,
        (LoanStatus.Lost, LoanStatus.Closed) => null,
        (LoanStatus.Closed, _) => $"Видача {Id} вже закрита {ReturnedOn:dd.MM.yyyy}, змінити її стан неможливо",
        (LoanStatus.Lost, LoanStatus.Lost) => $"Примірник {Copy.Id} за видачею {Id} вже позначено як втрачений",
        _ => $"Видача {Id}: перехід {Status} → {target} не передбачено"
    };

    if (error is not null)
        throw new InvalidOperationException(error);
}
```

Мапінг (використає сховище тижня 5): `FromDto` викликає ту саму фабрику / ті самі перевірки, що й первинне створення:

```csharp
public BookCopyDto ToDto() => new(Id, Isbn, IsIssued);

public static BookCopy FromDto(BookCopyDto dto)
{
    ArgumentNullException.ThrowIfNull(dto);
    return Create(dto.Id, dto.Isbn, dto.IsIssued);
}
```

### 5. Вивід консолі (`dotnet run --project src/Cli -- --domain`)

```text
=== Сценарій 1: успіх ===
B-001 [978-0-13-235088-4] «Чистий код», 2008 — Роберт Мартін
C-001 [978-0-13-235088-4] — на полиці
L-001: примірник C-001 → читач R-001, видано 01.09.2026, відкрита
C-001 [978-0-13-235088-4] — виданий
L-001: примірник C-001 → читач R-001, видано 01.09.2026, закрита 14.09.2026
C-001 [978-0-13-235088-4] — на полиці
L-002: примірник C-001 → читач R-002, видано 15.09.2026, відкрита

=== Сценарій 2: порушення інваріантів ===
 повторна видача виданого примірника: InvalidOperationException — Примірник C-001 вже виданий, повторна видача неможлива
 повернення примірника, що на полиці: InvalidOperationException — Примірник C-002 і так на полиці, повернути його неможливо
 порожній ISBN: ArgumentException — ISBN не може бути порожнім (Parameter 'isbn')
 ISBN неправильної довжини: ArgumentException — ISBN '978-0-13' некоректний: очікується 10 або 13 цифр (дефіси дозволені) (Parameter 'isbn')
 рік видання з майбутнього: ArgumentOutOfRangeException — Рік видання має бути в межах 1450..2026 (Parameter 'year')
Actual value was 2099.
 дата повернення раніше видачі: ArgumentOutOfRangeException — Дата повернення 01.09.2026 не може бути раніше дати видачі 15.09.2026 (Parameter 'returnedOn')
Actual value was 9/1/2026.
 закриття вже закритої видачі: InvalidOperationException — Видача L-001 вже закрита 14.09.2026, змінити її стан неможливо
 порожній читач: ArgumentException — Ідентифікатор читача обов'язковий (Parameter 'readerId')
 Стан після всіх відмов: L-002: примірник C-001 → читач R-002, видано 15.09.2026, відкрита; C-001 [978-0-13-235088-4] — виданий

=== Сценарій 3: ToDto / FromDto ===
 DTO      : LoanDto { Id = L-002, CopyId = C-001, ReaderId = R-002, IssuedOn = 9/15/2026, ReturnedOn = , Status = Open }
 Відновлено: L-002: примірник C-001 → читач R-002, видано 15.09.2026, відкрита
 FromDto: закрита видача без дати: ArgumentException — Закрита видача L-002 не має дати повернення (Parameter 'dto')
 FromDto: невідомий статус: ArgumentException — Невідомий статус видачі L-002: 'Archived' (Parameter 'dto')
 FromDto: примірник з порожнім ISBN: ArgumentException — ISBN не може бути порожнім (Parameter 'isbn')

=== Додаткове 1: ImportResult<BookDto> → сутності + помилки ===
 Файл: data\books_domain.csv
  + B-201 [978-0-13-235088-4] «Чистий код», 2008 — Роберт Мартін
  + B-202 [978-0-201-61622-4] «Прагматичний програміст», 1999 — Ендрю Гант
  + B-205 [0-201-63361-2] «Design Patterns (ISBN-10)», 1994 — Еріх Гамма
  + B-206 [0-8044-2957-X] «ISBN-10 з контрольним символом X», 1971
  ! рядок 8: очікую щонайменше 4 колонки, отримав 2
  ! запис B-203: ISBN '978-0-13' некоректний: очікується 10 або 13 цифр (дефіси дозволені) (Parameter 'isbn')
  ! запис B-204: ISBN 'ISBN-ABC-DEF' некоректний: очікується 10 або 13 цифр (дефіси дозволені) (Parameter 'isbn')
 Усього: 7, прийнято: 4, пропущено: 3, помилок: 42.9%

=== Додаткове 2: не більше 5 незакритих видач на читача ===
 Читач R-007 має незакритих видач: 5
 шоста видача: InvalidOperationException — Читач R-007 уже має 5 незакритих видач (ліміт 5), видати примірник C-106 неможливо
 Примірник C-106 після відмови: на полиці
 Після повернення L-001: L-006: примірник C-106 → читач R-007, видано 08.09.2026, відкрита

=== Додаткове 3: переходи стану Open → Lost → Closed ===
 L-002: примірник C-102 → читач R-007, видано 01.09.2026, примірник втрачено
 повторна позначка «втрачено»: InvalidOperationException — Примірник C-102 за видачею L-002 вже позначено як втрачений
 L-002: примірник C-102 → читач R-007, видано 01.09.2026, закрита 01.10.2026 (примірник знайшовся)
 втрата вже закритої видачі: InvalidOperationException — Видача L-002 вже закрита 01.10.2026, змінити її стан неможливо
```

**Спостережуваний факт**: у сценарії 1 стан змінюється лише через методи; у сценарії 2 жодна спроба не змінила об'єкти — після всіх відмов `L-002` так само відкрита, а `C-001` так само виданий. Після відмови шостої видачі примірник `C-106` лишився на полиці.

### 6. DTO проти сутності

`BookDto`, `BookCopyDto`, `LoanDto` — це **формат** даних: позиційні records з публічними `init`-властивостями, рівністю за значенням і без жодних правил; вони мають уміти прийняти будь-що, що лежить у файлі, щоб імпортер міг прочитати рядок і повідомити, що з ним не так. Сутності `Book`, `BookCopy`, `Loan` — це **поведінка**: приватні конструктори, стан без публічних сетерів, методи з бізнес-назвами, які не дозволяють перевести об'єкт у некоректний стан. Один тип не залишено, бо вимоги протилежні: DTO мусить бути відкритим і «всеїдним» для серіалізації (`System.Text.Json`, пізніше EF Core і Api), а сутність — закритою та прискіпливою. Якби це був один тип, то або серіалізатор потребував би публічних `set` і правила можна було б обійти присвоєнням, або кожен некоректний рядок файлу падав би ще на етапі десеріалізації без зрозумілої діагностики. Міст між ними — `ToDto()` / `FromDto()`, причому `FromDto` проходить ті самі перевірки, що й фабрика.

### 7. Додаткові завдання

1. **ImportResult → сутності + помилки** — `EntityImport.ToEntities(ImportResult<TDto>, Func<TDto, TEntity>)`. Помилки парсера тижня 3 переносяться, а до них додаються записи, що не пройшли інваріанти (`ArgumentException` / `InvalidOperationException` з `FromDto`). Файл `data/books_domain.csv` містить рядки `B-203`, `B-204`, які парсер пропускає (формат правильний), але домен відхиляє (ISBN неправильної довжини).
2. **Інваріант на дві сутності** — `LendingService.IssueCopy`: читачу з 5 незакритими видачами (включно зі статусом `Lost`) новий примірник не видається. *Чому в сервісі, а не в сутності*: окремий `Loan` не знає про інші видачі того самого читача, а `BookCopy` — про читача взагалі. Щоб перевірити правило в сутності, їй довелося б тримати посилання на всі видачі читача або на сховище, тобто сутність залежала б від інфраструктури й завантажувала б чужі дані. Сервіс (на тижні 5 — `CatalogService`) має доступ до сховища, збирає потрібні сутності, перевіряє правило між ними і лише потім викликає доменний метод `Loan.Open`, який відповідає за власні інваріанти.
3. **Явний стан-перелічування** — `enum LoanStatus { Open, Lost, Closed }`; допустимі переходи описано одним `switch` за кортежем `(Status, target)` у `Loan.EnsureCanMoveTo`. `MarkLost()` дозволений лише з `Open`, `Close()` — з `Open` або `Lost` (примірник знайшовся), зі стану `Closed` переходів немає.

### 8. Самоперевірка

1. `copy.IsIssued = true;` / `loan.Status = LoanStatus.Closed;` → **не компілюється**: `CS0200: Property or indexer 'BookCopy.IsIssued' cannot be assigned to -- it is read only`.
2. `new BookCopy("C-1", "978-0-13-235088-4", false)` → **не компілюється** (`CS1729`): публічного конструктора немає.
3. Пошук `Console.` і `File.` у `src/Core/Domain` — порожньо. Domain не посилається на `Cli`.
4. Усі інваріанти зі списку спрацьовують у демонстрації; повідомлення містять ключ запису і значення.
5. `FromDto` кожної сутності йде через ту саму фабрику/перевірки, що й первинне створення.
6. Збірка `net10.0` і `net8.0` — без попереджень і помилок; сценарії лабораторної 3 (`dotnet run --project src/Cli`) працюють як раніше.

### 9. Definition of Done (Лабораторна робота 4)

- [x] Сутності в `Core/Domain`; records тижня 3 лишились як DTO.
- [x] Стан інкапсульовано: `{ get; }` / `private set`, змінюють лише методи.
- [x] Є фабричні методи (`Create`, `Register`, `Open`), публічних конструкторів немає.
- [x] Реалізовано 12 інваріантів з осмисленими повідомленнями.
- [x] `Argument*` — для некоректного входу, `InvalidOperationException` — для неможливої в поточному стані операції.
- [x] Є `ToDto` / `FromDto` для `Book`, `BookCopy`, `Loan`.
- [x] Cli демонструє і успіх, і відмову; виводиться лише `Message`, без stack trace.
- [x] Колекції назовні — тільки для читання (`LendingService.Loans`).
- [x] Виконано всі 3 додаткові завдання.

### 10. Відповіді на питання для захисту

1. **Що таке інваріант?** Умова, що істинна весь час життя об'єкта. Мої — у таблиці розділу 3 (напр., «примірник не можна видати двічі» — `BookCopy.Issue`; «дата повернення ≥ дати видачі» — `Loan.Close`).
2. **Навіщо інкапсуляція, якщо дані з файлу можна підправити руками?** Саме тому: файл може бути пошкоджений, а `FromDto` проганяє дані через ті самі перевірки. Інкапсуляція гарантує, що в пам'яті не існує некоректного об'єкта незалежно від джерела даних.
3. **Чому конструктор приватний, а створення — через статичний метод?** Фабрика має бізнес-назву (`Open`, `Register`), перевіряє всі аргументи і може відмовити. Приватний конструктор гарантує, що обійти перевірки через `new` неможливо.
4. **ArgumentException vs InvalidOperationException?** `Argument*` — вхідне значення погане саме по собі (порожній ISBN, рік 2099). `InvalidOperationException` — аргументи нормальні, але операція неможлива в поточному стані (видати вже виданий примірник, закрити закриту видачу).
5. **Анемічна модель і чим вона погана тут?** Клас з `{ get; set; }` без поведінки. У бібліотеці будь-хто міг би написати `copy.IsIssued = false` без закриття видачі — примірник «на полиці», а видача відкрита.
6. **Чому `IReadOnlyList`, а не `List`?** Щоб зовнішній код не міг додати видачу в обхід ліміту через `Add`. `AsReadOnly()` ще й не дає привести тип назад до `List<T>`.
7. **DTO і сутність — чим відрізняються?** DTO переносить дані і приймає будь-що; сутність захищає правила (розділ 6).
8. **Чи потрібні перевірки в Cli?** Ні. Cli лише викликає доменні методи й обробляє винятки у `try/catch`.
9. **Що, якщо Cli і Api перевірятимуть правила кожен сам?** Перевірки розійдуться (одна забуде ліміт видач, інша — дату), і той самий запит буде прийнято в одному клієнті та відхилено в іншому.
10. **Чому повідомлення містить числа й ключ?** Щоб відтворити ситуацію без перегляду коду: «Дата повернення 01.09.2026 не може бути раніше дати видачі 15.09.2026» одразу каже, яка видача і що не так.
11. **Що не можна змінити після створення і чому?** `Id`, `Isbn` примірника, `ReaderId`, `IssuedOn` видачі, а `Book` — повністю. Це ідентичність та історичні факти: зміна `IssuedOn` чи читача після видачі означала б підробку історії; для виправлення треба закрити видачу й відкрити нову.


---

## Швидка перевірка всіх лабораторних

Усі команди виконуються з кореня репозиторію.

### На актуальному стані (`main`)

| Лабораторна | Команда | Очікуваний результат |
| :---------: | :------ | :------------------- |
| — | `dotnet build` | `0 Warning(s)`, `0 Error(s)`, збірка `Core` для net10.0 і net8.0 |
| 2 | `dotnet run --project src/Cli -- --info` | Відомості про середовище, RID `win-x64`, `Цільова збірка Core : .NET 10.0` |
| 2 | `dotnet run --project src/Cli -f net8.0 -p:TargetFrameworks=net8.0 -- --info` | `Runtime : .NET 8.0.21`, `Цільова збірка Core : .NET 8.0 (LTS)` |
| 2 | `dotnet build src/Core/Core.csproj` | Підкаталоги `net10.0/` та `net8.0/` у `src/Core/bin/Debug/` |
| 3 | `dotnet run --project src/Cli` | `data/sample.csv`: прийнято 10, пропущено 3 |
| 3 | `dotnet run --project src/Cli -- data/sample_valid.csv` | Прийнято 10, помилок 0.0% |
| 3 | `dotnet run --project src/Cli -- data/sample.json` | Прийнято 5, пропущено 1 (рік 2099) |
| 3 | `dotnet run --project src/Cli -- data/mixed.csv` | 3 книги + 3 читачі, пропущено 3 |
| 3 | `dotnet run --project src/Cli -- non_existent.csv` | `Файл не знайдено`, код завершення 1 |
| 4 | `dotnet run --project src/Cli -- --domain` | Сценарії «успіх», «порушення інваріантів», ToDto/FromDto, 3 додаткові завдання |
| 4 | `dotnet run --project src/Cli -- --domain data/sample.csv` | Додаткове завдання 1 на іншому файлі |
| 4 | `git grep -nE "Console\.\|File\." -- src/Core/Domain` | Порожній вивід |

### На комітах попередніх лабораторних

| Лабораторна | Переключення | Команди |
| :---------: | :----------- | :------ |
| 1 | `git switch --detach 4c38a6e` | `dotnet run --project src/Cli`, `dotnet run --project src/Cli -- --json` |
| 2 | `git switch --detach 1447cca` | `dotnet sln list`, `dotnet run --project src/Cli`, `dotnet run --project src/Cli -- --json`, `dotnet run --project src/Cli -f net8.0 -p:TargetFrameworks=net8.0` |
| 2 | (на `1447cca`) | 4 команди `dotnet publish` з розділу «Порівняння режимів публікації», потім `./pub/fdd/Cli.exe`, `./pub/trimmed/Cli.exe --json` |
| — | `git switch main` | Повернення до актуального стану |

Код завершення в PowerShell перевіряється командою `echo $LASTEXITCODE` одразу після запуску.
