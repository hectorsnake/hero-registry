using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Users;
using static HeroRegistry.Domain.Tests.TestData;

namespace HeroRegistry.Domain.Tests.Users;

public class UserTests
{
    private const string PasswordHash = "$2a$11$rnYVgixh.98DnFhT80CktedjRbl0EMfnBjZppegPZdRKmMXDk2uTq";

    [Fact]
    public void Register_creates_a_regular_user()
    {
        var user = User.Register("peter.parker", "peter@dailybugle.com", PasswordHash, UtcNow);

        user.UserName.ShouldBe("peter.parker");
        user.Email.ShouldBe("peter@dailybugle.com");
        user.Password.ShouldBe(PasswordHash);
        user.IsAdmin.ShouldBeFalse(); // self-registration can never create an admin
        user.IsDeleted.ShouldBeFalse();
        user.CreatedAt.ShouldBe(UtcNow);
    }

    [Fact]
    public void Self_registration_has_no_creator()
    {
        User.Register("peter.parker", "peter@dailybugle.com", PasswordHash, UtcNow).CreatedBy.ShouldBeNull();
    }

    [Fact]
    public void A_user_registered_by_someone_else_records_the_creator()
    {
        User.Register("peter.parker", "peter@dailybugle.com", PasswordHash, UtcNow, createdBy: AdminId)
            .CreatedBy.ShouldBe(AdminId);
    }

    [Fact]
    public void User_name_is_trimmed_and_email_normalized()
    {
        var user = User.Register("  peter.parker ", " Peter@DailyBugle.com ", PasswordHash, UtcNow);

        user.UserName.ShouldBe("peter.parker");
        user.Email.ShouldBe("peter@dailybugle.com");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("Admin")]
    [InlineData("peter.parker_99-x")]
    [InlineData("12345678901234567890123456789012345678901234567890")] // 50
    public void Valid_user_names_are_accepted(string userName)
    {
        User.Register(userName, "user@heroregistry.com", PasswordHash, UtcNow).UserName.ShouldBe(userName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("peter parker")]
    [InlineData("peter@parker")]
    [InlineData("pétér")]
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51
    public void Invalid_user_names_are_rejected(string? userName)
    {
        Should.Throw<DomainException>(() => User.Register(userName!, "user@heroregistry.com", PasswordHash, UtcNow))
            .Field.ShouldBe("UserName");
    }

    [Fact]
    public void An_invalid_email_is_rejected()
    {
        Should.Throw<DomainException>(() => User.Register("peter", "not-an-email", PasswordHash, UtcNow))
            .Field.ShouldBe("Email");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_password_hash_is_required(string? hash)
    {
        Should.Throw<DomainException>(() => User.Register("peter", "peter@dailybugle.com", hash!, UtcNow))
            .Field.ShouldBe("Password");
    }

    [Fact]
    public void A_password_hash_longer_than_the_column_is_rejected()
    {
        Should.Throw<DomainException>(
                () => User.Register("peter", "peter@dailybugle.com", new string('x', 201), UtcNow))
            .Field.ShouldBe("Password");
    }

    [Fact]
    public void Users_can_be_soft_deleted()
    {
        var user = User.Register("peter", "peter@dailybugle.com", PasswordHash, UtcNow);

        user.SoftDelete(AdminId, UtcNow.AddDays(1));

        user.IsDeleted.ShouldBeTrue();
        user.UpdatedBy.ShouldBe(AdminId);
    }
}
