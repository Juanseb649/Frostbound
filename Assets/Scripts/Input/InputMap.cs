using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public enum GameAction
{
    MoveUp, MoveDown, MoveLeft, MoveRight,
    Attack, Ability, Dodge, Interact, QuickPotion,
    Belt1, Belt2, Belt3, Belt4, Belt5, Belt6,
    Inventory, Character, Map, Pause
}

// Mapa de controles: qué tecla/botón del ratón y qué botón del mando hace cada acción.
// El jugador 1 elige su periférico preferido (teclado y ratón, o mando de Xbox) y puede reasignar ambos.
// El jugador 2 siempre juega con mando y usa la misma asignación de mando.
// Se guarda en PlayerPrefs.
public static class InputMap
{
    public enum Device { KeyboardMouse, Gamepad }

    // Códigos de teclado: >= 0 es un Key; MouseLeft/Right/Middle son los botones del ratón; None = sin asignar.
    public const int None = int.MinValue;
    public const int MouseLeft = -1, MouseRight = -2, MouseMiddle = -3;
    // Códigos de mando: un GamepadButton (o NoPad).
    public const int NoPad = -1;

    private const string PrefDevice = "controls_device";
    private const string PrefKeys = "controls_keys";
    private const string PrefPad = "controls_pad";
    private const string PrefVersion = "controls_version";
    // Sube cuando cambian los botones por defecto del mando: las asignaciones guardadas de antes se restablecen.
    private const int PadDefaultsVersion = 2;
    // Atacar pasó del clic derecho al izquierdo: se migra una vez a quien tenía guardado el antiguo por defecto.
    private const string PrefAttackLeft = "controls_attack_left";

    public static readonly GameAction[] All = (GameAction[])Enum.GetValues(typeof(GameAction));

    public static event Action Changed;

    private static readonly Dictionary<GameAction, int> Keys = new Dictionary<GameAction, int>();
    private static readonly Dictionary<GameAction, int> Pads = new Dictionary<GameAction, int>();
    private static bool _loaded;
    private static Device _device;

    // ---------- Valores por defecto ----------

    public static int DefaultKey(GameAction a)
    {
        switch (a)
        {
            case GameAction.MoveUp: return (int)Key.W;
            case GameAction.MoveDown: return (int)Key.S;
            case GameAction.MoveLeft: return (int)Key.A;
            case GameAction.MoveRight: return (int)Key.D;
            case GameAction.Attack: return MouseLeft;
            case GameAction.Ability: return (int)Key.F;
            case GameAction.Dodge: return (int)Key.Space;
            case GameAction.Interact: return (int)Key.E;
            case GameAction.QuickPotion: return (int)Key.Q;
            case GameAction.Belt1: return (int)Key.Digit1;
            case GameAction.Belt2: return (int)Key.Digit2;
            case GameAction.Belt3: return (int)Key.Digit3;
            case GameAction.Belt4: return (int)Key.Digit4;
            case GameAction.Belt5: return (int)Key.Digit5;
            case GameAction.Belt6: return (int)Key.Digit6;
            case GameAction.Inventory: return (int)Key.I;
            case GameAction.Character: return (int)Key.C;
            case GameAction.Map: return (int)Key.M;
            case GameAction.Pause: return (int)Key.Escape;
            default: return None;
        }
    }

    // Como en Minecraft Dungeons: X ataca, A rueda, B recoge/habla, Y mochila, Vista mapa, RB habilidad, LB poción, cruceta = cinturón.
    public static int DefaultPad(GameAction a)
    {
        switch (a)
        {
            case GameAction.Attack: return (int)GamepadButton.West;
            case GameAction.Ability: return (int)GamepadButton.RightShoulder;
            case GameAction.Dodge: return (int)GamepadButton.South;
            case GameAction.Interact: return (int)GamepadButton.East;
            case GameAction.QuickPotion: return (int)GamepadButton.LeftShoulder;
            case GameAction.Belt1: return (int)GamepadButton.DpadLeft;
            case GameAction.Belt2: return (int)GamepadButton.DpadUp;
            case GameAction.Belt3: return (int)GamepadButton.DpadRight;
            case GameAction.Belt4: return (int)GamepadButton.DpadDown;
            case GameAction.Belt5: return (int)GamepadButton.LeftTrigger;
            case GameAction.Belt6: return (int)GamepadButton.RightTrigger;
            case GameAction.Inventory: return (int)GamepadButton.North;
            case GameAction.Character: return (int)GamepadButton.RightStick;
            case GameAction.Map: return (int)GamepadButton.Select;
            case GameAction.Pause: return (int)GamepadButton.Start;
            default: return NoPad;
        }
    }

