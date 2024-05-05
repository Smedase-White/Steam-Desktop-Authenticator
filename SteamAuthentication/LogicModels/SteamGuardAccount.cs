using System.Net;
using System.Text;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using RestSharp;

using SteamAuthentication.Exceptions;
using SteamAuthentication.Logic;
using SteamAuthentication.Models;
using SteamAuthentication.Responses;

using SteamKit2;
using SteamKit2.Authentication;

namespace SteamAuthentication.LogicModels;

public class SteamGuardAccount
{
    public ISteamTime SteamTime { get; }

    private readonly ILogger<SteamGuardAccount> _logger;

    public SteamMaFile MaFile { get; private set; }

    public SteamRestClient RestClient { get; }

    public SteamGuardAccount(SteamMaFile maFile, SteamRestClient restClient, ISteamTime steamTime,
        ILogger<SteamGuardAccount> logger)
    {
        SteamTime = steamTime;
        _logger = logger;
        MaFile = maFile;
        RestClient = restClient;
    }

    public async Task<string?>
        TryGenerateSteamGuardCodeForTimeStampAsync(CancellationToken cancellationToken = default) =>
        await TryHelpers.TryAsync(GenerateSteamGuardCodeForTimeStampAsync(cancellationToken));

    public async Task<string> GenerateSteamGuardCodeForTimeStampAsync(CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = _logger.CreateScopeForMethod(this);

        long timeStamp;

        try
        {
            timeStamp = await SteamTime.GetCurrentSteamTimeAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception while get steamTime: {exception}", e.ToJson());
            throw;
        }

        try
        {
            string code = SteamGuardCodeGenerating.GenerateSteamGuardCode(MaFile.SharedSecret, timeStamp, _logger);

            _logger.LogDebug("Steam guard got, code: {code}", code);

            if (code == null)
                throw new Exception("Steam guard code is null");

            return code;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception while generating steam guard code: {exception}", e);
            throw;
        }
    }

    public async Task<SdaConfirmation[]?> TryFetchConfirmationAsync(CancellationToken cancellationToken = default) =>
        await TryHelpers.TryAsync(FetchConfirmationAsync(cancellationToken));

