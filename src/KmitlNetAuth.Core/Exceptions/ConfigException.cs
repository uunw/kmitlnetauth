namespace KmitlNetAuth.Core.Exceptions;

public class ConfigException : KmitlNetAuthException
{
    public ConfigException(string message) : base(message) { }
    public ConfigException(string message, Exception inner) : base(message, inner) { }
}
