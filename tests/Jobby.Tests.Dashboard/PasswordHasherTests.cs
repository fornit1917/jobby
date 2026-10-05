using Jobby.Dashboard.Authorization;

namespace Jobby.Tests.Dashboard;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_True()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_WrongPassword_False()
    {
        var hash = PasswordHasher.Hash("s3cret");
        Assert.False(PasswordHasher.Verify("S3cret", hash));
        Assert.False(PasswordHasher.Verify("", hash));
    }

    [Fact]
    public void Hash_Format_IsSelfDescribing()
    {
        var hash = PasswordHasher.Hash("pw");
        Assert.StartsWith("JDPBKDF2$v1$", hash);
        Assert.Equal(5, hash.Split('$').Length);
    }

    [Fact]
    public void Hash_UsesRandomSalt_DifferentEachTime()
    {
        Assert.NotEqual(PasswordHasher.Hash("pw"), PasswordHasher.Hash("pw"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("JDPBKDF2$v1$bad")]
    [InlineData("JDPBKDF2$v2$210000$AAAA$AAAA")]
    public void Verify_MalformedHash_FalseNotThrow(string encoded)
    {
        Assert.False(PasswordHasher.Verify("pw", encoded));
    }

    [Fact]
    public void IsValidEncoding_TrueForRealHash_FalseForPrefixedGarbage()
    {
        Assert.True(PasswordHasher.IsValidEncoding(PasswordHasher.Hash("pw")));
        Assert.False(PasswordHasher.IsValidEncoding("JDPBKDF2$v1$210000$@@@$@@@"));
        Assert.False(PasswordHasher.IsValidEncoding("JDPBKDF2$v1$210000$AAAA$AAAA"));
        Assert.False(PasswordHasher.IsValidEncoding("nope"));
    }
}