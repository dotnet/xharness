using Microsoft.Extensions.Logging;

public class PlaywrightLogger : ILogger
{
    private readonly LogLevel _logLevel;

    public PlaywrightLogger(LogLevel logLevel)
    {
        _logLevel = logLevel;
    }

    public void Log(LogLevel logLevel, string message)
    {
        if (logLevel >= _logLevel)
        {
            Console.WriteLine($"{logLevel}: {message}");
        }
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;
}