using WebPush;

namespace RonFlow.Infrastructure;

public sealed class PushNotificationConfiguration
{
    private const string DefaultSubject = "mailto:ronflow@example.local";

    public PushNotificationConfiguration(string subject, string publicKey, string privateKey)
    {
        Subject = subject;
        PublicKey = publicKey;
        PrivateKey = privateKey;
    }

    public string Subject { get; }

    public string PublicKey { get; }

    public string PrivateKey { get; }

    public static PushNotificationConfiguration Create(string? subject, string? publicKey, string? privateKey)
    {
        var resolvedSubject = string.IsNullOrWhiteSpace(subject)
            ? DefaultSubject
            : subject.Trim();

        if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(privateKey))
        {
            return new PushNotificationConfiguration(resolvedSubject, publicKey.Trim(), privateKey.Trim());
        }

        var generatedKeys = VapidHelper.GenerateVapidKeys();
        return new PushNotificationConfiguration(resolvedSubject, generatedKeys.PublicKey, generatedKeys.PrivateKey);
    }
}