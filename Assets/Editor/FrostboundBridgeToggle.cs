using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

public static class FrostboundBridgeToggle
{
    private const string Symbol = "FROSTBOUND_BRIDGE";
    private const string MenuPath = "Tools/Frostbound/Debug/Bridge de automatización";

    private static NamedBuildTarget Target => NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

    private static List<string> Symbols() =>
        PlayerSettings.GetScriptingDefineSymbols(Target).Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        List<string> symbols = Symbols();
        if (!symbols.Remove(Symbol)) symbols.Add(Symbol);
        PlayerSettings.SetScriptingDefineSymbols(Target, string.Join(";", symbols));
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Symbols().Contains(Symbol));
        return true;
    }
}
