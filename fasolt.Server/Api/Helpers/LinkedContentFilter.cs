using Fasolt.Server.Application.Services;

namespace Fasolt.Server.Api.Helpers;

public static class LinkedContentFilter
{
    /// <summary>
    /// Turns a <see cref="LinkedContentException"/> thrown anywhere below into a 403
    /// with the usual <c>{ error, message }</c> body. Applied to the whole card and
    /// deck groups so every mutation path — single, bulk or nested — reports linked
    /// content the same way.
    /// </summary>
    public static TBuilder AddLinkedContentGuard<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            try
            {
                return await next(context);
            }
            catch (LinkedContentException ex)
            {
                return Results.Json(
                    new { error = LinkedContentException.ErrorCode, message = ex.Message },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        });

        return builder;
    }
}
