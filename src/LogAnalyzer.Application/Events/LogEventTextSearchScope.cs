namespace LogAnalyzer.Application;

/// <summary>
/// Какие поля события участвуют в текстовом поиске (поле <see cref="LogEventSearchRequest.Query"/>).
/// </summary>
public enum LogEventTextSearchScope
{
    /// <summary>Сообщение, сырой текст, exception и URL (как раньше).</summary>
    All = 0,

    /// <summary>Только <c>message</c>.</summary>
    Message = 1,

    /// <summary>Только <c>exception</c>.</summary>
    Exception = 2,

    /// <summary>Только <c>raw_text</c>.</summary>
    RawText = 3,

    /// <summary>Только <c>url</c>.</summary>
    Url = 4,

    /// <summary><c>message</c> и <c>exception</c>.</summary>
    MessageAndException = 5
}
