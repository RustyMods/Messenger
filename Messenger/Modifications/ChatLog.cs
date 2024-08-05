using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Messenger.Modifications;

public static class ChatLog
{
    private static readonly List<string> m_chatLog = new();

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), typeof(string), typeof(string), typeof(Talker.Type), typeof(bool))]
    private static class Chat_AddString_Patch
    {
        private static void Postfix(string user, string text, Talker.Type type)
        {
            if (MessengerPlugin._useChatLog.Value is MessengerPlugin.Toggle.Off) return;
            Color color;
            switch (type)
            {
                case Talker.Type.Whisper:
                    color = new Color(1f, 1f, 1f, 0.75f);
                    text = text.ToLowerInvariant();
                    break;
                case Talker.Type.Shout:
                    color = Color.yellow;
                    text = text.ToUpper();
                    break;
                default:
                    color = Color.white;
                    break;
            }

            string time = GetTimeString();
            string message =
                $"{(MessengerPlugin._showTimestamp.Value is MessengerPlugin.Toggle.On ? $"<color=#{ColorUtility.ToHtmlStringRGBA(MessengerPlugin._dateColor.Value)}>{time}</color>" : "")} <color=orange>{user}</color>: <color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
            m_chatLog.Add(message);
            while (m_chatLog.Count > MessengerPlugin._maxChatLog.Value)
            {
                m_chatLog.RemoveAt(0);
            }
        }
    }

    public static string GetTimeString()
    {
        string time;
        switch (MessengerPlugin._chatLogDateFormat.Value)
        {
            case MessengerPlugin.DateFormat.Numbered:
                time = $"[{DateTime.Now:MM-dd-yyyy HH:mm:ss}]";
                break;
            case MessengerPlugin.DateFormat.Worded:
                time = $"[{DateTime.Now:MMM dd yyyy h:mmtt}]";
                break;
            case MessengerPlugin.DateFormat.TimeOnly:
                time = $"[{DateTime.Now:h:mmtt}]";
                break;
            default:
                time = $"[{DateTime.Now:MM-dd-yyyy HH:mm:ss}]";
                break;
        }

        return time;
    }

    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
    private static class TextsDialog_UpdateTextsList_Patch
    {
        private static void Postfix(TextsDialog __instance)
        {
            if (MessengerPlugin._useChatLog.Value is MessengerPlugin.Toggle.Off) return;
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var message in m_chatLog)
            {
                stringBuilder.Append(message + "\n");
            }
            __instance.m_texts.Insert(0, new TextsDialog.TextInfo(MessengerPlugin._chatLogTitle.Value, stringBuilder.ToString()));
        }
    }
}