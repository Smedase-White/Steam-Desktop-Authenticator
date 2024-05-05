using Avalonia.Controls;

using TradeOnSda.Data;
using TradeOnSda.ViewModels.Controls.AddGuardFirstStep;

namespace TradeOnSda.ViewModels.Windows.AddGuard;

public class AddGuardWindowViewModel : ViewModelBase
{
    public AddGuardViewModel AddGuardViewModel { get; }

    public AddGuardWindowViewModel(Window ownerWindow, SdaManager sdaManager)
    {
        AddGuardViewModel = new AddGuardViewModel(sdaManager, ownerWindow);
    }

    public AddGuardWindowViewModel()
    {
        AddGuardViewModel = null!;
    }
}