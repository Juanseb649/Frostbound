using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Escenas")]
    [Tooltip("Escena de juego que se carga con 'Nuevo juego'.")]
    public string gameSceneName = "SampleScene";

    [Header("Paneles")]
    public GameObject mainPanel;
    public GameObject optionsPanel;
    public GameObject loadPanel;

    [Header("Crear usuario")]
    public InputField usernameInput;
    public Button enterGameButton;
    public Text usernameHint;

    [Header("Botones principales")]
    public Button newGameButton;
    public Button loadButton;
    public Button optionsButton;
    public Button quitButton;

    [Header("Opciones")]
    public Slider volumeSlider;
    public Toggle fullscreenToggle;
    public Button resPrevButton;
    public Button resNextButton;
    public Text resText;
    public Button optionsBackButton;

    [Header("Cargar partida")]
    public Button loadBackButton;
    public Text loadStatusText;
    public Button[] loadSlotButtons;

    private Resolution[] _resolutions;
    private int _resIndex;

    [Header("Cursor personalizado")]
    public Texture2D iceCursorTexture;
    public Vector2 cursorHotspot = new Vector2(16, 16);

    // Nombre del jugador que leen las demás escenas.
    public static string PlayerName { get; private set; }

    void Start()
    {
        if (iceCursorTexture != null)
        {
            Cursor.SetCursor(iceCursorTexture, cursorHotspot, CursorMode.Auto);
        }

        PlayerName = PlayerPrefs.GetString("username", "");

        if (usernameInput != null)
        {
            usernameInput.text = PlayerName;
            usernameInput.onEndEdit.AddListener(val =>
            {
                if (!string.IsNullOrWhiteSpace(val)) EnterGame();
            });
        }
        if (enterGameButton != null) enterGameButton.onClick.AddListener(EnterGame);

        if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
        if (loadButton != null) loadButton.onClick.AddListener(() => ShowPanel(loadPanel));
        if (optionsButton != null) optionsButton.onClick.AddListener(() => ShowPanel(optionsPanel));
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (optionsBackButton != null) optionsBackButton.onClick.AddListener(() => ShowPanel(mainPanel));
        if (loadBackButton != null) loadBackButton.onClick.AddListener(() => ShowPanel(mainPanel));

        if (loadSlotButtons != null)
        {
            foreach (Button slot in loadSlotButtons)
            {
                if (slot == null) continue;
                slot.onClick.AddListener(() =>
                {
                    if (loadStatusText != null)
                        loadStatusText.text = "Este hueco está vacío. (El sistema de guardado se añadirá pronto).";
                });
            }
        }

        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(SetVolume);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (resPrevButton != null) resPrevButton.onClick.AddListener(() => StepResolution(-1));
        if (resNextButton != null) resNextButton.onClick.AddListener(() => StepResolution(1));

        SetupResolutions();

        if (volumeSlider != null) volumeSlider.value = AudioListener.volume;
        if (fullscreenToggle != null) fullscreenToggle.isOn = Screen.fullScreen;

        ShowPanel(mainPanel);
    }

    public void NewGame()
    {
        // Si hay un nombre escrito, lo usa; si no, entra como "Pingüino".
        if (usernameInput != null && !string.IsNullOrWhiteSpace(usernameInput.text))
        {
            EnterGame();
            return;
        }
        PlayerName = "Pingüino";
        PlayerPrefs.SetString("username", PlayerName);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void EnterGame()
    {
        string name = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            if (usernameHint != null) usernameHint.text = "Escribe un nombre de usuario.";
            return;
        }

        PlayerName = name;
        PlayerPrefs.SetString("username", name);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Salir del juego (en el editor no cierra la aplicación).");
#else
        Application.Quit();
#endif
    }

    void ShowPanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (optionsPanel != null) optionsPanel.SetActive(panel == optionsPanel);
        if (loadPanel != null) loadPanel.SetActive(panel == loadPanel);
    }

    void SetVolume(float v) => AudioListener.volume = v;

    void SetFullscreen(bool on) => Screen.fullScreen = on;

    void SetupResolutions()
    {
        Resolution[] all = Screen.resolutions;
        System.Collections.Generic.List<Resolution> uniques = new System.Collections.Generic.List<Resolution>();
        for (int i = 0; i < all.Length; i++)
        {
            bool dup = false;
            for (int j = 0; j < uniques.Count; j++)
            {
                if (uniques[j].width == all[i].width && uniques[j].height == all[i].height) { dup = true; break; }
            }
            if (!dup && all[i].width >= 1024) uniques.Add(all[i]);
        }

        _resolutions = uniques.ToArray();
        _resIndex = 0;
        for (int i = 0; i < _resolutions.Length; i++)
        {
            if (_resolutions[i].width == Screen.width && _resolutions[i].height == Screen.height) { _resIndex = i; break; }
        }

        ApplyResolution();
    }

    void StepResolution(int dir)
    {
        if (_resolutions == null || _resolutions.Length == 0) return;
        _resIndex = (_resIndex + dir + _resolutions.Length) % _resolutions.Length;
        ApplyResolution();
    }

    void ApplyResolution()
    {
        if (_resolutions == null || _resolutions.Length == 0) return;
        Resolution r = _resolutions[_resIndex];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
        if (resText != null) resText.text = r.width + "x" + r.height;
    }
}
