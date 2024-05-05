using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using SteamAuthentication.LogicModels;

using TradeOnSda.Data;
using TradeOnSda.ViewModels.Windows.GuardAdded;

namespace TradeOnSda.Views.Windows.GuardAdded;

public partial class GuardAddedWindow : Window
{
    public GuardAddedWindow(SteamGuardAccount steamGuardAccount, MaFileCredentials credentials)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        DataContext = new GuardAddedWindowViewModel(steamGuardAccount, credentials, this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static async Task ShowWindow(SteamGuardAccount steamGuardAccount, MaFileCredentials credentials,
        Window ownerWindow) => await new GuardAddedWindow(steamGuardAccount, credentials).ShowDialog(ownerWindow);
}