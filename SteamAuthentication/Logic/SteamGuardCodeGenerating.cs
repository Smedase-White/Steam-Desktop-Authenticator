using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

namespace SteamAuthentication.Logic;

internal static class SteamGuardCodeGenerating
{
    private static readonly byte[] SteamGuardCodeTranslations = "23456789BCDFGHJKMNPQRTVWXY"u8.ToArray();

    internal static string GenerateSteamGuardCode(string sharedSecret, long timestamp, ILogger logger)
    {
        if (string.IsNullOrEmpty(sharedSecret))
            return "";

        string sharedSecretUnescaped = Regex.Unescape(sharedSecret);
        byte[] sharedSecretArray = Convert.FromBase64String(sharedSecretUnescaped);

        byte[] timeArray = new byte[8];

        timestamp /= 30L;

        for (int i = 8; i > 0; i--)
        {
            timeArray[i - 1] = (byte)timestamp;
            timestamp >>= 8;
        }

        HMACSHA1 hmacGenerator = new() { Key = sharedSecretArray };

        byte[] hashedData = hmacGenerator.ComputeHash(timeArray);
        byte[] codeArray = new byte[5];

        try
        {
            byte b = (byte)(hashedData[19] & 0xF);

            int codePoint =
                (hashedData[b] & 0x7F) << 24 |
                (hashedData[b + 1] & 0xFF) << 16 |
                (hashedData[b + 2] & 0xFF) << 8 |
                (hashedData[b + 3] & 0xFF);

            for (int i = 0; i < 5; ++i)
            {
                codeArray[i] = SteamGuardCodeTranslations[codePoint % SteamGuardCodeTranslations.Length];
                codePoint /= SteamGuardCodeTranslations.Length;
            }
        }
        catch (Exception e)
        {
            logger.LogError("Error compute sda code, exception: {exception}", e.ToJson());
        }

        return Encoding.UTF8.GetString(codeArray);
    }

    public static string? GenerateConfirmationHash(long timeStamp, string? tag, string identitySecret, ILogger logger)
    {
        byte[] decode = Convert.FromBase64String(identitySecret);
        int n2 = 8;

        if (tag != null)
            if (tag.Length > 32)
                n2 = 8 + 32;
            else
                n2 = 8 + tag.Length;

        byte[] array = new byte[n2];
        int n3 = 8;

        while (true)
        {
            int n4 = n3 - 1;

            if (n3 <= 0)
                break;

            array[n4] = (byte)timeStamp;
            timeStamp >>= 8;
            n3 = n4;
        }

        if (tag != null)
            Array.Copy(Encoding.UTF8.GetBytes(tag), 0, array, 8, n2 - 8);

        try
        {
            HMACSHA1 hmacGenerator = new() { Key = decode };

            byte[] hashedData = hmacGenerator.ComputeHash(array);
            string encodedData = Convert.ToBase64String(hashedData, Base64FormattingOptions.None);
            string hash = WebUtility.UrlEncode(encodedData);

            return hash;
        }
        catch (Exception e)
        {
            logger.LogError("Error compute confirmation hash, exception: {exception}", e.ToJson());

            return null;
        }
    }
}