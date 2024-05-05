using System.Net;
using System.Threading.RateLimiting;

using RestSharp;

using SteamAuthentication.Exceptions;

namespace SteamAuthentication.LogicModels;

public class SteamRestClient
{
    private readonly RestClient _restClient;

    private RateLimiter? _rateLimiter;

    public IWebProxy? Proxy { get; }

    // ReSharper disable once MemberCanBeProtected.Global
    public SteamRestClient(IWebProxy? proxy)
    {
        _restClient = new RestClient(
            new RestClientOptions
            {
                Proxy = proxy,
                FollowRedirects = true,
                AutomaticDecompression = DecompressionMethods.GZip,
            });

        Proxy = proxy;
    }

    public SteamRestClient(HttpClient httpClient)
    {
        _restClient = new RestClient(httpClient);
    }

    public void SetRateLimiter(RateLimiter? rateLimiter) => _rateLimiter = rateLimiter;

    public async Task<RestResponse> ExecuteAsync(RestRequest request, CancellationToken ct)
    {
        if (_rateLimiter == null)
        {
            RestResponse response = await _restClient.ExecuteAsync(request, ct);

            return response;
        }

        using RateLimitLease lease = await _rateLimiter.AcquireAsync(1, ct);

        if (lease.IsAcquired)
        {
            RestResponse response = await _restClient.ExecuteAsync(request, ct);

            return response;
        }

        throw new RateLimiterException();
    }

    public async Task<RestResponse> ExecuteGetRequestAsync(string url, CookieContainer cookies,
        IEnumerable<(string name, string value)>? headers, string referer,
        CancellationToken cancellationToken = default)
    {
        RestRequest request = new(url) { CookieContainer = cookies };

        AddHeadersToRequest(request, referer);

        if (headers != null)
            foreach ((string name, string value) in headers)
                request.AddHeader(name, value);

        RestResponse response = await ExecuteAsync(request, cancellationToken);

        return response;
    }

    public async Task<RestResponse> ExecutePostRequestAsync(string url, CookieContainer cookies,
        IEnumerable<(string name, string value)>? headers, string referer,
        string body,
        CancellationToken cancellationToken = default)
    {
        RestRequest request = new(url, Method.Post) { CookieContainer = cookies };

        AddHeadersToRequest(request, referer);

        if (headers != null)
            foreach ((string name, string value) in headers)
                request.AddHeader(name, value);

        request.AddBody(body, ContentType.FormUrlEncoded);

        RestResponse response = await ExecuteAsync(request, cancellationToken);

        return response;
    }

    public async Task<RestResponse> ExecutePostRequestAsync(string url, CookieContainer cookies,
        IEnumerable<(string name, string value)>? headers,
        string body,
        CancellationToken cancellationToken = default)
    {
        RestRequest request = new(url, Method.Post) { CookieContainer = cookies };

        AddHeadersToRequest(request);

        if (headers != null)
            foreach ((string name, string value) in headers)
                request.AddHeader(name, value);

        request.AddBody(body, ContentType.FormUrlEncoded);

        RestResponse response = await ExecuteAsync(request, cancellationToken);

        return response;
    }

    public async Task<RestResponse> ExecuteGetRequestAsync(string url, CookieContainer? cookies,
        CancellationToken cancellationToken = default)
    {
        RestRequest request = new(url) { CookieContainer = cookies };

        AddHeadersToRequest(request);

        RestResponse response = await ExecuteAsync(request, cancellationToken);

        return response;
    }

    protected async Task<RestResponse> ExecutePostRequestWithoutHeadersAsync(string url,
        CookieContainer? cookies,
        string body,
        CancellationToken cancellationToken = default)
    {
        RestRequest request = new(url, Method.Post) { CookieContainer = cookies };

        request.AddBody(body);

        RestResponse response = await ExecuteAsync(request, cancellationToken);

        return response;
    }

    private static void AddHeadersToRequest(RestRequest request, string? referer = Endpoints.SteamCommunityUrl)
    {
        request.AddHeader("Accept", "application/json, text/javascript;q=0.9, */*;q=0.5");
        request.AddHeader("UserAgent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
        request.AddHeader("Accept-Encoding", "gzip, deflate");
        request.AddHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");

        if (referer != null)
            request.AddHeader("Referer", referer);
    }
}