using System.Collections.Concurrent;

using SteamAuthentication.Trades.Models;
using SteamAuthentication.Trades.Responses;

namespace SteamAuthentication.Trades;

public class TradesState
{
    private readonly ConcurrentDictionary<ulong, Offer> _offers;

    internal long StartTimeStamp { get; }

    public IReadOnlyDictionary<ulong, Offer> CurrentOffers => _offers.AsReadOnly();

    public TradesState(long startTimeStamp)
    {
        _offers = new ConcurrentDictionary<ulong, Offer>();

        StartTimeStamp = startTimeStamp;
    }

    internal TradeEvent? SetOffer(Offer offer)
    {
        if (!_offers.TryGetValue(offer.TradeOfferId, out Offer? previousState))
        {
            _offers[offer.TradeOfferId] = offer;

            return new TradeEvent(null, offer);
        }

        if (previousState.TradeOfferState == offer.TradeOfferState)
        {
            _offers[offer.TradeOfferId] = offer;

            return null;
        }

        _offers[offer.TradeOfferId] = offer;

        return new TradeEvent(previousState, offer);
    }
}