using System.Collections.Specialized;

using Microsoft.Extensions.Logging;

namespace SteamAuthentication.Logic;

internal static class SdaConfirmationsLogic
{
    public static string GenerateConfirmationUrl(long timeStamp, string deviceId, string identitySecret,
        ulong steamId, string tag, ILogger logger)
    {
        string endpoint = Endpoints.SteamCommunityUrl + "/mobileconf/getlist?";

        string queryString = GenerateConfirmationQueryParams(tag, deviceId, identitySecret, steamId, timeStamp, logger);

        return endpoint + queryString;
    }

    public static string GenerateConfirmationQueryParams(string tag, string deviceId, string identitySecret,
        ulong steamId, long timeStamp, ILogger logger)
    {
        if (string.IsNullOrEmpty(deviceId))
            throw new ArgumentException("Device Id is not present");

        NameValueCollection queryParams =
            GenerateConfirmationQueryParameters(tag, deviceId, identitySecret, steamId, timeStamp, logger);

        return "p=" + queryParams["p"] + "&a=" + queryParams["a"] + "&k=" + queryParams["k"] + "&t=" +
               queryParams["t"] + "&m=android&tag=" + queryParams["tag"];
    }

    public static NameValueCollection GenerateConfirmationQueryParameters(string tag, string deviceId,
        string identitySecret,
        ulong steamId,
        long timeStamp,
        ILogger logger)
    {
        if (string.IsNullOrEmpty(deviceId))
            throw new ArgumentException("Device Id is not present");

        string? k = SteamGuardCodeGenerating.GenerateConfirmationHash(timeStamp, tag, identitySecret, logger) ??
            throw new ArgumentException("Cannot generate confirmation hash");
        NameValueCollection result = new()
        {
            { "p", deviceId },
            { "a", steamId.ToString() },
            { "k", k },
            { "t", timeStamp.ToString() },
            { "m", "android" },
            { "tag", tag }
        };

        return result;
    }
}