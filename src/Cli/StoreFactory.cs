using Core;
using Core.Abstractions;
using Core.Storage;

// Додаткове завдання 3: вибір реалізації за аргументами командного рядка винесено з Program.cs.
// Фабрика — частина composition root, тому живе в Cli: Core про конкретні класи сховищ не дізнається.
internal static class StoreFactory
{
    public static IBookStore Create(string[] args, string dataPath)
    {
        // --empty (лаб. 7): порожнє сховище в пам'яті — перевірка звітів на порожньому джерелі.
        IBookStore store = args.Contains("--file") ? new FileBookStore(dataPath)
            : args.Contains("--empty") ? new InMemoryBookStore()
            : new InMemoryBookStore(SampleData.Items());

        // Декоратор накладається поверх будь-якої з двох реалізацій.
        return args.Contains("--cache") ? new CachingBookStore(store) : store;
    }
}
