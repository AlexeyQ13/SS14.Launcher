using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class ServerEntryViewModel : ObservableRecipient, IRecipient<FavoritesChanged>, IViewModelBase
{
    private readonly ServerStatusData _cacheData;
    private readonly IServerSource _serverSource;
    private readonly DataManager _cfg;
    private readonly MainWindowViewModel _windowVm;
    private string Address => _cacheData.Address;
    private string _fallbackName = string.Empty;
    private bool _isExpanded;

    public ServerEntryViewModel(MainWindowViewModel windowVm, ServerStatusData cacheData, IServerSource serverSource)
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _windowVm = windowVm;
        _cacheData = cacheData;
        _serverSource = serverSource;

        _cacheData.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(IServerStatusData.PlayerCount):
                case nameof(IServerStatusData.SoftMaxPlayerCount):
                    OnPropertyChanged(nameof(ServerStatusString));
                    break;

                case nameof(IServerStatusData.Status):
                    OnPropertyChanged(nameof(IsOnline));
                    OnPropertyChanged(nameof(ServerStatusString));
                    OnPropertyChanged(nameof(Description));
                    CheckUpdateInfo();
                    break;

                case nameof(IServerStatusData.Name):
                    OnPropertyChanged(nameof(Name));
                    break;

                case nameof(IServerStatusData.Description):
                case nameof(IServerStatusData.StatusInfo):
                    OnPropertyChanged(nameof(Description));
                    break;
            }
        };
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusData cacheData,
        FavoriteServer favorite,
        IServerSource serverSource)
        : this(windowVm, cacheData, serverSource)
    {
        Favorite = favorite;
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusDataWithFallbackName ssdfb,
        IServerSource serverSource)
        : this(windowVm, ssdfb.Data, serverSource)
    {
        FallbackName = ssdfb.FallbackName ?? "";
    }

    public void ConnectPressed()
    {
        ConnectingViewModel.StartConnect(_windowVm, Address);
    }

    public FavoriteServer? Favorite { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            CheckUpdateInfo();
        }
    }

    public string Name => Favorite?.Name ?? _cacheData.Name ?? _fallbackName;
    public string FavoriteButtonText => IsFavorite ? "Remove Favorite" : "Add Favorite";
    private bool IsFavorite => _cfg.FavoriteServers.Lookup(Address).HasValue;

    public bool ViewedInFavoritesPane { get; set; }

    public string ServerStatusString
    {
        get
        {
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    return "ОФФЛАЙН";
                case ServerStatusCode.Online:
                    // Give a ratio for servers with a defined player count, or just a current number for those without.
                    if (_cacheData.SoftMaxPlayerCount > 0)
                    {
                        return $"{_cacheData.PlayerCount} / {_cacheData.SoftMaxPlayerCount}";
                    }
                    else
                    {
                        return $"{_cacheData.PlayerCount} / ∞";
                    }
                case ServerStatusCode.FetchingStatus:
                    return "Загрузка...";
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public string Description
    {
        get
        {
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    return "Не удается связаться с сервером";
                case ServerStatusCode.FetchingStatus:
                    return "Получение сведений сервера...";
            }

            return _cacheData.StatusInfo switch
            {
                ServerStatusInfoCode.NotFetched => "Получение описания сервера...",
                ServerStatusInfoCode.Fetching => "Получение описания сервера...",
                ServerStatusInfoCode.Error => "Ошибка при получении описания сервера",
                ServerStatusInfoCode.Fetched => _cacheData.Description ?? "Описание сервера не указано",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public bool IsOnline => _cacheData.Status == ServerStatusCode.Online;

    public string FallbackName
    {
        get => _fallbackName;
        set
        {
            SetProperty(ref _fallbackName, value);
            OnPropertyChanged(nameof(Name));
        }
    }

    public ServerStatusData CacheData => _cacheData;

    public void FavoriteButtonPressed()
    {
        if (IsFavorite)
        {
            // Remove favorite.
            _cfg.RemoveFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value);
        }
        else
        {
            var fav = new FavoriteServer(_cacheData.Name ?? FallbackName, Address);
            _cfg.AddFavoriteServer(fav);
        }

        _cfg.CommitConfig();
    }

    public void FavoriteRaiseButtonPressed()
    {
        if (IsFavorite)
        {
            // Usual business, raise priority
            _cfg.RaiseFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value);
        }

        _cfg.CommitConfig();
    }

    public void Receive(FavoritesChanged message)
    {
        OnPropertyChanged(nameof(FavoriteButtonText));
    }

    private void CheckUpdateInfo()
    {
        if (!IsExpanded || _cacheData.Status != ServerStatusCode.Online)
            return;

        if (_cacheData.StatusInfo is not (ServerStatusInfoCode.NotFetched or ServerStatusInfoCode.Error))
            return;

        _serverSource.UpdateInfoFor(_cacheData);
    }
}