    public async Task<SdaConfirmation[]> FetchConfirmationAsync(CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = _logger.CreateScopeForMethod(this);

        string url = SdaConfirmationsLogic.GenerateConfirmationUrl(
            await SteamTime.GetCurrentSteamTimeAsync(cancellationToken),
            MaFile.DeviceId,
            MaFile.IdentitySecret,
            MaFile.Session.SteamId,
            "conf",
            _logger);

        _logger.LogDebug("Created url: {url}", url);

        CookieContainer cookies = MaFile.Session.CreateCookies();

        _logger.LogDebug("Created cookies: {cookies}", cookies.ToJson());

        RestResponse response;

        try
        {
            response = await RestClient.ExecuteGetRequestAsync(url, cookies, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception while executing request, exception: {exception}", e.ToJson());
            throw new RequestException("Exception while executing request", null, null, e);
        }

        _logger.LogRestResponse(response);

        if (!response.IsSuccessful)
            throw new RequestException("Response is not successful", response.StatusCode, response.Content, null);

        if (response.RawBytes == null)
            throw new RequestException("RawBytes is null", response.StatusCode, null, null);

        string content = await GZipDecoding.DecodeGZipAsync(response.RawBytes, _logger, cancellationToken);

        try
        {
            SdaConfirmationsResponse? confirmationsResponse = JsonConvert.DeserializeObject<SdaConfirmationsResponse>(content) ??
                throw new RequestException("Response parse result is null or not success", response.StatusCode, content, null);
            if (!confirmationsResponse.Success)
            {
                if (confirmationsResponse.IsNeedAuth)
                    throw new RequestException("Response result is unauthorized", HttpStatusCode.Unauthorized, content,
                        null);

                throw new RequestException("Response parse result is not success", response.StatusCode, content,
                    null);
            }

            if (confirmationsResponse.Confirmations == null)
                throw new RequestException("Confirmations not found", response.StatusCode, content, null);

            return confirmationsResponse.Confirmations;
        }
        catch (Exception e)
        {
            _logger.LogError("Error parse confirmations, exception: {exception}", e.ToJson());
            throw;
        }
    }

    #region AcceptDenyConfirmations

    public async Task<bool> TryAcceptConfirmationAsync(SdaConfirmation confirmation,
        CancellationToken cancellationToken = default) =>
        await TryHelpers.TryAsync(AcceptConfirmationAsync(confirmation, cancellationToken));

    public async Task AcceptConfirmationAsync(SdaConfirmation confirmation,
        CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = _logger.CreateScopeForMethod(this);

        await ProcessConfirmationAsync(confirmation, "allow",
            await SteamTime.GetCurrentSteamTimeAsync(cancellationToken), cancellationToken);
    }

    public async Task<bool> TryAcceptConfirmationsAsync(SdaConfirmation[] confirmations,
        CancellationToken cancellationToken = default) =>
        await TryHelpers.TryAsync(AcceptConfirmationsAsync(confirmations, cancellationToken));

    public async Task AcceptConfirmationsAsync(SdaConfirmation[] confirmations,
        CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = _logger.CreateScopeForMethod(this);

        await ProcessConfirmationsAsync(confirmations, "allow",
            await SteamTime.GetCurrentSteamTimeAsync(cancellationToken), cancellationToken);
    }

    public async Task<bool> TryDenyConfirmationAsync(SdaConfirmation confirmation,
        CancellationToken cancellationToken = default) =>
        await TryHelpers.TryAsync(DenyConfirmationAsync(confirmation, cancellationToken));

    public async Task DenyConfirmationAsync(SdaConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = _logger.CreateScopeForMethod(this);

        await ProcessConfirmationAsync(confirmation, "cancel",
            await SteamTime.GetCurrentSteamTimeAsync(cancellationToken),
            cancellationToken);
    }

    public async Task<bool> TryDenyConfirmationsAsync(SdaConfirmation[] confirmations,
        CancellationToken cancellationToken = default) =>
        await TryHelpers.TryAsync(DenyConfirmationsAsync(confirmations, cancellationToken));

    public async Task DenyConfirmationsAsync(SdaConfirmation[] confirmations,
        CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = _logger.CreateScopeForMethod(this);

        await ProcessConfirmationsAsync(confirmations, "cancel",
            await SteamTime.GetCurrentSteamTimeAsync(cancellationToken), cancellationToken);
    }

    private async Task ProcessConfirmationsAsync(SdaConfirmation[] confirmations, string tag, long timestamp,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Start processing confirmations, count: {confirmationsCount}, tag: {tag}, timestamp: {timeStamp}",
            confirmations.Length, tag, timestamp);

        string url = Endpoints.SteamCommunityUrl + "/mobileconf/multiajaxop";

        StringBuilder queryString = new();

        queryString.Append("op=" + tag + "&");

        queryString.Append(SdaConfirmationsLogic.GenerateConfirmationQueryParams(tag, MaFile.DeviceId,
            MaFile.IdentitySecret,
            MaFile.Session.SteamId,
            timestamp,
            _logger));

        foreach (SdaConfirmation confirmation in confirmations)
            queryString.Append("&cid[]=" + confirmation.Id + "&ck[]=" + confirmation.Key);

        _logger.LogDebug("Result url: {url}", url);

        CookieContainer cookies = MaFile.Session.CreateCookies();

        _logger.LogDebug("Cookies: {cookies}", cookies.ToJson());

        RestResponse response;

        try
        {
            response = await RestClient.ExecutePostRequestAsync(url, cookies, null, queryString.ToString(),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError("Error while executing request, exception: {exception}", e.ToJson());
            throw new RequestException("Exception while executing request", null, null, e);
        }

        _logger.LogRestResponse(response);

        if (!response.IsSuccessful)
            throw new RequestException("Response is not successful", response.StatusCode, response.Content, null);

        if (response.RawBytes == null)
            throw new RequestException("RawBytes is null", response.StatusCode, null, null);

        string content = await GZipDecoding.DecodeGZipAsync(response.RawBytes, _logger, cancellationToken);

        try
        {
            ProcessConfirmationResponse? processConfirmationResponse = JsonConvert.DeserializeObject<ProcessConfirmationResponse>(content) ??
                throw new RequestException("Response parse result is null", response.StatusCode, content, null);
            if (!processConfirmationResponse.Success)
                throw new Exception("ConfirmationResponse is false");
        }
        catch (Exception e)
        {
            _logger.LogError("Error while deserializing content, content: {content}, exception: {exception}", content,
                e.ToJson());
            throw;
        }
    }

    private async Task ProcessConfirmationAsync(
        SdaConfirmation confirmation,
        string tag,
        long timestamp,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Start processing confirmation, id: {confirmationId}, tag: {tag}, timestamp: {timeStamp}",
            confirmation.Id, tag, timestamp);

        string url = Endpoints.SteamCommunityUrl + "/mobileconf/ajaxop";

        string queryString = "?op=" + tag + "&";

        queryString += SdaConfirmationsLogic.GenerateConfirmationQueryParams(tag, MaFile.DeviceId,
            MaFile.IdentitySecret,
            MaFile.Session.SteamId,
            timestamp,
            _logger);

        queryString += "&cid=" + confirmation.Id + "&ck=" + confirmation.Key;

        url += queryString;

        _logger.LogDebug("Result url: {url}", url);

        CookieContainer cookies = MaFile.Session.CreateCookies();

        _logger.LogDebug("Cookies: {cookies}", cookies.ToJson());

        RestResponse response;

        try
        {
            response = await RestClient.ExecuteGetRequestAsync(url, cookies, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError("Error while executing request, exception: {exception}", e.ToJson());
            throw new RequestException("Exception while executing request", null, null, e);
        }

        _logger.LogRestResponse(response);

        if (!response.IsSuccessful)
            throw new RequestException("Response is not successful", response.StatusCode, response.Content, null);

        if (response.RawBytes == null)
            throw new RequestException("RawBytes is null", response.StatusCode, null, null);

        string content = await GZipDecoding.DecodeGZipAsync(response.RawBytes, _logger, cancellationToken);

        try
        {
            ProcessConfirmationResponse? processConfirmationResponse = JsonConvert.DeserializeObject<ProcessConfirmationResponse>(content) ??
                throw new RequestException("Response parse result is null", response.StatusCode, content, null);
            if (!processConfirmationResponse.Success)
                throw new Exception("ConfirmationResponse is false");
        }
        catch (Exception e)
        {
            _logger.LogError("Error while deserializing content, content: {content}, exception: {exception}", content,
                e.ToJson());
            throw;
        }
    }

    #endregion

    public async Task<string?> TryLoginAgainAsync(string username, string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoginAgainAsync(username, password, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    public async Task<string?> LoginAgainAsync(string username, string password,
        CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = _logger.CreateScopeForMethod(this);

        SteamConfiguration configuration = SteamConfiguration.Create(builder => builder.WithHttpClientFactory(
                () =>
                {
                    HttpClientHandler httpClientHandler = new() { Proxy = RestClient.Proxy };
                    HttpClient client = new(httpClientHandler);
                    return client;
                })
            .WithProtocolTypes(ProtocolTypes.WebSocket));


        SteamClient steamClient = new(configuration);
        CallbackManager manager = new(steamClient);
        TaskCompletionSource tks = new();

        CancellationTokenSource cts = new(TimeSpan.FromMinutes(1));
        CancellationToken ct = cts.Token;

        manager.Subscribe<SteamClient.ConnectedCallback>(_ =>
        {
            cts.Cancel();
            tks.SetResult();
        });

        SteamUser steamUser = steamClient.GetHandler<SteamUser>()!;

        Task connectTask = tks.Task;

        steamClient.ConnectWithProxy(null, RestClient.Proxy);

        try
        {
            Task __ = Task.Run(() =>
            {
                while (true)
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));

                    if (!ct.IsCancellationRequested)
                        continue;

                    tks.TrySetException(new OperationCanceledException());
                    return;
                }

                // ReSharper disable once FunctionNeverReturns
            }, ct);

            await connectTask;

            CredentialsAuthSession authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = username,
                    Password = password,
                    IsPersistentSession = true,
                    Authenticator = new SteamGuardAuthenticator(this),
                });