    // Con mando, moverse es siempre el stick izquierdo (no se reasigna).
    public static bool PadRebindable(GameAction a) => a != GameAction.MoveUp && a != GameAction.MoveDown && a != GameAction.MoveLeft && a != GameAction.MoveRight;

    public static string ActionName(GameAction a)
    {
        switch (a)
        {
            case GameAction.MoveUp: return "Avanzar";
            case GameAction.MoveDown: return "Retroceder";
            case GameAction.MoveLeft: return "Moverse a la izquierda";
            case GameAction.MoveRight: return "Moverse a la derecha";
            case GameAction.Attack: return "Atacar";
            case GameAction.Ability: return "Habilidad de clase";
            case GameAction.Dodge: return "Rodar";
            case GameAction.Interact: return "Recoger / hablar";
            case GameAction.QuickPotion: return "Poción de vida rápida";
            case GameAction.Inventory: return "Mochila";
            case GameAction.Character: return "Personaje";
            case GameAction.Map: return "Mapa";
            case GameAction.Pause: return "Pausa";
            default: return "Cinturón " + ((int)a - (int)GameAction.Belt1 + 1);
        }
    }

    // ---------- Guardado ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _loaded = false;
        Changed = null;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _device = (Device)PlayerPrefs.GetInt(PrefDevice, (int)Device.KeyboardMouse);
        Keys.Clear();
        Pads.Clear();
        foreach (GameAction a in All) { Keys[a] = DefaultKey(a); Pads[a] = DefaultPad(a); }
        Parse(PlayerPrefs.GetString(PrefKeys, ""), Keys);
        if (PlayerPrefs.GetInt(PrefAttackLeft, 0) == 0)
        {
            PlayerPrefs.SetInt(PrefAttackLeft, 1);
            if (Keys[GameAction.Attack] == MouseRight)
            {
                foreach (GameAction other in All)
                    if (other != GameAction.Attack && Keys[other] == MouseLeft) Keys[other] = MouseRight;
                Keys[GameAction.Attack] = MouseLeft;
                PlayerPrefs.SetString(PrefKeys, Serialize(Keys));
            }
            PlayerPrefs.Save();
        }
        if (PlayerPrefs.GetInt(PrefVersion, 1) >= PadDefaultsVersion) Parse(PlayerPrefs.GetString(PrefPad, ""), Pads);
        else
        {
            PlayerPrefs.SetInt(PrefVersion, PadDefaultsVersion);
            PlayerPrefs.DeleteKey(PrefPad);
        }
    }

    private static void Parse(string s, Dictionary<GameAction, int> into)
    {
        if (string.IsNullOrEmpty(s)) return;
        foreach (string pair in s.Split(';'))
        {
            string[] kv = pair.Split('=');
            if (kv.Length != 2) continue;
            if (Enum.TryParse(kv[0], out GameAction a) && int.TryParse(kv[1], out int v)) into[a] = v;
        }
    }

    private static string Serialize(Dictionary<GameAction, int> map)
    {
        var parts = new List<string>();
        foreach (var kv in map) parts.Add(kv.Key + "=" + kv.Value);
        return string.Join(";", parts);
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(PrefDevice, (int)_device);
        PlayerPrefs.SetString(PrefKeys, Serialize(Keys));
        PlayerPrefs.SetString(PrefPad, Serialize(Pads));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // ---------- Consulta y cambios ----------

    public static Device Preferred
    {
        get { EnsureLoaded(); return _device; }
        set { EnsureLoaded(); if (_device == value) return; _device = value; Save(); }
    }

    public static int KeyOf(GameAction a) { EnsureLoaded(); return Keys.TryGetValue(a, out int v) ? v : None; }
    public static int PadOf(GameAction a) { EnsureLoaded(); return Pads.TryGetValue(a, out int v) ? v : NoPad; }

    // Asigna y, si otra acción ya usaba esa tecla, le pasa la que tenía esta (intercambio). Devuelve la acción afectada.
    public static GameAction? SetKey(GameAction a, int code)
    {
        EnsureLoaded();
        GameAction? swapped = null;
        int old = Keys[a];
        foreach (GameAction other in All)
            if (other != a && Keys[other] == code) { Keys[other] = old; swapped = other; }
        Keys[a] = code;
        Save();
        return swapped;
    }

    public static GameAction? SetPad(GameAction a, int code)
    {
        EnsureLoaded();
        GameAction? swapped = null;
        int old = Pads[a];
        foreach (GameAction other in All)
            if (other != a && Pads[other] == code && PadRebindable(other)) { Pads[other] = old; swapped = other; }
        Pads[a] = code;
        Save();
        return swapped;
    }

    public static void ResetDefaults(Device d)
    {
        EnsureLoaded();
        foreach (GameAction a in All)
        {
            if (d == Device.KeyboardMouse) Keys[a] = DefaultKey(a);
            else Pads[a] = DefaultPad(a);
        }
        Save();
    }

    // ---------- Lectura ----------


    // Se leen todos los teclados y ratones conectados: algunos mandos o programas crean un teclado virtual que
    // pasaría a ser Keyboard.current y dejaría al héroe sin moverse aunque se pulsen las teclas reales.
    private static bool KbCheck(int code, bool pressedThisFrame)
    {
        if (code == None) return false;
        if (code < 0)
        {
            foreach (InputDevice d in InputSystem.devices)
            {
                if (!(d is Mouse m)) continue;
                var c = code == MouseLeft ? m.leftButton : code == MouseRight ? m.rightButton : m.middleButton;
                if (pressedThisFrame ? c.wasPressedThisFrame : c.isPressed) return true;
            }
            return false;
        }
        if (code == (int)Key.None) return false;
        foreach (InputDevice d in InputSystem.devices)
        {
            if (!(d is Keyboard kb)) continue;
            UnityEngine.InputSystem.Controls.KeyControl c;
            try { c = kb[(Key)code]; } catch { continue; }
            if (c != null && (pressedThisFrame ? c.wasPressedThisFrame : c.isPressed)) return true;
        }
        return false;
    }

    public static bool KeyDown(Key k) => KbCheck((int)k, true);
    public static bool KeyIsDown(Key k) => KbCheck((int)k, false);

    private static UnityEngine.InputSystem.Controls.ButtonControl PadControl(Gamepad g, int code)
    {
        if (g == null || code == NoPad) return null;
        try { return g[(GamepadButton)code]; } catch { return null; }
    }

    public static bool KeyPressed(GameAction a) => KbCheck(KeyOf(a), true);
    public static bool KeyHeld(GameAction a) => KbCheck(KeyOf(a), false);
    public static bool PadPressed(Gamepad g, GameAction a) { var c = PadControl(g, PadOf(a)); return c != null && c.wasPressedThisFrame; }
    public static bool PadHeld(Gamepad g, GameAction a) { var c = PadControl(g, PadOf(a)); return c != null && c.isPressed; }

    public static bool KeyIsMouse(GameAction a) { int k = KeyOf(a); return k != None && k < 0; }

    // Lo que pulsa este héroe (teclado si es el jugador 1, y su mando si tiene).
    public static bool Pressed(GameAction a, Component who)
    {
        if (PlayerController.UsesKeyboard(who) && KeyPressed(a)) return true;
        return PadPressed(PlayerController.PadOf(who), a);
    }

    public static bool Held(GameAction a, Component who)
    {
        if (PlayerController.UsesKeyboard(who) && KeyHeld(a)) return true;
        return PadHeld(PlayerController.PadOf(who), a);
    }

    // Dirección de las teclas de movimiento (con las flechas como alternativa fija).
    public static Vector2 KeyboardMove()
    {
        Vector2 v = Vector2.zero;
        if (KeyHeld(GameAction.MoveUp) || KeyIsDown(Key.UpArrow)) v.y += 1f;
        if (KeyHeld(GameAction.MoveDown) || KeyIsDown(Key.DownArrow)) v.y -= 1f;
        if (KeyHeld(GameAction.MoveLeft) || KeyIsDown(Key.LeftArrow)) v.x -= 1f;
        if (KeyHeld(GameAction.MoveRight) || KeyIsDown(Key.RightArrow)) v.x += 1f;
        return Vector2.ClampMagnitude(v, 1f);
    }

    // ---------- Nombres ----------

    public static string KeyName(int code)
    {
        if (code == None) return "—";
        if (code == MouseLeft) return "Clic izquierdo";
        if (code == MouseRight) return "Clic derecho";
        if (code == MouseMiddle) return "Rueda";
        Key k = (Key)code;
        if (k >= Key.Digit1 && k <= Key.Digit9) return ((int)k - (int)Key.Digit1 + 1).ToString();
        if (k == Key.Digit0) return "0";
        if (k >= Key.A && k <= Key.Z) return k.ToString();
        switch (k)
        {
            case Key.Space: return "Espacio";
            case Key.Escape: return "Esc";
            case Key.Enter: return "Intro";
            case Key.Tab: return "Tab";
            case Key.Backspace: return "Retroceso";
            case Key.LeftShift: return "Mayús izq.";
            case Key.RightShift: return "Mayús der.";
            case Key.LeftCtrl: return "Ctrl izq.";
            case Key.RightCtrl: return "Ctrl der.";
            case Key.LeftAlt: return "Alt";
            case Key.RightAlt: return "Alt Gr";
            case Key.UpArrow: return "Flecha arriba";
            case Key.DownArrow: return "Flecha abajo";
            case Key.LeftArrow: return "Flecha izq.";
            case Key.RightArrow: return "Flecha der.";
            case Key.CapsLock: return "Bloq Mayús";
            default: return k.ToString();
        }
    }

    public static string PadName(int code)
    {
        if (code == NoPad) return "—";
        switch ((GamepadButton)code)
        {
            case GamepadButton.South: return "A";
            case GamepadButton.East: return "B";
            case GamepadButton.West: return "X";
            case GamepadButton.North: return "Y";
            case GamepadButton.LeftShoulder: return "LB";
            case GamepadButton.RightShoulder: return "RB";
            case GamepadButton.LeftTrigger: return "LT";
            case GamepadButton.RightTrigger: return "RT";
            case GamepadButton.LeftStick: return "LS";
            case GamepadButton.RightStick: return "RS";
            case GamepadButton.DpadUp: return "Cruceta arriba";
            case GamepadButton.DpadDown: return "Cruceta abajo";
            case GamepadButton.DpadLeft: return "Cruceta izq.";
            case GamepadButton.DpadRight: return "Cruceta der.";
            case GamepadButton.Start: return "Menú";
            case GamepadButton.Select: return "Vista";
            default: return ((GamepadButton)code).ToString();
        }
    }

    // Botones del mando que se pueden asignar (en este orden se buscan al reasignar).
    public static readonly GamepadButton[] PadButtons =
    {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
        GamepadButton.LeftStick, GamepadButton.RightStick, GamepadButton.Start, GamepadButton.Select
    };

    // Primer botón del mando pulsado en este fotograma (para reasignar).
    public static int PadButtonPressed()
    {
        foreach (Gamepad g in Gamepad.all)
            foreach (GamepadButton b in PadButtons)
                if (g[b].wasPressedThisFrame) return (int)b;
        return NoPad;
    }

    public static bool AnyPadButtonHeld()
    {
        foreach (Gamepad g in Gamepad.all)
            foreach (GamepadButton b in PadButtons)
                if (g[b].isPressed) return true;
        return false;
    }

    // Primera tecla o botón del ratón pulsado (Esc no cuenta: cancela).
    public static int KeyPressedNow()
    {
        Mouse m = Mouse.current;
        if (m != null)
        {
            if (m.leftButton.wasPressedThisFrame) return MouseLeft;
            if (m.rightButton.wasPressedThisFrame) return MouseRight;
            if (m.middleButton.wasPressedThisFrame) return MouseMiddle;
        }
        Keyboard kb = Keyboard.current;
        if (kb == null) return None;
        foreach (var k in kb.allKeys)
            if (k != null && k.wasPressedThisFrame && k.keyCode != Key.Escape) return (int)k.keyCode;
        return None;
    }

    public static bool AnyKeyHeld()
    {
        Mouse m = Mouse.current;
        if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed)) return true;
        Keyboard kb = Keyboard.current;
        return kb != null && kb.anyKey.isPressed;
    }
}
