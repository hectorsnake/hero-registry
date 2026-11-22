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

    // Account update (AC11)

    [Fact]
    public void UpdateAccount_changes_the_user_name_and_email_and_records_who_and_when()
    {
        var user = User.Register("peter", "peter@dailybugle.com", PasswordHash, UtcNow);
        var later = UtcNow.AddDays(1);

        user.UpdateAccount(" spider.man ", " Spidey@Avengers.org ", OtherUserId, later);

        user.UserName.ShouldBe("spider.man");
        user.Email.ShouldBe("spidey@avengers.org");
        user.UpdatedBy.ShouldBe(OtherUserId);
        user.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void UpdateAccount_never_changes_the_password_the_admin_flag_or_the_creation_audit()
    {
        var user = User.Register("peter", "peter@dailybugle.com", PasswordHash, UtcNow, createdBy: AdminId);

        user.UpdateAccount("spider.man", "spidey@avengers.org", OtherUserId, UtcNow.AddDays(1));

        user.Password.ShouldBe(PasswordHash);
        user.IsAdmin.ShouldBeFalse();
        user.CreatedBy.ShouldBe(AdminId);
        user.CreatedAt.ShouldBe(UtcNow);
    }

    [Theory]
    [InlineData("ab", "spidey@avengers.org", "UserName")]
    [InlineData("spider man", "spidey@avengers.org", "UserName")]
    [InlineData("spider.man", "not-an-email", "Email")]
    public void An_invalid_update_is_rejected_and_leaves_the_account_unchanged(string userName, string email, string field)
    {
        var user = User.Register("peter", "peter@dailybugle.com", PasswordHash, UtcNow);

        Should.Throw<DomainException>(() => user.UpdateAccount(userName, email, OtherUserId, UtcNow.AddDays(1)))
            .Field.ShouldBe(field);

        user.UserName.ShouldBe("peter");
        user.Email.ShouldBe("peter@dailybugle.com");
        user.UpdatedBy.ShouldBeNull();
    }

    [Fact]
    public void A_deleted_account_cannot_be_updated()
    {
        var user = User.Register("peter", "peter@dailybugle.com", PasswordHash, UtcNow);
        user.SoftDelete(AdminId, UtcNow.AddDays(1));

        Should.Throw<DomainException>(() => user.UpdateAccount("spider.man", "spidey@avengers.org", AdminId, UtcNow.AddDays(2)));
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
