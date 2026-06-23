using Microsoft.Extensions.Configuration;

namespace EventosVivos.Infrastructure.Persistence.Seed;

internal static class SeedPasswords
{
    // Pre-computed BCrypt hashes for the default local-development passwords.
    // Override these in production by setting Seed:AdminPasswordHash and Seed:UserPasswordHash.
    public static string AdminHash { get; private set; } =
        "$2a$11$LQ5iQL/ZfsZJOQDnrH.6peU7CxCg7FpnYfp30wVdSF4.bxOKRM37i";

    public static string UserHash { get; private set; } =
        "$2a$11$jV3jsyE.9ewiwreqVJDWOOqF5uHANGAJv14Oigj.N7Gn14LXMBG6i";

    public static void Initialize(IConfiguration configuration)
    {
        var seedSection = configuration.GetSection("Seed");
        AdminHash = seedSection["AdminPasswordHash"] ?? AdminHash;
        UserHash = seedSection["UserPasswordHash"] ?? UserHash;
    }
}
