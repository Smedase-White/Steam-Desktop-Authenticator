using System.Threading;
using System.Threading.Tasks;

using SteamAuthentication.GuardLinking;

namespace TradeOnSda.ViewModels.Controls.AddGuardFirstStep;

// ReSharper disable once InconsistentNaming
public class UIPhoneProvider : IPhoneNumberProvider
{
    private readonly AddGuardViewModel _viewModel;

    public UIPhoneProvider(AddGuardViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public async Task<string> GetPhoneNumberAsync(CancellationToken cancellationToken)
    {
        string phoneNumber = await _viewModel.AskUserAsync("Enter a new phone number for steam account", "+XXXXXXXXXXX");

        _viewModel.LastPhoneNumber = phoneNumber;

        return phoneNumber;
    }
}