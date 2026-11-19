namespace MicroLIMS.Application.Abstractions.Security;

public sealed class SecretProtectionException : Exception
{
    public SecretProtectionException(string message) : base(message)
    {
    }

    public SecretProtectionException(string message, Exception inner) : base(message, inner)
    {
    }
}
