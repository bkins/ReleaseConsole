namespace ReleaseConsole.Core;

public sealed record Component(ComponentType Type
                              , string       Name
                              , string       Description )
{
    public static readonly Component CpApi = new(ComponentType.Api
                                             , "API"
                                             , "Main API Application");

    public static readonly Component LaaMauiApp = new(ComponentType.Laa
                                                 , "Laa"
                                                 , "MAUI Mobile Application");

    public static readonly Component CpSharedPrimitives = new(ComponentType.SharedLibrary1
                                                    , "SharedLib1"
                                                    , "Shared Library 1");

    public static readonly Component CpClientCore = new(ComponentType.SharedLibrary2
                                                    , "SharedLib2"
                                                    , "Shared Library 2");

    public static Component FromString( string name ) => name switch
    {
            nameof(CpApi)                        => CpApi
          , nameof(LaaMauiApp) or "maui"         => LaaMauiApp
          , nameof(CpSharedPrimitives) or "lib1" => CpSharedPrimitives
          , nameof(CpClientCore) or "lib2"       => CpClientCore
          , _                                    => throw new ArgumentException($"Unknown component: {name} (acceptable component types: \"{nameof(CpApi)}\", \"{nameof(LaaMauiApp)}\" or \"maui\", \"{nameof(CpSharedPrimitives)}\" or \"lib1\", or \"{nameof(CpClientCore)}\" or \"lib2\" )")
    };

    public static IEnumerable<Component> All =>
    [
            CpApi
          , LaaMauiApp
          , CpSharedPrimitives
          , CpClientCore
    ];
}