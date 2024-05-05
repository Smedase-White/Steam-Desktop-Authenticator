using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using SteamAuthentication.LogicModels;

using TradeOnSda.Data.FileSystemAdapters;

namespace TradeOnSda.Data;

public class SdaManager : ReactiveObservableCollection<SdaWithCredentials>
{
    private const string SettingsFileName = "settings.json";
    private const string GlobalSettingsFileName = "globalSettings.json";

    public GlobalSettings GlobalSettings { get; private set; }

    public FileSystemAdapterProvider FileSystemAdapterProvider { get; }

    public GlobalSteamTime GlobalSteamTime { get; }

    public static async Task<SdaManager> CreateSdaManagerAsync()
    {
        SdaManager sdaManager = new();

        await sdaManager.LoadFromDiskAsync();

        return sdaManager;
    }

    private SdaManager()
    {
        GlobalSteamTime = new GlobalSteamTime(new TimeDeferenceRestClient(null));

        GlobalSettings = new GlobalSettings();

        FileSystemAdapterProvider = new FileSystemAdapterProvider();

        Task.Run(CheckProxiesWorkingLoop);
    }

    private async Task CheckProxiesWorkingLoop()
    {
        while (true)
        {
            try
            {
                SdaWithCredentials[] sdas = Items
                    .Where(t => t.Credentials.Proxy != null)
                    .ToArray();

                System.Collections.Generic.IEnumerable<SdaWithCredentials> withoutProxySdas = Items
                    .Where(t => t.Credentials.Proxy == null);

                foreach (SdaWithCredentials? item in withoutProxySdas)
                    item.SdaState.ProxyState = ProxyState.Unknown;

                foreach (SdaWithCredentials? sda in sdas)
                {
                    try
                    {
                        Debug.Assert(sda.Credentials.Proxy != null, "sda.Credentials.Proxy != null");

                        bool result = await ProxyChecking.CheckProxyAsync(sda.Credentials.Proxy);

                        sda.SdaState.ProxyState = result ? ProxyState.Ok : ProxyState.Error;
                    }
                    catch (Exception)
                    {
                        sda.SdaState.ProxyState = ProxyState.Error;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // ReSharper disable once FunctionNeverReturns
    }

    private async Task LoadFromDiskAsync()
    {
        await LoadSettingsAsync();

        await LoadGlobalSettingsAsync();

        async Task LoadGlobalSettingsAsync()
        {
            try
            {
                if (!FileSystemAdapterProvider.GetAdapter().ExistsFile(GlobalSettingsFileName))
                    return;

                string settingsContent = await FileSystemAdapterProvider.GetAdapter()
                    .ReadFileAsync(GlobalSettingsFileName, CancellationToken.None);

                GlobalSettings globalSettings = JsonConvert.DeserializeObject<GlobalSettings>(settingsContent)
                                     ?? throw new Exception();

                GlobalSettings = globalSettings;
            }
            catch (Exception)
            {
                // ignored
            }
        }

        async Task LoadSettingsAsync()
        {
            try
            {
                if (!FileSystemAdapterProvider.GetAdapter().ExistsFile(SettingsFileName))
                    return;

                string settingsContent = await FileSystemAdapterProvider.GetAdapter()
                    .ReadFileAsync(SettingsFileName, CancellationToken.None);

                SavedSdaDto[] savedSdaDtos = JsonConvert.DeserializeObject<SavedSdaDto[]>(settingsContent)
                                   ?? throw new Exception();

                foreach (SavedSdaDto dto in savedSdaDtos)
                {
                    try
                    {
                        SdaWithCredentials sdaWithCredentials = await SdaWithCredentials.FromDto(dto, this);

                        _items.Add(sdaWithCredentials);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public async Task AddAccountAsync(SteamGuardAccount steamGuardAccount, MaFileCredentials maFileCredentials,
        SdaSettings sdaSettings)
    {
        _items.Add(new SdaWithCredentials(steamGuardAccount, maFileCredentials, sdaSettings, this));

        await SaveMaFile(steamGuardAccount);

        await SaveSettingsAsync();
    }

    public async Task RemoveAccountAsync(SdaWithCredentials sdaWithCredentials)
    {
        _items.Remove(sdaWithCredentials);

        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        System.Collections.Generic.IEnumerable<SavedSdaDto> settings = _items.Select(t =>
        {
            Debug.Assert(t != null, nameof(t) + " != null");
            return t.ToDto();
        });

        await FileSystemAdapterProvider.GetAdapter().WriteFileAsync(SettingsFileName,
            JsonConvert.SerializeObject(settings), CancellationToken.None);
    }

    public async Task SaveGlobalSettingsAsync()
    {
        string globalSettings = JsonConvert.SerializeObject(GlobalSettings);

        await FileSystemAdapterProvider.GetAdapter()
            .WriteFileAsync(GlobalSettingsFileName, globalSettings, CancellationToken.None);
    }

    public async Task SaveMaFile(SteamGuardAccount sda)
    {
        string maFileContent = sda.MaFile.ConvertToJson();

        string maFilePath = Path.Combine("MaFiles",
            $"{sda.MaFile.Session?.SteamId}.maFile");

        await FileSystemAdapterProvider.GetAdapter().WriteFileAsync(maFilePath, maFileContent, CancellationToken.None);
    }
}