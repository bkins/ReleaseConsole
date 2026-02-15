namespace ReleaseConsole.Core;

public sealed record Component( ComponentType Type
                              , string        Name
                              , string        Description 
    , string TargetName = "" )
{
    public static readonly Component CpApi = new(ComponentType.Api
                                               , "API"
                                               , "Main API Application");

    public static readonly Component LaaMauiApp = new(ComponentType.Laa
                                                    , "Laa"
                                                    , "MAUI Mobile Application");

    private const string CpClientCodeDescription = """
                                                         Core client-side library for offline intent capture and
                                                         read-only Knowledge caching for the CP ecosystem.
                                                   """;

    public static readonly Component CpClientCore = new(ComponentType.CpClientCore
                                                      , "CpClientCore"
                                                      , CpClientCodeDescription
                                                      , "Core");

    private const string CpSharedPrimitivesDescription = """
                                                             Pure, domain-agnostic primitive helpers shared across Cognitive Platform components.
                                                             This package contains small, stable utilities such as primitive extensions,
                                                             guard clauses, and result types that are safe to use by any CP client or service.
                                                             It intentionally   contains no domain logic, no client behavior, and no API-specific concerns.
                                                         """;

    public static readonly Component CpSharedPrimitives = new(ComponentType.CpSharedPrimitives
                                                                        , "CpSharedPrimitives"
                                                                        , CpSharedPrimitivesDescription
                                                                        , "Primitives");

    public static readonly Component AllNugetComponents = new(ComponentType.AllNuget
                                                            , "AllNugetComponents"
                                                            , "Represents all components that produce NuGet packages (currently CpSharedPrimitives and CpClientCore)"
                                                            , "All");
    public static Component FromString( string name ) => name switch
    {
            nameof(CpApi)                        => CpApi
          , nameof(LaaMauiApp) or "maui"         => LaaMauiApp
          , nameof(CpSharedPrimitives) or "lib1" => CpSharedPrimitives
          , nameof(CpClientCore) or "lib2"       => CpClientCore
          , _ => throw new ArgumentException(
                     $"Unknown component: {name} (acceptable component types: \"{nameof(CpApi)}\", \"{nameof(LaaMauiApp)}\" or \"maui\", \"{nameof(CpSharedPrimitives)}\" or \"lib1\", or \"{nameof(CpClientCore)}\" or \"lib2\" )")
    };

    public static IEnumerable<Component> All =>
    [
            CpApi
          , LaaMauiApp
          , CpSharedPrimitives
          , CpClientCore
    ];
}