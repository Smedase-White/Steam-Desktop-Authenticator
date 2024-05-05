using System;
using System.Threading.Tasks;

using SteamKit2.Authentication;

namespace TradeOnSda.ViewModels.Controls.AddGuardFirstStep;

// ReSharper disable once InconsistentNaming
public class UIAuthenticator : IAuthenticator
{
    private readonly AddGuardViewModel _viewModel;

    public UIAuthenticator(AddGuardViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        string title = previousCodeWasIncorrect ? "Incorrect code. Enter a new 2FA code" : "Enter a 2FA code";

        string result = await _viewModel.AskUserAsync(title);

        if (result == null)
            throw new UserCancelException();

        return result;
    }

    public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
    {
        string title = previousCodeWasIncorrect ? "Incorrect code. Enter a correct e-mail code" : "Enter a e-mail code";

        string result = await _viewModel.AskUserAsync(title);

        if (result == null)
            throw new UserCancelException();

        return result;
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        throw new NotSupportedException();
    }
}

public class UserCancelException : Exception
{
}