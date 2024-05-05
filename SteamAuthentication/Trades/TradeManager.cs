using SteamAuthentication.Trades.Models;

namespace SteamAuthentication.Trades;

public class TradeManager : IDisposable
{
    private readonly SteamAccount _tradeSteamAccount;

    public TimeSpan UpdateInterval { get; }

    private readonly TradeObservable _observable;

    private PeriodicTimer? _timer;

    private CancellationTokenSource? _cancellationTokenSource;

    internal TradeManager(SteamAccount tradeSteamAccount, TimeSpan updateInterval)
    {
        _tradeSteamAccount = tradeSteamAccount;
        UpdateInterval = updateInterval;
        _observable = new TradeObservable();
    }

    public void Start()
    {
        if (_cancellationTokenSource != null)
            throw new InvalidOperationException("TradeManager is already started");

        _cancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = _cancellationTokenSource.Token;

        _timer = new PeriodicTimer(UpdateInterval);

        Task.Run(async () =>
        {
            while (true)
            {
                await _timer.WaitForNextTickAsync(cancellationToken);

                try
                {
                    (Responses.Offer[] sentOffers, Responses.Offer[] receivedOffers) =
                        await _tradeSteamAccount.GetSentAndReceivedTradeOffersAsync(_tradeSteamAccount.TradesState.StartTimeStamp,
                            cancellationToken);

                    foreach (Responses.Offer offer in sentOffers)
                    {
                        TradeEvent? tradeEvent = _tradeSteamAccount.TradesState.SetOffer(offer);

                        if (tradeEvent != null)
                            _observable.NewEvent(tradeEvent);
                    }

                    foreach (Responses.Offer offer in receivedOffers)
                    {
                        TradeEvent? tradeEvent = _tradeSteamAccount.TradesState.SetOffer(offer);

                        if (tradeEvent != null)
                            _observable.NewEvent(tradeEvent);
                    }

                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // ignored
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            // ReSharper disable once FunctionNeverReturns
        }, cancellationToken);
    }

    public IObservable<TradeEvent> GetObservable() => _observable;

    public void Dispose()
    {
        _timer?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}