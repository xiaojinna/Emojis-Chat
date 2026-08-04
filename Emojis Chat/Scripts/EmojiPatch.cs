using HarmonyLib;
using Nebula.Modules.Logging;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using Virial.Helpers;
using static Il2CppMono.Security.X509.X520;
using GUI = Nebula.Modules.GUIWidget.NebulaGUIWidgetEngine;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace EmojisChat;

[HarmonyPatch]
public static class EmojiPatch
{
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Start))]
    [HarmonyPostfix]
    public static void TextBoxStart(TextBoxTMP __instance)
    {
        if (__instance == null) return;

        __instance.allowAllCharacters = true;
        __instance.AllowSymbols = true;
        __instance.AllowEmail = true;
        __instance.AllowPaste = true;

        try
        {
            EmojiTextBox.InitEmoji();
            if (EmojiTextBox.emojiFont == null) return;

            EmojiTextBox.AddEmojiFont(__instance.outputText);
            EmojiTextBox.AddEmojiFont(__instance.placeholderText);
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e}");
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
    [HarmonyPostfix]
    public static void ChatAwake(ChatController __instance)
    {
        if (__instance == null || __instance.freeChatField == null) return;

        EmojiTextBox.InitEmoji();

        var textArea = __instance.freeChatField.textArea;
        if (textArea != null)
        {
            if (textArea.outputText != null)
            {
                EmojiTextBox.SetupTextForEmoji(textArea.outputText);
            }

            if (textArea.placeholderText != null)
            {
                EmojiTextBox.SetupTextForEmoji(textArea.placeholderText);
            }
        }

        EmojiTextBox.CreateEmojiButton();
    }

    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.Awake))]
    [HarmonyPostfix]
    public static void FreeChatInputFieldAwakePostfix(FreeChatInputField __instance)
    {
        if (__instance == null || __instance.textArea == null) return;

        EmojiTextBox.InitEmoji();

        var textArea = __instance.textArea;

        if (textArea.outputText != null)
        {
            EmojiTextBox.SetupTextForEmoji(textArea.outputText);
        }

        if (textArea.placeholderText != null)
        {
            EmojiTextBox.SetupTextForEmoji(textArea.placeholderText);
        }
    }

    [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
    [HarmonyPostfix]
    public static void ChatBubbleSetTextPostfix(ChatBubble __instance, [HarmonyArgument(0)] string chatText)
    {
        try
        {
            var convertedText = EmojiTextBox.ConvertEmoji(chatText);

            if (!string.IsNullOrEmpty(convertedText) && EmojiTextBox.ContainsEmoji(convertedText))
            {
                EmojiTextBox.SetupTextForEmoji(__instance.TextArea);
                __instance.TextArea.SetText(convertedText);
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }

    [HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.SetUp))]
    [HarmonyPostfix]
    public static void ChatNotificationSetUpPostfix(ChatNotification __instance, PlayerControl sender, string text)
    {
        try
        {
            var convertedText = EmojiTextBox.ConvertEmoji(text);

            if (!string.IsNullOrEmpty(convertedText) && EmojiTextBox.ContainsEmoji(convertedText))
            {
                EmojiTextBox.SetupTextForEmoji(__instance.chatText);
                __instance.chatText.SetText(convertedText);
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }

}