using HeroRegistry.Application.Common;

namespace HeroRegistry.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void A_value_converts_to_a_successful_result()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void An_error_converts_to_a_failed_result()
    {
        Result<int> result = Errors.NotFound("hero.not_found", "No hero.");

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void The_non_generic_result_reports_success_or_an_error()
    {
        Result.Success().IsSuccess.ShouldBeTrue();

        Result failure = Errors.Forbidden("Only admins.");
        failure.IsSuccess.ShouldBeFalse();
        failure.Error!.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void Validation_errors_carry_every_field_error()
    {
        var error = Errors.Validation(new Dictionary<string, string[]>
        {
            ["HeroName"] = ["HeroName is required."],
            ["PowerLevel"] = ["PowerLevel must be between 1 and 100."],
        });

        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe("validation.failed");
        error.FieldErrors.Keys.ShouldBe(["HeroName", "PowerLevel"], ignoreOrder: true);
    }

    [Fact]
    public void Errors_without_fields_have_an_empty_field_list()
    {
        Errors.Unauthorized().FieldErrors.ShouldBeEmpty();
        Errors.Conflict("hero.duplicate", "Taken.").FieldErrors.ShouldBeEmpty();
    }
}
