using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
#if LPP_Sponsors  // _LostParadise-Sponsors
using Content.Server._LostParadise.Sponsors;
#endif


namespace Content.Server.Preferences.Managers
{
    /// <summary>
    /// Sends <see cref="MsgPreferencesAndSettings"/> before the client joins the lobby.
    /// Receives <see cref="MsgSelectCharacter"/> and <see cref="MsgUpdateCharacter"/> at any time.
    /// </summary>
    public sealed class ServerPreferencesManager : IServerPreferencesManager
    {
        [Dependency] private readonly IServerNetManager _netManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly ILogManager _log = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;
#if LPP_Sponsors  // _LostParadise-Sponsors
        [Dependency] private readonly SponsorsManager _sponsors = default!;
#endif

        // Cache player prefs on the server so we don't need as much async hell related to them.
        private readonly Dictionary<NetUserId, PlayerPrefData> _cachedPlayerPrefs =
            new();

        private ISawmill _sawmill = default!;

        private int MaxCharacterSlots => _cfg.GetCVar(CCVars.GameMaxCharacterSlots);

        public void Init()
        {
            _netManager.RegisterNetMessage<MsgPreferencesAndSettings>();
            _netManager.RegisterNetMessage<MsgSelectCharacter>(HandleSelectCharacterMessage);
            _netManager.RegisterNetMessage<MsgUpdateCharacter>(HandleUpdateCharacterMessage);
            _netManager.RegisterNetMessage<MsgDeleteCharacter>(HandleDeleteCharacterMessage);
            _sawmill = _log.GetSawmill("prefs");
        }

        private async void HandleSelectCharacterMessage(MsgSelectCharacter message)
        {
            var index = message.SelectedCharacterIndex;
            var userId = message.MsgChannel.UserId;

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (index < 0 || index >=
#if LPP_Sponsors  // _LostParadise-Sponsors
                GetMaxUserCharacterSlots(userId)
#else
                MaxCharacterSlots
#endif
                )
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            if (!curPrefs.Characters.ContainsKey(index))
            {
                // Non-existent slot.
                return;
            }

            prefsData.Prefs = new PlayerPreferences(curPrefs.Characters, index, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                await _db.SaveSelectedCharacterIndexAsync(message.MsgChannel.UserId, message.SelectedCharacterIndex);
            }
        }

        private async void HandleUpdateCharacterMessage(MsgUpdateCharacter message)
        {
            var userId = message.MsgChannel.UserId;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (message.Profile == null)
                _sawmill.Error($"User {userId} sent a {nameof(MsgUpdateCharacter)} with a null profile in slot {message.Slot}.");
            else
                await SetProfile(userId, message.Slot, message.Profile);
        }

        public async Task SetProfile(NetUserId userId, int slot, ICharacterProfile profile)
        {
            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded)
            {
                _sawmill.Error($"Tried to modify user {userId} preferences before they loaded.");
                return;
            }

            if (slot < 0 || slot >=
#if LPP_Sponsors  // _LostParadise-Sponsors
                GetMaxUserCharacterSlots(userId)
#else
                MaxCharacterSlots
#endif
                )
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;
            var session = _playerManager.GetSessionById(userId);
            var collection = IoCManager.Instance!;

#if LPP_Sponsors  // _LostParadise-Sponsors
            var allowedMarkings = _sponsors.TryGetInfo(userId, out var sponsor) ? sponsor.AllowedMarkings : new string[] { };
            profile.EnsureValid(session, collection, allowedMarkings);
#else
            profile.EnsureValid(session, collection);
#endif

            var profiles = new Dictionary<int, ICharacterProfile>(curPrefs.Characters)
            {
                [slot] = profile
            };

            prefsData.Prefs = new PlayerPreferences(profiles, slot, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(session.Channel.AuthType))
                await _db.SaveCharacterSlotAsync(userId, profile, slot);
        }

        private async void HandleDeleteCharacterMessage(MsgDeleteCharacter message)
        {
            var slot = message.Slot;
            var userId = message.MsgChannel.UserId;

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (slot < 0 || slot >=
#if LPP_Sponsors  // _LostParadise-Sponsors
                GetMaxUserCharacterSlots(userId)
#else
                MaxCharacterSlots
#endif
                )
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            // If they try to delete the slot they have selected then we switch to another one.
            // Of course, that's only if they HAVE another slot.
            int? nextSlot = null;
            if (curPrefs.SelectedCharacterIndex == slot)
            {
                // That ! on the end is because Rider doesn't like .NET 5.
                var (ns, profile) = curPrefs.Characters.FirstOrDefault(p => p.Key != message.Slot);
                if (profile == null)
                {
                    // Only slot left, can't delete.
                    return;
                }

                nextSlot = ns;
            }

            var arr = new Dictionary<int, ICharacterProfile>(curPrefs.Characters);
            arr.Remove(slot);

            prefsData.Prefs = new PlayerPreferences(arr, nextSlot ?? curPrefs.SelectedCharacterIndex, curPrefs.AdminOOCColor);

            if (!ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                return;
            }

            if (nextSlot != null)
            {
                await _db.DeleteSlotAndSetSelectedIndex(userId, slot, nextSlot.Value);
            }
            else
            {
                await _db.SaveCharacterSlotAsync(userId, null, slot);
            }
        }

