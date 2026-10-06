using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Moongate.Admin.Tests.TestSupport.Hosting;

public sealed class ChunkedJsonContent : HttpContent
{
    private readonly byte[] _bytes;
    public ChunkedJsonContent(string json)
    {
        _bytes = Encoding.UTF8.GetBytes(json);
        Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }
    protected override bool TryComputeLength(out long length)
    {
        length = 0; return false;
    }
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await stream.WriteAsync(_bytes);
    }
}
