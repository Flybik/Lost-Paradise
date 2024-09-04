using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Client.Language.Systems;
using Content.Client.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Ghost;


namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly IClientAdminManager _adminMgr = default!;

    private ISawmill _sawmill = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private static readonly ResPath Prefix = ResPath.Root / "TTS";

    /// <summary>
    /// Reducing the volume of the TTS when whispering. Will be converted to logarithm.
    /// </summary>
    private const float WhisperFade = 4f;

    /// <summary>
    /// The volume at which the TTS sound will not be heard.
    /// </summary>
    private const float MinimalVolume = -10f;

    private float _volume = 0f;
    private int _fileIdx = 0;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        _resourceCache.AddRoot(Prefix, _contentRoot);
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged);
        _contentRoot.Dispose();
    }

    public void RequestGlobalTTS(string text, string voiceId)
    {
        RaiseNetworkEvent(new RequestGlobalTTSEvent(text, voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        var canPlay = false;
#if LPP_TTS_play   //это предназначено для того, чтобы в случае отсутствия ссылки на ТТС, игра не пыталась выполнить обработку звука
        canPlay = true;
#endif
        if (!canPlay)
            return;
        _sawmill.Debug($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        var volume = AdjustVolume(ev.IsWhisper);

        var filePath = new ResPath($"{_fileIdx++}.ogg");
        //_contentRoot.AddOrUpdateFile(filePath, ev.Data);
        // Languages TTS support start
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player != null)
        {
            var isadmin = _adminMgr.HasFlag(AdminFlags.Admin) && _entities.TryGetComponent<GhostComponent>(player, out var ghostcomp);

            if ((_language.UnderstoodLanguages.Contains(ev.LanguageProtoId) || isadmin) && ev.LanguageProtoId != "Sign")
                _contentRoot.AddOrUpdateFile(filePath, ev.Data);
            else
                _contentRoot.AddOrUpdateFile(filePath, ev.LanguageData);
        }
        else
            _contentRoot.AddOrUpdateFile(filePath, ev.Data);
        // Languages TTS support end

        var audioParams = AudioParams.Default.WithVolume(volume);
        var soundPath = new SoundPathSpecifier(Prefix / filePath, audioParams);
        if (ev.SourceUid != null)
        {
            var sourceUid = GetEntity(ev.SourceUid.Value);
            _audio.PlayEntity(soundPath, new EntityUid(), sourceUid); // recipient arg ignored on client
        }
        else
        {
            _audio.PlayGlobal(soundPath, Filter.Local(), false);
        }

        _contentRoot.RemoveFile(filePath);
    }

    private float AdjustVolume(bool isWhisper)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
        {
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);
        }

        return volume;
    }
}
