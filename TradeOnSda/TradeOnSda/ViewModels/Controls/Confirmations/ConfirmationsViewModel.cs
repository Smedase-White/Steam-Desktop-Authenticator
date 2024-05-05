using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

using Avalonia.Controls;

using ReactiveUI;

using SteamAuthentication.Exceptions;
using SteamAuthentication.LogicModels;
using SteamAuthentication.Models;

using TradeOnSda.ViewModels.Controls.ConfirmationItem;
using TradeOnSda.Views.Windows.NotificationMessage;

namespace TradeOnSda.ViewModels.Controls.Confirmations;

public class ConfirmationsViewModel : ViewModelBase
{
    private bool _isNoConfirmations;
    public SdaConfirmation[] SdaConfirmations { get; private set; }

    public ObservableCollection<ConfirmationItemViewModel> ConfirmationsViewModels { get; }

    public ICommand AcceptAllCommand { get; }

    public ICommand DenyAllCommand { get; }

    public bool IsNoConfirmations
    {
        get => _isNoConfirmations;
        set => RaiseAndSetIfPropertyChanged(ref _isNoConfirmations, value);
    }

    public ICommand RefreshConfirmationsCommand { get; }

    public ConfirmationsViewModel(SdaConfirmation[] sdaConfirmations, SteamGuardAccount steamGuardAccount,
        Window ownerWindow)
    {
        SdaConfirmations = sdaConfirmations;

        IsNoConfirmations = sdaConfirmations.Length == 0;

        ConfirmationsViewModels = new ObservableCollection<ConfirmationItemViewModel>(sdaConfirmations
            .Select(t => new ConfirmationItemViewModel(steamGuardAccount, t, ownerWindow, this)));

        RefreshConfirmationsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                SdaConfirmation[] newConfirmations = (await steamGuardAccount.FetchConfirmationAsync())
                    .Where(t => t.ConfirmationType is ConfirmationType.Trade or ConfirmationType.MarketSellTransaction)
                    .ToArray();

                SdaConfirmations = newConfirmations;

                ConfirmationsViewModels.Clear();

                foreach (SdaConfirmation? confirmation in newConfirmations)
                    ConfirmationsViewModels.Add(new ConfirmationItemViewModel(steamGuardAccount, confirmation,
                        ownerWindow, this));

                IsNoConfirmations = newConfirmations.Length == 0;
            }
            catch (RequestException e)
            {
                await NotificationsMessageWindow.ShowWindow(
                    $"Error load confirmations, message: {e.Message}, httpStatusCode: {e.HttpStatusCode}",
                    ownerWindow);
            }
            catch (Exception e)
            {
                await NotificationsMessageWindow.ShowWindow($"Error load confirmations, message: {e.Message}",
                    ownerWindow);
            }
        });

        AcceptAllCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                await steamGuardAccount.AcceptConfirmationsAsync(SdaConfirmations);

                SdaConfirmations = [];

                ConfirmationsViewModels.Clear();
            }
            catch (RequestException e)
            {
                await NotificationsMessageWindow.ShowWindow(
                    $"Error accept confirmations, message: {e.Message}, httpStatusCode: {e.HttpStatusCode}",
                    ownerWindow);
            }
            catch (Exception e)
            {
                await NotificationsMessageWindow.ShowWindow($"Error accept confirmations, message: {e.Message}",
                    ownerWindow);
            }
        });

        DenyAllCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                await steamGuardAccount.DenyConfirmationsAsync(SdaConfirmations);

                SdaConfirmations = [];

                ConfirmationsViewModels.Clear();
            }
            catch (RequestException e)
            {
                await NotificationsMessageWindow.ShowWindow(
                    $"Error deny confirmations, message: {e.Message}, httpStatusCode: {e.HttpStatusCode}",
                    ownerWindow);
            }
            catch (Exception e)
            {
                await NotificationsMessageWindow.ShowWindow($"Error deny confirmations, message: {e.Message}",
                    ownerWindow);
            }
        });
    }

    public void RemoveViewModel(ConfirmationItemViewModel viewModel)
    {
        ConfirmationsViewModels.Remove(viewModel);
    }

    public ConfirmationsViewModel()
    {
        SdaConfirmations = null!;
        ConfirmationsViewModels = null!;
        RefreshConfirmationsCommand = null!;
        AcceptAllCommand = null!;
        DenyAllCommand = null!;
    }
}