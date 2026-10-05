using Jobby.Dashboard.Authorization;

namespace Jobby.Tests.Dashboard;

public class BasicCredentialsTests
{
    private static readonly string Hash = PasswordHasher.Hash("s3cret");

    [Fact]
    public void CorrectUserAndPassword_True()
    {
        Assert.True(BasicCredentials.Validate("admin", "s3cret", "admin", Hash));
    }

    [Fact]
    public void WrongPassword_False()
    {
        Assert.False(BasicCredentials.Validate("admin", "nope", "admin", Hash));
    }

    [Fact]
    public void WrongUser_False()
    {
        Assert.False(BasicCredentials.Validate("root", "s3cret", "admin", Hash));
    }

    [Fact]
    public void DifferentLengthUser_False()
    {
        Assert.False(BasicCredentials.Validate("administrator", "s3cret", "admin", Hash));
    }

    [Fact]
    public void NullInputs_False()
    {
        Assert.False(BasicCredentials.Validate(null, null, "admin", Hash));
    }
}