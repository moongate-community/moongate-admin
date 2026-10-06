using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class TestGrpcCertificates : IDisposable
{
    public X509Certificate2 Root { get; }
    public X509Certificate2 Server { get; }

    public TestGrpcCertificates()
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=Moongate test CA",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        Root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var serverKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true)
        );
        using var signed = request.Create(
            Root,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            RandomNumberGenerator.GetBytes(16)
        );
        Server = signed.CopyWithPrivateKey(serverKey);
    }

    public HttpMessageHandler TrustedHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                {
                    if (certificate is null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                    {
                        return false;
                    }

                    using var chain = new X509Chain();
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(Root);
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    using var leaf = new X509Certificate2(certificate);
                    return chain.Build(leaf);
                }
            }
        };
    }

    public void Dispose()
    {
        Server.Dispose();
        Root.Dispose();
    }
}
