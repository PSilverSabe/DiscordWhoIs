using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace DiscordWhoIs.Core.Configuration.Serializers;

/// <summary>
/// Composes multiple JsonTypeInfoResolvers, returning the first non-null JsonTypeInfo.
/// Use this to allow source-generated contexts to be used while still falling back to the default resolver.
/// </summary>
public sealed class CompositeJsonTypeInfoResolver(params IJsonTypeInfoResolver[] resolvers) : IJsonTypeInfoResolver
{
    private readonly IJsonTypeInfoResolver[] _resolvers = resolvers ?? Array.Empty<IJsonTypeInfoResolver>();

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        foreach (IJsonTypeInfoResolver resolver in _resolvers)
        {
            if (resolver == null)
            {
                continue;
            }

            JsonTypeInfo? info = resolver.GetTypeInfo(type, options);
            if (info != null)
            {
                return info;
            }
        }

        return null;
    }
}
