namespace ReleaseConsole.Core;

public static class ApiEnvironments
{
    public static string BaseUrl = "http://localhost";
    public static string DevPort = ":5273";
    public static string QaPort = ":5274";
    public static string ProdPort = ":5275";

    public static string Url( Environment environment )
    {
        return environment switch
        {
                Environment.Dev  => BaseUrl + DevPort
              , Environment.Qa   => BaseUrl + QaPort
              , Environment.Prod => BaseUrl + ProdPort
              , _ => throw new ArgumentOutOfRangeException(nameof(environment)
                                                         , environment
                                                         , null)
        };
    }
    
}