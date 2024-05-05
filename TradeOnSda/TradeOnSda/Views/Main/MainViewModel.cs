using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using DynamicData.Binding;

using Newtonsoft.Json;

using ReactiveUI;

using SteamAuthentication.Exceptions;
using SteamAuthentication.Models;

using TradeOnSda.Data;
using TradeOnSda.ViewModels;
using TradeOnSda.Views.Account;
using TradeOnSda.Views.AccountList;
using TradeOnSda.Windows.AddGuard;
using TradeOnSda.Windows.ImportAccounts;
using TradeOnSda.Windows.NotificationMessage;

namespace TradeOnSda.Views.Main;

public class MainViewModel : ViewModelBase
{
    private string _steamGuardToken = null!;
    private string _searchText = null!;

    public string SteamGuardToken
    {
        get => _steamGuardToken;
        set => RaiseAndSetIfPropertyChanged(ref _steamGuardToken, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => RaiseAndSetIfPropertyChanged(ref _searchText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => RaiseAndSetIfPropertyChanged(ref _progressValue, value);
    }

    public AccountListViewModel AccountListViewModel { get; }

    public ICommand ImportAccountsCommand { get; }

    public ICommand ReLoginCommand { get; }

    public ICommand CopySdaCodeCommand { get; }

    public ICommand AddGuardCommand { get; }

    public SdaManager SdaManager { get; }

    public string VersionString { get; }

    public ICommand AboutUsCommand { get; }

    public ICommand VersionCommand { get; }

    public bool IsAccountSelected
    {
        get => _isAccountSelected;
        set => RaiseAndSetIfPropertyChanged(ref _isAccountSelected, value);
    }

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly Window _ownerWindow;

    private CancellationTokenSource? _currentSdaCodeCts;
    private double _progressValue;
    private bool _isAccountSelected;
    private bool _isReLoginSuccess;

    public bool IsReLoginSuccess
    {
        get => _isReLoginSuccess;
        set => RaiseAndSetIfPropertyChanged(ref _isReLoginSuccess, value);
    }

    public MainViewModel(Window ownerWindow, SdaManager sdaManager)
    {
        _ownerWindow = ownerWindow;
        SteamGuardToken = "-----";
        SearchText = string.Empty;
        ProgressValue = 0d;
        SdaManager = sdaManager;
        VersionString = GetUserFriendlyApplicationVersion();

        AccountListViewModel = new AccountListViewModel(SdaManager, _ownerWindow);

        Observable.Interval(TimeSpan.FromSeconds(0.03))
            .Subscribe(_ =>
            {
                DateTime time = DateTime.UtcNow;
                DateTime date = time.Date;

                double delta = (time - date).TotalMilliseconds / 1000d % 30d;

                double value = 100d - delta / 30d * 100d;

                ProgressValue = value;
            });

        this.WhenPropertyChanged(t => t.SearchText)
            .Subscribe(valueWrapper =>
            {
                string? newSearchText = valueWrapper.Value;

                AccountListViewModel.SearchText = newSearchText ?? "";
            });

        AccountListViewModel
            .WhenPropertyChanged(t => t.SelectedAccountViewModel)
            .Subscribe(valueWrapper =>
                {
                    _currentSdaCodeCts?.Cancel();

                    _currentSdaCodeCts = new CancellationTokenSource();

                    AccountViewModel? newValue = valueWrapper.Value;

                    IsAccountSelected = newValue != null;

                    Task.Run(async () =>
                    {
                        CancellationToken token = _currentSdaCodeCts.Token;

                        while (true)
                        {
                            if (newValue == null)
                            {
                                SteamGuardToken = "-----";
                                return;
                            }

                            string? sdaCode = await newValue.SdaWithCredentials.SteamGuardAccount
                                .TryGenerateSteamGuardCodeForTimeStampAsync(token);

                            SteamGuardToken = sdaCode ?? "-----";

                            token.ThrowIfCancellationRequested();

                            await Task.Delay(1000, token);
                        }
                    });
                }
            );

        ImportAccountsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            System.Collections.Generic.IReadOnlyList<IStorageFile> result = await _ownerWindow.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("MaFile")
                        {
                            Patterns = new[] { "*.mafile" }
                        }
                    },
                    Title = "Select mafiles",
                });

