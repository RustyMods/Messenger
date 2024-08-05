using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Messenger.Modifications;

public static class RemoveTexts
{
    private static string m_currentText = "";

    private static void OnRemove()
    {
        if (!Player.m_localPlayer) return;
        if (!Player.m_localPlayer.m_knownTexts.ContainsKey(m_currentText))
        {
            if (m_currentText.StartsWith("["))
            {
                string key = $"${m_currentText.Replace("[", string.Empty).Replace("]", string.Empty)}";
                if (Player.m_localPlayer.m_knownTexts.ContainsKey(key))
                    Player.m_localPlayer.m_knownTexts.Remove(key);
            }
        }
        else
        {
            Player.m_localPlayer.m_knownTexts.Remove(m_currentText);
        }
        m_currentText = "";
        InventoryGui.instance.OnOpenTexts();
    }
    
    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.Awake))]
    private static class TextsDialogue_Awake_Patch
    {
        private static void Postfix(TextsDialog __instance)
        {
            Transform? Texts_frame = __instance.transform.Find("Texts_frame");
            if (!Texts_frame) return;
            Transform Closebutton = Texts_frame.Find("Closebutton");
            GameObject removeButton = Object.Instantiate(Closebutton.gameObject, Texts_frame);
            removeButton.name = "Removebutton";
            if (!removeButton.TryGetComponent(out RectTransform rectTransform)) return;
            rectTransform.position = MessengerPlugin._removeButtonPos.Value;
            MessengerPlugin._removeButtonPos.SettingChanged += (sender, args) =>
            {
                rectTransform.position = MessengerPlugin._removeButtonPos.Value;
            };
            if (!removeButton.TryGetComponent(out Button button)) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnRemove);
            if (!removeButton.TryGetComponent(out ButtonSfx buttonSfx)) return;
            buttonSfx.Start();
            Transform text = removeButton.transform.Find("Text");
            if (!text.TryGetComponent(out TMP_Text component)) return;
            component.text = MessengerPlugin._removeText.Value;
            MessengerPlugin._removeText.SettingChanged += (sender, args) =>
            {
                component.text = MessengerPlugin._removeText.Value;
            };
        }
    }

    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.OnSelectText))]
    private static class TextsDialog_OnSelectText_Patch
    {
        private static void Postfix(TextsDialog.TextInfo text) => m_currentText = text.m_topic;
    }
}