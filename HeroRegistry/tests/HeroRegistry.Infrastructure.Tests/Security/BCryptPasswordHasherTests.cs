using HeroRegistry.Infrastructure.Security;

namespace HeroRegistry.Infrastructure.Tests.Security;

public class BCryptPasswordHasherTests
{
    /// <summary>The hash stored for the seeded administrator in 0003_SeedData.sql (password "Admin").</summary>
    private const string SeededAdminHash = "$2a$11$rnYVgixh.98DnFhT80CktedjRbl0EMfnBjZppegPZdRKmMXDk2uTq";

    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void A_hash_is_a_BCrypt_hash_with_work_factor_11_never_the_password()
    {
        var hash = _hasher.Hash("Sp1der-Sense!");

        hash.ShouldNotContain("Sp1der-Sense!");
        hash.ShouldStartWith("$2a$11$");
        hash.Length.ShouldBe(60);
    }

    [Fact]
    public void Hashing_the_same_password_twice_gives_different_hashes_because_of_the_salt()
    {
        _hasher.Hash("Sp1der-Sense!").ShouldNotBe(_hasher.Hash("Sp1der-Sense!"));
    }

    [Fact]
    public void The_right_password_verifies_and_a_wrong_one_does_not()
    {
        var hash = _hasher.Hash("Sp1der-Sense!");

        _hasher.Verify("Sp1der-Sense!", hash).ShouldBeTrue();
        _hasher.Verify("sp1der-sense!", hash).ShouldBeFalse();
        _hasher.Verify("Sp1der-Sense! ", hash).ShouldBeFalse();
    }

    [Fact]
    public void The_seeded_administrator_hash_verifies_with_Admin()
    {
        _hasher.Verify("Admin", SeededAdminHash).ShouldBeTrue();
        _hasher.Verify("admin", SeededAdminHash).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Without_a_hash_verification_runs_against_a_dummy_and_fails(string? hash)
    {
        _hasher.Verify("Admin", hash).ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2a$11$tooShort")]
    public void A_malformed_hash_fails_without_throwing(string hash)
    {
        _hasher.Verify("Admin", hash).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_empty_password_cannot_be_hashed(string? password)
    {
        Should.Throw<ArgumentException>(() => _hasher.Hash(password!));
    }
}
