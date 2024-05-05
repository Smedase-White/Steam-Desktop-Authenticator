namespace SteamAuthentication.LogicModels;

public class SteamTime : ISteamTime
{
    private readonly TimeDeferenceRestClient _restClient;

    private long? _timeDifference;

    public SteamTime(TimeDeferenceRestClient restClient)
    {
        _restClient = restClient;
    }

    public long GetCurrentClientTime() =>
        (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

    public long GetCurrentSteamTime()
    {
        if (_timeDifference == null)
            throw new Exception("SteamTime is not synchronized");

        return GetCurrentClientTime() + _timeDifference.Value;
    }

    public async Task SynchronizeTimeAsync(CancellationToken cancellationToken = default)
    {
        long steamTime = await _restClient.GetSteamTimeAsync(cancellationToken);

        long clientTime = GetCurrentClientTime();

        _timeDifference = steamTime - clientTime;
    }

    public Task<long> GetCurrentSteamTimeAsync(CancellationToken cancellationToken) => Task.FromResult(GetCurrentSteamTime());
}