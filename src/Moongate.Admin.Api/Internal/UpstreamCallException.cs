using Grpc.Core;

namespace Moongate.Admin.Api.Internal;

public sealed class UpstreamCallException : Exception
{
    public StatusCode StatusCode { get; }
    public bool MutationOutcomeUnknown { get; }
    public UpstreamCallException(StatusCode statusCode, bool mutationOutcomeUnknown = false)
        : base("Moongate administration call failed.")
    {
        StatusCode = statusCode;
        MutationOutcomeUnknown = mutationOutcomeUnknown;
    }
}
