using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;

using AsyncImageLoader;

using Avalonia.Controls;
using Avalonia.Media;

using Humanizer;

using ReactiveUI;

using SteamAuthentication.Exceptions;
using SteamAuthentication.Logic;
using SteamAuthentication.LogicModels;
using SteamAuthentication.Models;

using TradeOnSda.ViewModels.Controls.Confirmations;
using TradeOnSda.Views.Windows.NotificationMessage;

namespace TradeOnSda.ViewModels.Controls.ConfirmationItem;

public class ConfirmationItemViewModel : ViewModelBase
{
    private IImage? _userImage;

    public SteamGuardAccount SteamGuardAccount { get; }

    public SdaConfirmation SdaConfirmation { get; }

    public string ConfirmationTypeString { get; }

    public string ConfirmationTime { get; }

    public IImage? UserImage
    {
        get => _userImage;
        set => RaiseAndSetIfPropertyChanged(ref _userImage, value);
    }

    public ConfirmationItemViewModel(SteamGuardAccount steamGuardAccount, SdaConfirmation sdaConfirmation,
        Window ownerWindow, ConfirmationsViewModel confirmationsViewModel)
    {
        SteamGuardAccount = steamGuardAccount;
        SdaConfirmation = sdaConfirmation;

        ConfirmationTypeString = sdaConfirmation.ConfirmationType switch
        {
            ConfirmationType.Trade => "Trade",
            ConfirmationType.MarketSellTransaction => "Market",
            ConfirmationType.Recovery => "Account",
            _ => "",
        };

        DateTime creationTime = TimeHelpers.FromTimeStamp(sdaConfirmation.CreationTimeStamp);
        TimeSpan delta = DateTime.UtcNow - creationTime;

        string humanizedTime = delta.Humanize(2, new CultureInfo("en-US"));
        ConfirmationTime = humanizedTime + " ago";

        Task.Run(async () =>
        {
            if (sdaConfirmation.Icon != null)
            {
                Avalonia.Media.Imaging.Bitmap? bitmap = await ImageLoader.AsyncImageLoader.ProvideImageAsync(sdaConfirmation.Icon);
                UserImage = bitmap;
            }
        });

        AcceptCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                await SteamGuardAccount.AcceptConfirmationAsync(SdaConfirmation);

                confirmationsViewModel.RemoveViewModel(this);
            }
            catch (RequestException e)
            {
                await NotificationsMessageWindow.ShowWindow(
                    $"{e.Message}, statusCode: {e.HttpStatusCode}, Content: {e.Content}", ownerWindow);
            }
            catch (Exception e)
            {
                await NotificationsMessageWindow.ShowWindow(e.Message, ownerWindow);
            }
        });

        DeclineCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                await SteamGuardAccount.DenyConfirmationAsync(SdaConfirmation);

                confirmationsViewModel.RemoveViewModel(this);
            }
            catch (RequestException e)
            {
                await NotificationsMessageWindow.ShowWindow(
                    $"{e.Message}, statusCode: {e.HttpStatusCode}, Content: {e.Content}", ownerWindow);
            }
            catch (Exception e)
            {
                await NotificationsMessageWindow.ShowWindow(e.Message, ownerWindow);
            }
        });
    }

    public ICommand AcceptCommand { get; }

    public ICommand DeclineCommand { get; }

    public ConfirmationItemViewModel()
    {
        SdaConfirmation = null!;
        ConfirmationTypeString = null!;
        AcceptCommand = null!;
        DeclineCommand = null!;
        SteamGuardAccount = null!;
        ConfirmationTime = null!;
    }
}