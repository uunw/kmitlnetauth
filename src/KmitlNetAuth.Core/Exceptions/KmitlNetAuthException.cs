namespace KmitlNetAuth.Core.Exceptions;

public class KmitlNetAuthException : Exception
{
    public KmitlNetAuthException(string message) : base(message) { }
    public KmitlNetAuthException(string message, Exception inner) : base(message, inner) { }
}
