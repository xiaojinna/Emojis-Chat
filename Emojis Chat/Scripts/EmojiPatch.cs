using Il2CppSystem.Runtime.Remoting.Messaging;

namespace EmojisChat;

[HarmonyPatch]
static class EmojiPatch
{
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Start))]
    [HarmonyPostfix]
    static void TextBoxStart(TextBoxTMP __instance)
    {
        __instance.allowAllCharacters = true;
        __instance.AllowSymbols = true;
        __instance.AllowEmail = true;
        if (!NebulaAddon.AllAddons.Any(addon => addon.Id == "Plan17ResourcesPlana"))
        {
            __instance.AllowPaste = true;
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
    [HarmonyPostfix]
    static void ChatAwake(ChatController __instance)
    {
        EmojiTextBox.CreateEmojiButton();
    }

    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.Awake))]
    [HarmonyPostfix]
    static void FreeChatInputFieldAwakePostfix(FreeChatInputField __instance)
    {
        var textArea = __instance.textArea;
        if (textArea.outputText != null) EmojiTextBox.SetupTextForEmoji(textArea.outputText);
        if (textArea.placeholderText != null) EmojiTextBox.SetupTextForEmoji(textArea.placeholderText);
    }

    [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
    [HarmonyPostfix]
    static void ChatBubbleSetTextPostfix(ChatBubble __instance, ref string chatText)
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
    static void ChatNotificationSetUpPostfix(ChatNotification __instance, PlayerControl sender, ref string text)
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

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    [HarmonyPostfix]
    static void OnLobbyStartPostfix(LobbyBehaviour __instance)
    {
        try
        {
            NebulaManager.Instance.StartDelayAction(1.5f, () => 
            {
                var player = PlayerControl.LocalPlayer;
                var originName = player.name;
                player.SetName("Emojis Chat");
                HudManager.Instance.Chat.AddChat(player, Language.Translate("emojis.welcome"));
                player.SetName(originName);
            });

        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }
}