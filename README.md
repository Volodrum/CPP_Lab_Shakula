# CrossApp – Лабораторна робота 2

Наскрізний навчальний проєкт з дисципліни **«Крос-платформне програмування»**.  
**Студент**: Шакула Володимир, група ФЕІ-32с  
**Тема**: Solution: бібліотека Core + Cli, multi-targeting, публікація

---

## 1. Предметна область: Бібліотека (Library Management)

- **Призначення**: Облік видач примірників видань читачам, контроль термінів повернення.
- **Основні сутності**: `Book` (видання), `BookCopy` (примірник), `Reader` (читач), `Loan` (видача/повернення).

---

## 2. Архітектура рішення

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
    │   ├── Dto/                      # Майбутні record-типи даних (тиждень 3): BookDto, LoanDto
    │   ├── Domain/                   # Сутності та бізнес-правила (тиждень 4): Book, Reader, Loan
    │   └── Storage/                  # Реалізації сховищ даних (тиждень 5): IBookRepository тощо
    └── Cli/
        ├── Cli.csproj                # Console App, посилається на Core через ProjectReference
        └── Program.cs                # Точка входу: парсинг аргументів та візуалізація (Console/JSON)
```

### Принципи проектування:
1. **Жодної бізнес-логіки в `Program.cs`**: Консольний застосунок відповідає лише за запуск, форматування виводу (текстового або JSON) та передачу результатів користувачу. Всі системні виклики та збір інформації реалізовано в `Core`.
2. **Одностороння залежність**: `Cli` посилається на `Core` (`Cli -> Core`). `Core` не посилається на `Cli`, що запобігає циклічним залежностям та дозволить підключити цей же `Core` до майбутнього Web API або Blazor.
3. **Multi-targeting**: Бібліотека `Core` підтримує цільові платформи `net10.0` та `net8.0`.

---

## 3. Збірка та запуск

### Збірка solution:
```bash
dotnet build
```

### Запуск CLI:
```bash
# Стандартний запуск
dotnet run --project src/Cli

# Вивід у форматі JSON
dotnet run --project src/Cli -- --json
```

### Збірка Core для кількох цільових платформ:
```bash
dotnet build src/Core/Core.csproj
# У src/Core/bin/Debug/ з'являються підкаталоги: net10.0/ та net8.0/
```

---

## 4. Порівняння режимів публікації

Команди для публікації у каталог `pub/`:

```powershell
# 1. Framework-dependent (FDD)
dotnet publish src/Cli -c Release -r win-x64 --self-contained false -o pub/fdd

# 2. Self-contained (SCD)
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -o pub/scd

# 3. Self-contained Single-File
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o pub/singlefile

# 4. Self-contained Trimmed + Single-File
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true -o pub/trimmed
```

### Порівняльна таблиця (виміряно на Windows 11 x64, .NET 10.0.11 SDK)

Вимірювання розміру виконано за допомогою PowerShell:
`(Get-ChildItem -Recurse <каталог> | Measure-Object Length -Sum).Sum / 1MB`

| RID | Режим публікації | Розмір | Кількість файлів | Потрібен .NET Runtime | Опис та примітки |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **win-x64** | **Framework-Dependent (FDD)** | **0.20 МБ** | 7 | **Так** (.NET 10) | Містить лише бінарні файли застосунку (`Cli.dll`, `Core.dll`, легкий apphost `Cli.exe` ~158 КБ). Використовує встановлений у системі .NET Runtime. |
| **win-x64** | **Self-Contained (SCD)** | **76.68 МБ** | 194 | **Ні** | Включає в себе повне середовище виконання .NET (CoreCLR), JIT-компілятор, бібліотеки BCL та залежності. Працює на машинах без встановленого .NET. |
| **win-x64** | **Single-File (SCD)** | **70.16 МБ** | 3 | **Ні** | Усі runtime-бібліотеки запаковано в один автономний виконуваний файл `Cli.exe` (+ `.pdb` налагоджувальні символи). |
| **win-x64** | **Trimmed Single-File (SCD)** | **12.58 МБ** | 3 | **Ні** | IL Linker/Trimmer видаляє невикористаний код і метадані. Розмір зменшено на **~83%** порівняно зі звичайним SCD. |

---

## 5. Різниця між Self-Contained та Framework-Dependent

- **Framework-Dependent (FDD)** публікує виключно скомпільований код застосунку та його залежності, покладаючись на глобально інстальований у системі .NET Runtime; це забезпечує мінімальний розмір розгортання (~0.2 МБ) та швидке оновлення середовища безпековими патчами ОС.
- **Self-Contained (SCD)** містить як код застосунку, так і копію середовища виконання .NET (CoreCLR + BCL) під конкретний ідентифікатор платформи (RID); застосунок може запускатися автономно на будь-якому комп'ютері без попереднього налаштування .NET, однак платить за це істотним розміром (~77 МБ) та необхідністю самостійно оновлювати runtime.

---

## 6. Аналіз Trimming та попередження IL2026

Під час публікації з параметром `-p:PublishTrimmed=true` компілятор видає попередження:
```text
Trim analysis warning IL2026: Program.<Main>$(String[]): Using member 'System.Text.Json.JsonSerializer.Serialize' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.
```
**Чому Trimming небезпечний для коду з рефлексією**:  
Trimmer аналізує статичні виклики та видаляє невикористані метадані типів, властивостей і методів. Бібліотека `System.Text.Json` за замовчуванням використовує рефлексію в runtime для обходу властивостей об'єктів. Якщо тип чи його властивості не були явно зафіксовані в коді, тример видалить їх або заблокує рефлексивну серіалізацію, що призведе до `System.InvalidOperationException` під час виконання. Для безпечної роботи у тримованих середовищах слід використовувати C# Source Generators (`[JsonSerializable]`).

---

## 7. Відповіді на питання для самоперевірки (Definition of Done)

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
