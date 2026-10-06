using Grpc.Core;

namespace Moongate.Admin.Api.Internal;

public sealed class UpstreamCallException : Exception
{
    public StatusCode StatusCode { get; }
    public bool MutationOutcomeUnknown { get; }
    public bool InvalidatesLocalSession { get; }

    public UpstreamCallException(StatusCode statusCode, bool mutationOutcomeUnknown = false, bool invalidatesLocalSession = true)
        : base("Moongate administration call failed.")
    {
        StatusCode = statusCode;
        MutationOutcomeUnknown = mutationOutcomeUnknown;
        InvalidatesLocalSession = invalidatesLocalSession;
    }
}
