using System.IO.Compression;
using System.Text;

using Microsoft.Extensions.Logging;

namespace SteamAuthentication.Logic;

public static class GZipDecoding
{
    public static async Task<string> DecodeGZipAsync(byte[] bytes, ILogger logger, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Start decoding gzip");

        GZipStream gZipStream = new(new MemoryStream(bytes), CompressionMode.Decompress);
        StreamReader stringReader = new(gZipStream);

        try
        {
            string content = await stringReader.ReadToEndAsync(cancellationToken);

            return content;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }
}