using System.Collections.Generic;
using UnityEngine;

public static class GameSettings
{
    private const string VolumeKey = "settings_volume";
    private const string FullscreenKey = "settings_fullscreen";
    private const string WidthKey = "settings_width";
    private const string HeightKey = "settings_height";

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            float v = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, v);
            AudioListener.volume = v;
        }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            Screen.fullScreen = value;
        }
    }

    public static Resolution[] AvailableResolutions()
    {
        var uniques = new List<Resolution>();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width < 1024) continue;
            bool dup = false;
            foreach (Resolution u in uniques)
                if (u.width == r.width && u.height == r.height) { dup = true; break; }
            if (!dup) uniques.Add(r);
        }
        return uniques.ToArray();
    }

    public static int CurrentResolutionIndex(Resolution[] list)
    {
        for (int i = 0; i < list.Length; i++)
            if (list[i].width == Screen.width && list[i].height == Screen.height) return i;
        return 0;
    }

    public static void SetResolution(int width, int height)
    {
        PlayerPrefs.SetInt(WidthKey, width);
        PlayerPrefs.SetInt(HeightKey, height);
        Screen.SetResolution(width, height, Screen.fullScreen);
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySaved()
    {
        AudioListener.volume = Volume;
        if (Application.isEditor) return;
        int w = PlayerPrefs.GetInt(WidthKey, 0), h = PlayerPrefs.GetInt(HeightKey, 0);
        if (w > 0 && h > 0) Screen.SetResolution(w, h, Fullscreen);
        else Screen.fullScreen = Fullscreen;
    }
}
