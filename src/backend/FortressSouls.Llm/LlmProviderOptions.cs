namespace FortressSouls.Llm;

using Microsoft.Extensions.Configuration;

public enum LlmProviderType
{
    Fake,
    OpenAiCompatible
}

public sealed record LlmProviderOptions
{
    public const string ConfigurationSectionPath = "FortressSouls:Llm";
    public const string DefaultModel = "deepseek/deepseek-v3.2";
    public static readonly Uri DefaultEndpoint = new("https://openrouter.ai/api/v1", UriKind.Absolute);

    public LlmProviderType ProviderType { get; init; } = LlmProviderType.Fake;

    public string Endpoint { get; init; } = DefaultEndpoint.ToString();

    public string Model { get; init; } = DefaultModel;

    public string ApiKey { get; init; } = string.Empty;

    public int MaxOutputTokens { get; init; } = 500;

    public double Temperature { get; init; } = 0.85d;

    public int TimeoutSeconds { get; init; } = 45;

    public static LlmProviderOptions LoadAndValidate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = configuration.GetSection(ConfigurationSectionPath).Get<LlmProviderOptions>() ?? new LlmProviderOptions();
        return options.Validate();
    }

    public LlmProviderOptions Validate()
    {
        if (MaxOutputTokens is < 64 or > 2_000)
        {
            throw InvalidConfiguration("max_output_tokens_out_of_range");
        }

        if (Temperature is < 0 or > 2)
        {
            throw InvalidConfiguration("temperature_out_of_range");
        }

        if (TimeoutSeconds is < 5 or > 120)
        {
            throw InvalidConfiguration("timeout_seconds_out_of_range");
        }

        if (ProviderType != LlmProviderType.OpenAiCompatible)
        {
            return this;
        }

        if (!TryValidateEndpoint(Endpoint, out _))
        {
            throw InvalidConfiguration("endpoint_invalid");
        }

        if (string.IsNullOrWhiteSpace(Model) || Model.Length > 200)
        {
            throw InvalidConfiguration("model_invalid");
        }

        if (!string.IsNullOrWhiteSpace(ApiKey) && ApiKey.Length > 512)
        {
            throw InvalidConfiguration("api_key_invalid");
        }

        return this;
    }

    public Uri GetValidatedEndpointUri()
    {
        if (!TryValidateEndpoint(Endpoint, out var endpoint))
        {
            throw InvalidConfiguration("endpoint_invalid");
        }

        var safePath = endpoint.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? endpoint.AbsolutePath
            : $"{endpoint.AbsolutePath}/";
        return new UriBuilder(endpoint) { Path = safePath }.Uri;
    }

    private static bool TryValidateEndpoint(string endpoint, out Uri uri)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out uri!))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return true;
    }

    private static LlmProviderException InvalidConfiguration(string code) =>
        new(LlmProviderErrorCode.InvalidConfiguration, $"The chat provider configuration is invalid ({code.ToLowerInvariant()}).");
}
