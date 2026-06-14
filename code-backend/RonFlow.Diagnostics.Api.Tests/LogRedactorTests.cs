namespace RonFlow.Diagnostics.Api.Tests;

public sealed class LogRedactorTests
{
    [Test]
    public void Redact_RemovesKnownSecretShapes()
    {
        var redactor = new Api.LogRedactor();

        var redacted = redactor.Redact("github_pat_ABC123 https://token@github.com/org/repo.git Bearer abc.def Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ== Password=secret");

        Assert.That(redacted, Does.Contain("github_pat_***"));
        Assert.That(redacted, Does.Contain("https://***@github.com"));
        Assert.That(redacted, Does.Contain("Bearer ***"));
        Assert.That(redacted, Does.Contain("Basic ***"));
        Assert.That(redacted, Does.Contain("Password=***"));
        Assert.That(redacted, Does.Not.Contain("ABC123"));
        Assert.That(redacted, Does.Not.Contain("token@github.com"));
        Assert.That(redacted, Does.Not.Contain("abc.def"));
        Assert.That(redacted, Does.Not.Contain("secret"));
    }
}
