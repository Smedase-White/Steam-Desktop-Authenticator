using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TradeOnSda.Windows.NotificationMessage;

public partial class NotificationsMessageWindow : Window
{
    public NotificationsMessageWindow(string message)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        DataContext = NotificationMessageViewModel.CreateViewModel(message);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static async Task ShowWindow(string message, Window owner)
    {
        try
        {
            await new NotificationsMessageWindow(message).ShowDialog(owner);
        }
        catch (Exception)
        {
            // ignored
        }
    }
}