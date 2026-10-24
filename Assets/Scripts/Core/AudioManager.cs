using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AudioId
{
    // UI
    UI_Hover,
    UI_Click,
    UI_Back,
    UI_Equip,
    UI_Error,
    UI_BonfireLit,
    UI_DeathBell,
    UI_QuestBanner,
    UI_OpenMenu,
    UI_CloseMenu,

    // Penguin
    Penguin_Chirp1,
    Penguin_Chirp2,
    Penguin_ChirpRandom,

    // Corrupt Monsters
    Corrupt_Alert1,
    Corrupt_Alert2,
    Corrupt_Attack,
    Corrupt_Pain1,
    Corrupt_Pain2,
    Corrupt_Death,

    // Boss & Heavy
    Boss_Attack,
    Boss_Grunt1,
    Boss_Grunt2,
    Boss_Pain1,
    Boss_Pain2,
    Boss_Laugh,
    Boss_Dizzy,

    // Shaman & Spells
    Shaman_Babble,
    Shaman_Cast,
    Slime_Splat,
    Spell_IceHiss,
    Spell_FaeMagic
}

public enum AmbienceId
{
    None,
    MainMenu,
    Village,
    Foothills,
    Escalinata,
    Ruins,
    StarlitPath,
    Boss
}

// Gestor de sonido y música ambiente (estilo Diablo / Frostbound).
// Carga recursos de audio desde Resources/Audio, maneja transiciones suaves (crossfade)
// de música por escena y reproducción 2D/3D de efectos de interfaz, monstruos y pingüinos.
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private AudioSource _musicSourceA;
    private AudioSource _musicSourceB;
    private AudioSource _uiSource;
    private AudioSource _sfx2DSource;

    private readonly Dictionary<AudioId, AudioClip> _clips = new Dictionary<AudioId, AudioClip>();
    private readonly Dictionary<AmbienceId, AudioClip> _musicClips = new Dictionary<AmbienceId, AudioClip>();

    private Coroutine _fadeCoroutine;
    private AudioSource _activeMusicSource;
    private AmbienceId _currentAmbience = AmbienceId.None;

    public float MasterVolume { get; set; } = 1f;
    public float MusicVolume { get; set; } = 0.75f;
    public float SFXVolume { get; set; } = 0.85f;
    public float UIVolume { get; set; } = 0.8f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInit()
    {
        if (_instance == null)
        {
            var go = new GameObject("AudioManager");
            _instance = go.AddComponent<AudioManager>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitSources();
        LoadAllClips();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }
    }

    private void InitSources()
    {
        _musicSourceA = gameObject.AddComponent<AudioSource>();
        _musicSourceA.loop = true;
        _musicSourceA.playOnAwake = false;
        _musicSourceA.spatialBlend = 0f;

        _musicSourceB = gameObject.AddComponent<AudioSource>();
        _musicSourceB.loop = true;
        _musicSourceB.playOnAwake = false;
        _musicSourceB.spatialBlend = 0f;

        _activeMusicSource = _musicSourceA;

        _uiSource = gameObject.AddComponent<AudioSource>();
        _uiSource.playOnAwake = false;
        _uiSource.spatialBlend = 0f;

        _sfx2DSource = gameObject.AddComponent<AudioSource>();
        _sfx2DSource.playOnAwake = false;
        _sfx2DSource.spatialBlend = 0f;
    }

    private void LoadAllClips()
    {
        // UI
        LoadClip(AudioId.UI_Hover, "Audio/UI/UI_Hover");
        LoadClip(AudioId.UI_Click, "Audio/UI/UI_Click");
        LoadClip(AudioId.UI_Back, "Audio/UI/UI_Back");
        LoadClip(AudioId.UI_Equip, "Audio/UI/UI_Equip");
        LoadClip(AudioId.UI_Error, "Audio/UI/UI_Error");
        LoadClip(AudioId.UI_BonfireLit, "Audio/UI/UI_BonfireLit");
        LoadClip(AudioId.UI_DeathBell, "Audio/UI/UI_DeathBell");
        LoadClip(AudioId.UI_QuestBanner, "Audio/UI/UI_QuestBanner");
        LoadClip(AudioId.UI_OpenMenu, "Audio/UI/UI_OpenMenu");
        LoadClip(AudioId.UI_CloseMenu, "Audio/UI/UI_CloseMenu");

        // Penguin
        LoadClip(AudioId.Penguin_Chirp1, "Audio/SFX/Penguin_Chirp1");
        LoadClip(AudioId.Penguin_Chirp2, "Audio/SFX/Penguin_Chirp2");

        // Monsters
        LoadClip(AudioId.Corrupt_Alert1, "Audio/SFX/Corrupt_Alert1");
        LoadClip(AudioId.Corrupt_Alert2, "Audio/SFX/Corrupt_Alert2");
        LoadClip(AudioId.Corrupt_Attack, "Audio/SFX/Corrupt_Attack");
        LoadClip(AudioId.Corrupt_Pain1, "Audio/SFX/Corrupt_Pain1");
        LoadClip(AudioId.Corrupt_Pain2, "Audio/SFX/Corrupt_Pain2");
        LoadClip(AudioId.Corrupt_Death, "Audio/SFX/Corrupt_Death");

        // Boss
        LoadClip(AudioId.Boss_Attack, "Audio/SFX/Boss_Attack");
        LoadClip(AudioId.Boss_Grunt1, "Audio/SFX/Boss_Grunt1");
        LoadClip(AudioId.Boss_Grunt2, "Audio/SFX/Boss_Grunt2");
        LoadClip(AudioId.Boss_Pain1, "Audio/SFX/Boss_Pain1");
        LoadClip(AudioId.Boss_Pain2, "Audio/SFX/Boss_Pain2");
        LoadClip(AudioId.Boss_Laugh, "Audio/SFX/Boss_Laugh");
        LoadClip(AudioId.Boss_Dizzy, "Audio/SFX/Boss_Dizzy");

        // Magic / Spells
        LoadClip(AudioId.Shaman_Babble, "Audio/SFX/Shaman_Babble");
        LoadClip(AudioId.Shaman_Cast, "Audio/SFX/Shaman_Cast");
        LoadClip(AudioId.Slime_Splat, "Audio/SFX/Slime_Splat");
        LoadClip(AudioId.Spell_IceHiss, "Audio/SFX/Spell_IceHiss");
        LoadClip(AudioId.Spell_FaeMagic, "Audio/SFX/Spell_FaeMagic");

        // Ambient Music Loops
        LoadMusic(AmbienceId.MainMenu, "Audio/Music/Ambience_MainMenu");
        LoadMusic(AmbienceId.Village, "Audio/Music/Ambience_Village");
        LoadMusic(AmbienceId.Foothills, "Audio/Music/Ambience_Foothills");
        LoadMusic(AmbienceId.Escalinata, "Audio/Music/Ambience_Escalinata");
        LoadMusic(AmbienceId.Ruins, "Audio/Music/Ambience_Ruins");
        LoadMusic(AmbienceId.StarlitPath, "Audio/Music/Ambience_StarlitPath");
        LoadMusic(AmbienceId.Boss, "Audio/Music/Boss_Theme");

        // Voces de los corruptos (gruñidos goblinoides y chillidos agudos, estilo caídos): cada una se elige al azar de su carpeta.
        AddFolder(AudioId.Corrupt_Alert1, "Audio/SFX/Corrupt/Alert");
        AddFolder(AudioId.Corrupt_Alert2, "Audio/SFX/Corrupt/Alert");
        AddFolder(AudioId.Corrupt_Attack, "Audio/SFX/Corrupt/Attack");
        AddFolder(AudioId.Corrupt_Pain1, "Audio/SFX/Corrupt/Pain");
        AddFolder(AudioId.Corrupt_Pain2, "Audio/SFX/Corrupt/Pain");
        AddFolder(AudioId.Corrupt_Death, "Audio/SFX/Corrupt/Death");
    }

    private void LoadClip(AudioId id, string resourcePath)
    {
        AudioClip c = Resources.Load<AudioClip>(resourcePath);
        if (c != null) _clips[id] = c;
    }

    private readonly Dictionary<AudioId, AudioClip[]> _variants = new Dictionary<AudioId, AudioClip[]>();

    private void AddVariants(AudioId id, params string[] paths)
    {
        var list = new List<AudioClip>();
        foreach (string p in paths)
        {
            AudioClip c = Resources.Load<AudioClip>(p);
            if (c != null) list.Add(c);
        }
        if (list.Count > 0) _variants[id] = list.ToArray();
    }

    private void AddFolder(AudioId id, string folder)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(folder);
        if (clips != null && clips.Length > 0) _variants[id] = clips;
    }

    private void LoadMusic(AmbienceId id, string resourcePath)
    {
        AudioClip c = Resources.Load<AudioClip>(resourcePath);
        if (c != null) _musicClips[id] = c;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        PlayAmbienceForScene(scene.name);
    }

    public void PlayAmbienceForScene(string sceneName)
    {
        if (sceneName.Contains("MainMenu") || sceneName.Contains("00_"))
            PlayAmbience(AmbienceId.MainMenu);
        else if (sceneName.Contains("Village") || sceneName.Contains("01_"))
            PlayAmbience(AmbienceId.Village);
        else if (sceneName.Contains("Foothills") || sceneName.Contains("02_"))
            PlayAmbience(AmbienceId.Foothills);
        else if (sceneName.Contains("Escalinata") || sceneName.Contains("03_"))
            PlayAmbience(AmbienceId.Escalinata);
        else
            PlayAmbience(AmbienceId.Village);
    }

    public void PlayAmbience(AmbienceId ambience, float crossfadeDuration = 2f)
    {
        if (ambience == _currentAmbience && _activeMusicSource.isPlaying) return;
        _currentAmbience = ambience;

        if (ambience == AmbienceId.None || !_musicClips.TryGetValue(ambience, out AudioClip targetClip) || targetClip == null)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutCurrent(crossfadeDuration));
            return;
        }

        AudioSource nextSource = (_activeMusicSource == _musicSourceA) ? _musicSourceB : _musicSourceA;
        nextSource.clip = targetClip;
        nextSource.volume = 0f;
        nextSource.Play();

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(CrossfadeRoutine(_activeMusicSource, nextSource, crossfadeDuration));
        _activeMusicSource = nextSource;
    }

    private IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float duration)
    {
        float targetVol = MasterVolume * MusicVolume;
        float elapsed = 0f;
        float startFromVol = from.isPlaying ? from.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (from.isPlaying) from.volume = Mathf.Lerp(startFromVol, 0f, t);
            to.volume = Mathf.Lerp(0f, targetVol, t);
            yield return null;
        }

        if (from.isPlaying)
        {
            from.Stop();
            from.clip = null;
        }
        to.volume = targetVol;
        _fadeCoroutine = null;
    }

    private IEnumerator FadeOutCurrent(float duration)
    {
        float startVol = _activeMusicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _activeMusicSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }
        _activeMusicSource.Stop();
        _activeMusicSource.clip = null;
        _fadeCoroutine = null;
    }

    // ---------- UI SFX ----------

    public static void PlayUI(AudioId id, float volumeScale = 1f, float pitchVariance = 0.04f)
    {
        if (Instance == null) return;
        Instance.DoPlayUI(id, volumeScale, pitchVariance);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Últimos sonidos de UI (para las comprobaciones del bridge: exec SmokeTest.UISounds).
    public static readonly System.Collections.Generic.List<AudioId> RecentUI = new System.Collections.Generic.List<AudioId>();
#endif

    private void DoPlayUI(AudioId id, float volumeScale, float pitchVariance)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RecentUI.Add(id);
        if (RecentUI.Count > 16) RecentUI.RemoveAt(0);
#endif
        AudioClip clip = ResolveClip(id);
        if (clip == null) return;

        float pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
        _uiSource.pitch = pitch;
        float vol = MasterVolume * UIVolume * volumeScale;
        _uiSource.PlayOneShot(clip, vol);
    }

    // ---------- 2D SFX ----------

    public static void PlaySFX(AudioId id, float volumeScale = 1f, float pitchVariance = 0.06f)
    {
        if (Instance == null) return;
        Instance.DoPlaySFX(id, volumeScale, pitchVariance);
    }

    private void DoPlaySFX(AudioId id, float volumeScale, float pitchVariance)
    {
        AudioClip clip = ResolveClip(id);
        if (clip == null) return;

        float pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
        _sfx2DSource.pitch = pitch;
        float vol = MasterVolume * SFXVolume * volumeScale;
        _sfx2DSource.PlayOneShot(clip, vol);
    }

    // ---------- 3D SFX (Espacial) ----------

    public static void Play3D(AudioId id, Vector3 position, float volumeScale = 1f, float minDistance = 2f, float maxDistance = 22f, float pitchVariance = 0.08f)
    {
        if (Instance == null) return;
        Instance.DoPlay3D(id, position, volumeScale, minDistance, maxDistance, pitchVariance);
    }

    private void DoPlay3D(AudioId id, Vector3 position, float volumeScale, float minDistance, float maxDistance, float pitchVariance)
    {
        AudioClip clip = ResolveClip(id);
        if (clip == null) return;

        var tempGO = new GameObject("TempAudio3D");
        tempGO.transform.position = position;
        var source = tempGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
        source.volume = MasterVolume * SFXVolume * volumeScale;
        source.Play();

        Destroy(tempGO, clip.length / Mathf.Max(0.1f, source.pitch) + 0.15f);
    }

    // ---------- Sonidos por ruta (Resources/Audio/...) ----------
    // path puede ser un archivo ("SFX/Rune_Ignis_Proc") o una carpeta ("Combate/Gore/"), de la que se elige uno al azar.
    private readonly Dictionary<string, AudioClip[]> _bank = new Dictionary<string, AudioClip[]>();
    private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();

    private AudioClip ResolvePath(string path)
    {
        if (!_bank.TryGetValue(path, out AudioClip[] clips))
        {
            if (path.EndsWith("/")) clips = Resources.LoadAll<AudioClip>("Audio/" + path.TrimEnd('/'));
            else { AudioClip c = Resources.Load<AudioClip>("Audio/" + path); clips = c != null ? new[] { c } : new AudioClip[0]; }
            _bank[path] = clips;
        }
        return clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    // Evita que un mismo sonido se amontone (runas que saltan en cada golpe, golpes de varios enemigos...).
    private bool Throttled(string key, float minGap)
    {
        if (minGap <= 0f) return false;
        if (_lastPlayed.TryGetValue(key, out float last) && Time.unscaledTime - last < minGap) return true;
        _lastPlayed[key] = Time.unscaledTime;
        return false;
    }

    public static void Play3D(string path, Vector3 position, float volumeScale = 1f, float minGap = 0f, float minDistance = 2f, float maxDistance = 24f, float pitchVariance = 0.07f)
    {
        AudioManager m = Instance;
        if (m == null || m.Throttled(path, minGap)) return;
        AudioClip clip = m.ResolvePath(path);
        if (clip == null) return;
        var go = new GameObject("TempAudio3D");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
        source.volume = m.MasterVolume * m.SFXVolume * volumeScale;
        source.Play();
        Destroy(go, clip.length / Mathf.Max(0.1f, source.pitch) + 0.15f);
    }

    public static void PlaySFX(string path, float volumeScale = 1f, float minGap = 0f, float pitchVariance = 0.04f)
    {
        AudioManager m = Instance;
        if (m == null || m.Throttled(path, minGap)) return;
        AudioClip clip = m.ResolvePath(path);
        if (clip == null) return;
        m._sfx2DSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
        m._sfx2DSource.PlayOneShot(clip, m.MasterVolume * m.SFXVolume * volumeScale);
    }

    public static bool Has(string path) => Instance != null && Instance.ResolvePath(path) != null;

    // ---------- Música de jefe ----------
    // Mientras un jefe esté en pelea suena el tema de jefe; al terminar vuelve la música de la zona.
    private AmbienceId _beforeBoss = AmbienceId.None;
    private float _bossCheckAt;
    public bool BossMusicOn => _currentAmbience == AmbienceId.Boss;

    void Update()
    {
        if (Time.unscaledTime < _bossCheckAt) return;
        _bossCheckAt = Time.unscaledTime + 0.4f;
        BossController boss = BossController.Active;
        bool fight = boss != null && boss.Engaged && boss.Health != null && !boss.Health.IsDead;
        if (fight && _currentAmbience != AmbienceId.Boss && _musicClips.ContainsKey(AmbienceId.Boss))
        {
            _beforeBoss = _currentAmbience;
            PlayAmbience(AmbienceId.Boss, 1.2f);
        }
        else if (!fight && _currentAmbience == AmbienceId.Boss)
        {
            if (_beforeBoss != AmbienceId.None) PlayAmbience(_beforeBoss, 3.5f);
            else PlayAmbienceForScene(SceneManager.GetActiveScene().name);
        }
    }

    private AudioClip ResolveClip(AudioId id)
    {
        if (id == AudioId.Penguin_ChirpRandom)
        {
            id = UnityEngine.Random.value < 0.5f ? AudioId.Penguin_Chirp1 : AudioId.Penguin_Chirp2;
        }

        if (_variants.TryGetValue(id, out AudioClip[] options) && options.Length > 0)
            return options[UnityEngine.Random.Range(0, options.Length)];
        if (_clips.TryGetValue(id, out AudioClip clip) && clip != null)
            return clip;

        return null;
    }
}
