using TradeOnSda.ViewModels;
using TradeOnSda.ViewModels.Controls.ImportAccounts;

namespace TradeOnSda.ViewModels.Windows.ImportAccounts;

public class ImportAccountsWindowViewModel : ViewModelBase
{
    public ImportAccountsViewModel ImportAccountsViewModel { get; }

    public ImportAccountsWindowViewModel(ImportAccountsViewModel importAccountsViewModel)
    {
        ImportAccountsViewModel = importAccountsViewModel;
    }

    public ImportAccountsWindowViewModel() => ImportAccountsViewModel = new ImportAccountsViewModel();
}