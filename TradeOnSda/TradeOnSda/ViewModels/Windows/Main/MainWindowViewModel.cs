﻿using System;

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using TradeOnSda.Data;
using TradeOnSda.ViewModels.Controls.Main;

namespace TradeOnSda.ViewModels.Windows.Main;

public class MainWindowViewModel : ViewModelBase
{
    public MainViewModel MainViewModel { get; }

    public IImage Logo { get; }

    public bool IsMac { get; }

    public MainWindowViewModel(Window ownerWindow, SdaManager sdaManager)
    {
        MainViewModel = new MainViewModel(ownerWindow, sdaManager);

        System.IO.Stream logoSteam = AssetLoader.Open(new Uri("avares://TradeOnSda/Assets/logo.png"));
        Logo = new Bitmap(logoSteam);

        IsMac = OperatingSystem.IsMacOS();
    }

    public MainWindowViewModel()
    {
        Logo = null!;
        MainViewModel = new MainViewModel();
    }
}