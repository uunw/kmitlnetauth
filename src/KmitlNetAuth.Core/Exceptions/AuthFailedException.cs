namespace KmitlNetAuth.Core.Exceptions;

public class AuthFailedException : KmitlNetAuthException
{
    public AuthFailedException(string message) : base(message) { }
    public AuthFailedException(string message, Exception inner) : base(message, inner) { }
}
