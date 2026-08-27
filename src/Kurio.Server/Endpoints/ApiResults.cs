using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Helpers that produce the same <see cref="ProblemDetails" /> payload the MVC
///     <c>ControllerBase.Problem(...)</c> helper used to produce, so the wire format of error
///     responses is unchanged by the move to minimal APIs.
/// </summary>
internal static class ApiResults
{
    /// <summary>
    ///     Creates an RFC 7807 problem response, including the <c>traceId</c> extension that
    ///     MVC's <c>DefaultProblemDetailsFactory</c> attached automatically.
    /// </summary>
    /// <param name="httpContext">The current request context, used for the trace identifier.</param>
    /// <param name="detail">Human-readable explanation of the failure.</param>
    /// <param name="statusCode">HTTP status code to return.</param>
    /// <param name="title">Short, human-readable summary of the problem type.</param>
    public static IResult Problem(
        HttpContext httpContext,
        string detail,
        int statusCode,
        string title)
    {
        ProblemDetails problemDetails = new()
        {
            Detail = detail,
            Status = statusCode,
            Title = title
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (traceId is not null)
        {
            problemDetails.Extensions["traceId"] = traceId;
        }

        return TypedResults.Problem(problemDetails);
    }
}
