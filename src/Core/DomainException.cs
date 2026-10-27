namespace Core;

// Тиждень 8: порушення правила бібліотеки — «примірник уже видано», «видача вже закрита», «ліміт видач читача».
// Аргументи окремого методу (порожній ISBN, рік поза межами) лишаються ArgumentException:
// це помилка того, хто викликав, а не стану бібліотеки. Технічні збої (диск, JSON) сюди не загортаються.
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception inner) : base(message, inner) { }
}
