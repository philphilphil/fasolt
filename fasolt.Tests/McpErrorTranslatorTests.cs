using System.Text.Json;
using Fasolt.Server.Api.McpTools;
using Fasolt.Server.Application.Services;
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

    [Fact]
    public void Linked_content_is_a_caller_error_not_a_server_fault()
    {
        McpErrorTranslator.IsCallerError(LinkedContentException.Deck()).Should().BeTrue();
        McpErrorTranslator.IsCallerError(new ArgumentException("boom")).Should().BeTrue();
        McpErrorTranslator.IsCallerError(new InvalidOperationException("boom")).Should().BeFalse();

        // Only the classification changed — a linked-content refusal is not a bad
        // argument, so the "Invalid arguments" phrasing must not apply to it.
        McpErrorTranslator.IsInputError(LinkedContentException.Deck()).Should().BeFalse();
    }

    [Theory]
    [InlineData("update_cards")]
    [InlineData("delete_cards")]
    [InlineData("update_deck")]
    [InlineData("delete_deck")]
    [InlineData("assign_cards_to_deck")]
    [InlineData("add_svg_to_card")]
    [InlineData("publish_deck")]
    public void Linked_content_gets_a_structured_error_instead_of_the_generic_internal_message(string toolName)
    {
        var result = McpErrorTranslator.ToErrorResult(LinkedContentException.Card(), toolName);

        result.IsError.Should().BeTrue();
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.Should().NotContain("Internal error");

        var payload = JsonDocument.Parse(text).RootElement;
        payload.GetProperty("error").GetString().Should().Be("linked_content");
        payload.GetProperty("message").GetString().Should().Contain("linked from another account");
        payload.GetProperty("hint").GetString().Should().Contain("convert the deck to a copy");
    }
}
