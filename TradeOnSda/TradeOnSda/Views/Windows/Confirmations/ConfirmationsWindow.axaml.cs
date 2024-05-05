using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using SteamAuthentication.LogicModels;
using SteamAuthentication.Models;

using TradeOnSda.ViewModels.Windows.Confirmations;

namespace TradeOnSda.Views.Windows.Confirmations;

public partial class ConfirmationsWindow : Window
{
    public ConfirmationsWindow(SdaConfirmation[] sdaConfirmations, SteamGuardAccount sda)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        DataContext = new ConfirmationsWindowViewModel(sdaConfirmations, sda, this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}