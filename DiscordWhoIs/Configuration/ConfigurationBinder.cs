using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Configuration
{
    public static class ConfigurationBinder
    {
        /// <summary>
        /// Binds a configuration section to a strongly-typed object and validates that the section exists.
        /// Supports classes with required members.
        /// </summary>
        public static T BindValidated<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IConfiguration configuration,
            string sectionName) where T : class
        {
            var section = configuration.GetSection(sectionName);
            if (!section.Exists())
                throw new InvalidOperationException($"Configuration section '{sectionName}' is missing.");

            var instance = section.Get<T>();
            if (instance == null)
                throw new InvalidOperationException($"Failed to bind configuration section '{sectionName}' to type {typeof(T).Name}.");

            return instance;
        }
    }
}
