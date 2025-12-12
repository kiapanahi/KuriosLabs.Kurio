using Microsoft.Extensions.Options;

namespace KuriousLabs.Kurio.Web.Services;

internal sealed class KurioServerOptionsValidator : IValidateOptions<KurioServerOptions>
{
    public ValidateOptionsResult Validate(string? name, KurioServerOptions options)
    {
        if (options.BaseUrl is null)
        {
            return ValidateOptionsResult.Fail("KurioServer:BaseUrl must be set.");
        }

        if (!options.BaseUrl.IsAbsoluteUri)
        {
            return ValidateOptionsResult.Fail("KurioServer:BaseUrl must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Hubs?.Downloads))
        {
            return ValidateOptionsResult.Fail("KurioServer:Hubs:Downloads must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Hubs.Queue))
        {
            return ValidateOptionsResult.Fail("KurioServer:Hubs:Queue must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Hubs.Stats))
        {
            return ValidateOptionsResult.Fail("KurioServer:Hubs:Stats must be set.");
        }

        return ValidateOptionsResult.Success;
    }
}