            AuthPollResult pollResponse = await authSession.PollingWaitForResultAsync(cancellationToken);

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken,
            });

            ulong steamId = authSession.SteamID.ConvertToUInt64();
            string steamLoginSecure = steamId + "%7C%7C" + pollResponse.AccessToken;

            SteamSessionData newSession = new(steamClient.ID, steamLoginSecure, steamId);

            SteamMaFile newMaFile = new(
                MaFile.SharedSecret,
                MaFile.SerialNumber,
                MaFile.RevocationCode,
                MaFile.Uri,
                MaFile.ServerTime,
                MaFile.AccountName,
                MaFile.TokenGuid,
                MaFile.IdentitySecret,
                MaFile.Secret1,
                MaFile.Status,
                MaFile.DeviceId,
                MaFile.FullyEnrolled,
                newSession);

            MaFile = newMaFile;

            _logger.LogDebug("MaFile data rewrited");

            return null;
        }
        finally
        {
            steamClient.Disconnect();
        }
    }

    public override string ToString() => $"SteamGuardAccount, steamId: {MaFile.Session.SteamId}";

    public static SteamGuardAccount Create(string maFileContent, SteamRestClient steamRestClient,
        ISteamTime steamTime, ILogger<SteamGuardAccount> logger)
    {
        SteamMaFile? steamMaFile = JsonConvert.DeserializeObject<SteamMaFile>(maFileContent) ??
            throw new DeserializeException("Error deserialize SteamMaFile, result is null");
        return new SteamGuardAccount(steamMaFile, steamRestClient, steamTime, logger);
    }

    public static async Task<SteamGuardAccount> CreateAsync(string maFilePath, SteamRestClient steamRestClient,
        ISteamTime steamTime, ILogger<SteamGuardAccount> logger, CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(maFilePath, cancellationToken);

        return Create(json, steamRestClient, steamTime, logger);
    }
}