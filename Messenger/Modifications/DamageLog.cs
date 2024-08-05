using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Messenger.Modifications;

public static class DamageLog
{
    private static readonly List<string> m_damageLog = new();
    private static readonly List<KeyValuePair<float, float>> me_damage = new();
    private static readonly List<KeyValuePair<float, float>> other_damage = new();

    private static void CalculateDPS(string name, List<KeyValuePair<float, float>> damages, float damage)
    {
        float time = Time.time;
        if (damages.Count > 0 && Time.time - damages[damages.Count - 1].Key > 5.0) damages.Clear();
        damages.Add(new KeyValuePair<float, float>(time, damage));
        float num1 = time - damages[0].Key;
        if (num1 < 0.000999999f) return;
        float num2 = 0.0f;
        foreach (KeyValuePair<float, float> kvp in damages)
        {
            num2 += kvp.Value;
        }

        float total = num2 / num1;
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendFormat("<color=#{0}>{1}</color> ", ColorUtility.ToHtmlStringRGBA(MessengerPlugin._dateColor.Value), ChatLog.GetTimeString());
        stringBuilder.AppendFormat("<color=orange>{0}</color> ", name);
        stringBuilder.AppendFormat("(<color=yellow>{0} {2}</color>, {1:0.0}s): ", damages.Count, num1, MessengerPlugin._attacks.Value);
        stringBuilder.AppendFormat("<color={0}>{1:0.0}</color> {2}", GetTotalDamageColor(total), total, MessengerPlugin._dps.Value);
        m_damageLog.Add(stringBuilder.ToString());
    }
    
    private static void AddDamageLog(HitData hit, Character character)
    {
        string key;
        if (character == Player.m_localPlayer)
        {
            key = $"<color=orange>{Player.m_localPlayer.GetHoverName()}</color> {MessengerPlugin._hasTaken.Value}";
        }
        else
        {
            var attacker = hit.GetAttacker() == null ? MessengerPlugin._unknown.Value : hit.GetAttacker().GetHoverName();
            key = $"<color=orange>{attacker}</color> {MessengerPlugin._hasInflicted.Value}";
        }
        CalculateDPS(key, character == Player.m_localPlayer ? me_damage : other_damage, hit.GetTotalDamage());
        while (m_damageLog.Count > MessengerPlugin._maxChatLog.Value)
        {
            m_damageLog.RemoveAt(0);
        }
    }

    private static string GetTotalDamageColor(float damage)
    {
        Color color;
        switch (damage)
        {
            case > 10 and < 20:
                color = new Color(1f, 0f, 0.5f, 1f);
                break;
            case > 20 and < 30:
                color = new Color(1f, 0.4f, 0.3f, 1f);
                break;
            case > 30 and < 40:
                color = new Color(1f, 0.8f, 0f, 1f);
                break;
            case > 50:
                color = Color.yellow;
                break;
            case < 10 :
                color = Color.gray;
                break;
            default:
                color = Color.white;
                break;
        }

        return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
    }

    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    private static class Character_ApplyDamage_Patch
    {
        private static void Postfix(Character __instance, HitData hit) => AddDamageLog(hit, __instance);
    }
    
    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
    private static class TextsDialog_UpdateTextsList_Patch
    {
        private static void Postfix(TextsDialog __instance)
        {
            if (MessengerPlugin._useDamageLog.Value is MessengerPlugin.Toggle.Off) return;
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var message in m_damageLog)
            {
                stringBuilder.Append(message + "\n");
            }
            __instance.m_texts.Insert(0, new TextsDialog.TextInfo(MessengerPlugin._damageLogTitle.Value, stringBuilder.ToString()));
        }
    }
}