            foreach (IStorageFile file in result)
            {
                try
                {
                    string path = file.Path.LocalPath;
                    string maFileName = file.Name;

                    SteamMaFile steamMaFile = JsonConvert.DeserializeObject<SteamMaFile>(await File.ReadAllTextAsync(path))!;

                    await ImportAccountsWindow.CreateImportAccountWindowAsync(
                        steamMaFile,
                        maFileName,
                        _ownerWindow,
                        SdaManager);
                }
                catch (Exception e)
                {
                    await NotificationsMessageWindow.ShowWindow($"Invalid mafile. Error: {e.Message}", _ownerWindow);
                }
            }
        });

        ReLoginCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            AccountViewModel? selectedAccountViewModel = AccountListViewModel.SelectedAccountViewModel;

            if (selectedAccountViewModel == null)
                return;

            try
            {
                MaFileCredentials credentials = selectedAccountViewModel.SdaWithCredentials.Credentials;
                string? username = selectedAccountViewModel.SdaWithCredentials.SteamGuardAccount.MaFile.AccountName;

                string? result =
                    await selectedAccountViewModel.SdaWithCredentials.SteamGuardAccount.LoginAgainAsync(username,
                        credentials.Password);

                if (result != null)
                {
                    await NotificationsMessageWindow.ShowWindow($"Error login in steam, message: {result}", _ownerWindow);
                    return;
                }

                await SdaManager.SaveMaFile(selectedAccountViewModel.SdaWithCredentials.SteamGuardAccount);

                Task _ = Task.Run(async () =>
                {
                    IsReLoginSuccess = true;
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    IsReLoginSuccess = false;
                });
            }
            catch (RequestException e)
            {
                await NotificationsMessageWindow.ShowWindow(
                    $"Error login in steam, message: {e.Message}, statusCode: {e.HttpStatusCode}", _ownerWindow);
            }
            catch (Exception e)
            {
                await NotificationsMessageWindow.ShowWindow($"Error login in steam, message: {e.Message}", _ownerWindow);
            }
        });

        CopySdaCodeCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (SteamGuardToken == "-----")
                return;

            Task? setTask = _ownerWindow.Clipboard?.SetTextAsync(SteamGuardToken);

            if (setTask != null)
                await setTask;
        });

        AddGuardCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddGuardWindow.ShowWindow(sdaManager, ownerWindow);
        });

        AboutUsCommand = ReactiveCommand.Create(() =>
        {
            Process.Start(new ProcessStartInfo() { FileName = "https://linktr.ee/tradeon", UseShellExecute = true });
        });

        VersionCommand = ReactiveCommand.Create(() =>
        {
            Process.Start(new ProcessStartInfo() { FileName = "https://github.com/TradeOnSolutions/Steam-Desktop-Authenticator/releases", UseShellExecute = true });
        });
    }

    public MainViewModel()
    {
        _ownerWindow = null!;
        SteamGuardToken = "AS2X3";
        SearchText = string.Empty;
        ImportAccountsCommand = null!;
        AccountListViewModel = null!;
        SdaManager = null!;
        ProgressValue = 0d;
        ReLoginCommand = null!;
        CopySdaCodeCommand = null!;
        AddGuardCommand = null!;
        VersionString = null!;
        AboutUsCommand = null!;
        VersionCommand = null!;
    }

    public static string GetUserFriendlyApplicationVersion()
    {
        Version version = Assembly.GetEntryAssembly()!.GetName().Version!;

        int major = version.Major;
        int minor = version.Minor;

        return $"v. {major}.{minor}";
    }
}