using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Messenger.Modifications;

public static class Notifications
{
    private static GameObject m_notificationList = null!;
    private static GameObject m_messageObject = null!;
    private static readonly Queue<GameObject> m_destroyQueue = new();
    private static float m_destroyQueueTimer;
    
    private static bool ShowText(MessageHud __instance, MessageHud.MessageType type, string text, int amount, Sprite? icon)
    {
        if (type is not MessageHud.MessageType.TopLeft) return true;
        if (Hud.IsUserHidden()) return true;
        text = Localization.instance.Localize(text);
        MessageHud.MsgData match = __instance.m_msgQeue.ToList().Find(x => x.m_text == text);
        if (match == null)
        {
            __instance.m_msgQeue.Enqueue(new MessageHud.MsgData()
            {
                m_icon = icon,
                m_text = text,
                m_amount = amount
            });
            __instance.AddLog(text);
        }
        else
        {
            match.m_amount += amount;
        }
        return false;
    }

    private static void UpdateMessage(MessageHud __instance, float dt)
    {
        UpdateCenterMessageFade(__instance);
        UpdateDestroy(__instance, dt);
        __instance.m_msgQueueTimer += dt;
        if (__instance.m_msgQueueTimer < 1f) return;
        __instance.m_msgQueueTimer = 0.0f;
        
        if (__instance.m_msgQeue.Count <= 0) return;
        GameObject message = Object.Instantiate(m_messageObject, m_notificationList.transform);
        if (!message.TryGetComponent(out Messenger component)) return;
            
        MessageHud.MsgData msgData = __instance.m_msgQeue.Dequeue();
            
        component.TMPText.text = msgData.m_amount > 0 ? $"{msgData.m_text} x{msgData.m_amount}" : msgData.m_text;
        component.Image.sprite = msgData.m_icon;
        component.Image.color = msgData.m_icon == null || MessengerPlugin._showIcon.Value is MessengerPlugin.Toggle.Off ? Color.clear : Color.white;
        m_destroyQueue.Enqueue(message);
        if (MessengerPlugin._crossFade.Value is MessengerPlugin.Toggle.Off) return;
        component.TMPText.CrossFadeAlpha(0f, MessengerPlugin._crossFadeDuration.Value, false);
        component.Image.CrossFadeAlpha(0f, MessengerPlugin._crossFadeDuration.Value, false);
    }
    
    private static void UpdateDestroy(MessageHud __instance, float dt)
    {
        m_destroyQueueTimer += dt;
        if (m_destroyQueueTimer < 1f) return;
        m_destroyQueueTimer = 0.0f;
        if (m_destroyQueue.Count <= 0) return;
        if (m_destroyQueue.Count < MessengerPlugin._amount.Value && __instance.m_msgQeue.Count > 0) return;
        GameObject obj = m_destroyQueue.Dequeue();
        Object.Destroy(obj);
    }

    private static void UpdateCenterMessageFade(MessageHud __instance)
    {
        if (__instance._crossFadeTextBuffer.Count <= 0) return;
        MessageHud.CrossFadeText crossFadeText = __instance._crossFadeTextBuffer[0];
        __instance._crossFadeTextBuffer.RemoveAt(0);
        crossFadeText.text.CrossFadeAlpha(crossFadeText.alpha, crossFadeText.time, true);
    }
    
    private static void CreateNotificationPanel(MessageHud __instance)
    {
        m_notificationList = new GameObject("notifications") { layer = 5 };
        Object.DontDestroyOnLoad(m_notificationList);
        RectTransform rect = m_notificationList.AddComponent<RectTransform>();
        if (__instance.transform.parent is not RectTransform transform) return;
        rect.position = MessengerPlugin._notificationPos.Value;

        MessengerPlugin._notificationPos.SettingChanged += (sender, args) =>
        {
            rect.position = MessengerPlugin._notificationPos.Value;
        };
        rect.SetParent(transform);
        rect.sizeDelta = new Vector2(628, 50);
        var group = m_notificationList.AddComponent<VerticalLayoutGroup>();
        group.spacing = MessengerPlugin._spacing.Value;
        MessengerPlugin._spacing.SettingChanged += (sender, args) =>
        {
            group.spacing = MessengerPlugin._spacing.Value;
        };
    }

    private static void ModifyTopLeftMessage(MessageHud __instance)
    {
        Transform TopLeftMessage = __instance.m_messageText.transform.parent;
        GameObject gameObject = TopLeftMessage.gameObject;
        if (TopLeftMessage == null) return;
        var clone = Object.Instantiate(gameObject, MessengerPlugin.m_root.transform, false);
        HorizontalLayoutGroup group = clone.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.reverseArrangement = true;
        group.childControlHeight = false;
        group.childControlWidth = false;
        group.childScaleHeight = false;
        group.childScaleWidth = false;
        group.childForceExpandHeight = true;
        group.childForceExpandWidth = true;
        group.spacing = MessengerPlugin._msgSpacing.Value;
        MessengerPlugin._msgSpacing.SettingChanged += (sender, args) =>
        {
            group.spacing = MessengerPlugin._msgSpacing.Value;
        };
        var message = clone.transform.Find("Message");
        var icon = clone.transform.Find("MessageIcon");
            
        if (!message.TryGetComponent(out RectTransform messageRect)) return;
        if (!icon.TryGetComponent(out RectTransform iconRect)) return;
        iconRect.sizeDelta = new Vector2(MessengerPlugin._iconSize.Value,MessengerPlugin._iconSize.Value);
        MessengerPlugin._iconSize.SettingChanged += (sender, args) =>
        {
            iconRect.sizeDelta = new Vector2(MessengerPlugin._iconSize.Value,MessengerPlugin._iconSize.Value);
        };
        
        if (!message.TryGetComponent(out TMP_Text tmpText)) return;
        tmpText.text = "";
        if (!icon.TryGetComponent(out Image image)) return;
        image.preserveAspect = true;
        Messenger messenger = clone.AddComponent<Messenger>();
        messenger.TMPText = tmpText;
        messenger.Image = image;
        m_messageObject = clone;
    }

    public class Messenger : MonoBehaviour
    {
        public TMP_Text TMPText = null!;
        public Image Image = null!;

        public void Awake()
        {
            TMPText = GetComponentInChildren<TMP_Text>();
            Image = GetComponentInChildren<Image>();
        }
    }
    
    [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.UpdateMessage))]
    private static class MessageHud_UpdateMessage_Patch
    {
        private static bool Prefix(MessageHud __instance, float dt)
        {
            if (MessengerPlugin._useNotifications.Value is MessengerPlugin.Toggle.Off) return true;
            UpdateMessage(__instance, dt);
            return false;
        }
    }

    [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.ShowMessage))]
    private static class MessageHud_ShowMessage_Patch
    {
        private static bool Prefix(MessageHud __instance, MessageHud.MessageType type, string text, int amount, Sprite? icon)
        {
            if (MessengerPlugin._useNotifications.Value is MessengerPlugin.Toggle.Off) return true;
            return ShowText(__instance, type, text, amount, icon);
        }
    }

    [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.Awake))]
    private static class MessageHud_Awake_Patch
    {
        private static void Postfix(MessageHud __instance)
        {
            ModifyTopLeftMessage(__instance);
            CreateNotificationPanel(__instance);
        }
    }
}