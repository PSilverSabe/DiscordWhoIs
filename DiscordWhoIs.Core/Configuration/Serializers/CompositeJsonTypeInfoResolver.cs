namespace DiscordWhoIs.Databases.Serializers
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    /// <summary>
    /// Composes multiple JsonTypeInfoResolvers, returning the first non-null JsonTypeInfo.
    /// Use this to allow source-generated contexts to be used while still falling back to the default resolver.
    /// </summary>
    public sealed class CompositeJsonTypeInfoResolver : IJsonTypeInfoResolver
    {
        private readonly IJsonTypeInfoResolver[] _resolvers;

        public CompositeJsonTypeInfoResolver(params IJsonTypeInfoResolver[] resolvers)
        {
            _resolvers = resolvers ?? Array.Empty<IJsonTypeInfoResolver>();
        }

        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            foreach (var resolver in _resolvers)
            {
                if (resolver == null) continue;
                var info = resolver.GetTypeInfo(type, options);
                if (info != null) return info;
            }

            return null;
        }
    }
}
