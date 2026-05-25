using Fasolt.Server.Api.McpTools;
using FluentAssertions;
using ModelContextProtocol.Protocol;

namespace Fasolt.Tests;

public class McpErrorTranslatorTests
{
    [Fact]
    public void ArgumentException_message_is_surfaced_to_caller()
    {
        var ex = new ArgumentException("The arguments dictionary is missing a value for the required parameter 'cards'.");

        var result = McpErrorTranslator.ToErrorResult(ex, "update_cards");

        result.IsError.Should().BeTrue();
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.Should().Contain("update_cards");
        text.Should().Contain("'cards'");
        text.Should().StartWith("Invalid arguments");
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(ArgumentNullException))]
    [InlineData(typeof(FormatException))]
    [InlineData(typeof(System.Text.Json.JsonException))]
    public void Input_error_types_are_classified_as_user_errors(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "boom")!;
        McpErrorTranslator.IsInputError(ex).Should().BeTrue();
    }

    [Fact]
    public void Internal_exceptions_get_generic_message_without_leaking_details()
    {
        var ex = new InvalidOperationException("connection string foo=bar password=secret");

        var result = McpErrorTranslator.ToErrorResult(ex, "create_cards");

        result.IsError.Should().BeTrue();
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.Should().Contain("Internal error");
        text.Should().Contain("create_cards");
        text.Should().Contain("InvalidOperationException");
        text.Should().NotContain("password");
        text.Should().NotContain("secret");
    }
}
