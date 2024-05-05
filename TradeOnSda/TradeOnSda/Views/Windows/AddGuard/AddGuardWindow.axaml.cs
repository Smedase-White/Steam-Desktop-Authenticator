using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using TradeOnSda.Data;
using TradeOnSda.ViewModels.Windows.AddGuard;

namespace TradeOnSda.Views.Windows.AddGuard;

public partial class AddGuardWindow : Window
{
    public AddGuardWindow(SdaManager sdaManager)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        AddGuardWindowViewModel dataContext = new AddGuardWindowViewModel(this, sdaManager);
        DataContext = dataContext;

        Closing += (_, _) =>
        {
            dataContext.AddGuardViewModel.WindowClose();
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static async Task ShowWindow(SdaManager sdaManager, Window owner) =>
        await new AddGuardWindow(sdaManager).ShowDialog(owner);
}