        // Should only be called via UserDbDataManager.
        public async Task LoadData(ICommonSession session, CancellationToken cancel)
        {
            if (!ShouldStorePrefs(session.Channel.AuthType))
            {
                // Don't store data for guests.
                var prefsData = new PlayerPrefData
                {
                    PrefsLoaded = true,
                    Prefs = new PlayerPreferences(
                        new[] { new KeyValuePair<int, ICharacterProfile>(0, HumanoidCharacterProfile.Random()) },
                        0, Color.Transparent)
                };

                _cachedPlayerPrefs[session.UserId] = prefsData;
            }
            else
            {
                var prefsData = new PlayerPrefData();
                var loadTask = LoadPrefs();
                _cachedPlayerPrefs[session.UserId] = prefsData;

                await loadTask;

                async Task LoadPrefs()
                {
                    var prefs = await GetOrCreatePreferencesAsync(session.UserId);
                    prefsData.Prefs = prefs;
                    prefsData.PrefsLoaded = true;

#if LPP_Sponsors
                    var sponsorSlots = GetMaxUserCharacterSlots(session.UserId);
#endif

                    var msg = new MsgPreferencesAndSettings
                    {
                        Preferences = prefs,
                        Settings = new GameSettings
                        {
                            MaxCharacterSlots = MaxCharacterSlots
#if LPP_Sponsors
                            + sponsorSlots
#endif
                        }
                    };
                    _netManager.ServerSendMessage(msg, session.Channel);
                }
            }
        }

        public void OnClientDisconnected(ICommonSession session)
        {
            _cachedPlayerPrefs.Remove(session.UserId);
        }

#if LPP_Sponsors  // _LostParadise-Sponsors
        private int GetMaxUserCharacterSlots(NetUserId userId)
        {
            var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
            var extraSlots = _sponsors.TryGetInfo(userId, out var sponsor) ? sponsor.ExtraSlots : 0;
            return maxSlots + extraSlots;
        }
#endif

        public bool HavePreferencesLoaded(ICommonSession session)
        {
            return _cachedPlayerPrefs.ContainsKey(session.UserId);
        }

        /// <summary>
        /// Tries to get the preferences from the cache
        /// </summary>
        /// <param name="userId">User Id to get preferences for</param>
        /// <param name="playerPreferences">The user preferences if true, otherwise null</param>
        /// <returns>If preferences are not null</returns>
        public bool TryGetCachedPreferences(NetUserId userId,
            [NotNullWhen(true)] out PlayerPreferences? playerPreferences)
        {
            if (_cachedPlayerPrefs.TryGetValue(userId, out var prefs))
            {
                playerPreferences = prefs.Prefs;
                return prefs.Prefs != null;
            }

            playerPreferences = null;
            return false;
        }

        /// <summary>
        /// Retrieves preferences for the given username from storage.
        /// Creates and saves default preferences if they are not found, then returns them.
        /// </summary>
        public PlayerPreferences GetPreferences(NetUserId userId)
        {
            var prefs = _cachedPlayerPrefs[userId].Prefs;
            if (prefs == null)
            {
                throw new InvalidOperationException("Preferences for this player have not loaded yet.");
            }

            return prefs;
        }

        /// <summary>
        /// Retrieves preferences for the given username from storage or returns null.
        /// Creates and saves default preferences if they are not found, then returns them.
        /// </summary>
        public PlayerPreferences? GetPreferencesOrNull(NetUserId? userId)
        {
            if (userId == null)
                return null;

            if (_cachedPlayerPrefs.TryGetValue(userId.Value, out var pref))
                return pref.Prefs;
            return null;
        }

        private async Task<PlayerPreferences> GetOrCreatePreferencesAsync(NetUserId userId)
        {
            var prefs = await _db.GetPlayerPreferencesAsync(userId);
            if (prefs is null)
            {
                return await _db.InitPrefsAsync(userId, HumanoidCharacterProfile.Random());
            }

            var session = _playerManager.GetSessionById(userId);
            var collection = IoCManager.Instance!;

            return SanitizePreferences(session, prefs, collection);
        }

        private PlayerPreferences SanitizePreferences(ICommonSession session, PlayerPreferences prefs,
            IDependencyCollection collection)
        {

#if LPP_Sponsors  // _LostParadise-Sponsors
            var allowedMarkings = _sponsors.TryGetInfo(session.UserId, out var sponsor) ? sponsor.AllowedMarkings : new string[] { };
#endif
            // Clean up preferences in case of changes to the game,
            // such as removed jobs still being selected.
            return new PlayerPreferences(prefs.Characters.Select(p => new KeyValuePair<int, ICharacterProfile>(p.Key,
                    p.Value.Validated(session, collection
#if LPP_Sponsors  // _LostParadise-Sponsors
                    , allowedMarkings
#endif
                        ))), prefs.SelectedCharacterIndex, prefs.AdminOOCColor);
        }

        public IEnumerable<KeyValuePair<NetUserId, ICharacterProfile>> GetSelectedProfilesForPlayers(
            List<NetUserId> usernames)
        {
            return usernames
                .Select(p => (_cachedPlayerPrefs[p].Prefs, p))
                .Where(p => p.Prefs != null)
                .Select(p =>
                {
                    var idx = p.Prefs!.SelectedCharacterIndex;
                    return new KeyValuePair<NetUserId, ICharacterProfile>(p.p, p.Prefs!.GetProfile(idx));
                });
        }

        internal static bool ShouldStorePrefs(LoginType loginType)
        {
            return loginType.HasStaticUserId();
        }

        private sealed class PlayerPrefData
        {
            public bool PrefsLoaded;
            public PlayerPreferences? Prefs;
        }
    }
}
