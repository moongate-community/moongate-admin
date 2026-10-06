namespace Moongate.Admin.Api.Internal;

public sealed class ConfigurationException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public ConfigurationException(int statusCode, string code) : base(code)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
