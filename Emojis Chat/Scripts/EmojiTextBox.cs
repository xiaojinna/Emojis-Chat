namespace EmojisChat;

public static class EmojiTextBox
{
    public static TMP_FontAsset emojiFont;
    public static bool isInitialized = false;
    public static readonly Regex emojiRegex = new Regex(
        @";([0-9a-fA-F]+);",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static void InitEmoji()
    {
        if (isInitialized) return;

        Assets.InitializeEmojiList();

        emojiFont = Assets.GetEmojiFont();
        if (emojiFont == null)
        {
            NebulaLogger.Instance.Error("Emoji Font initialization failed");
            isInitialized = true;
            return;
        }

        var mainFont = GetMainFont();
        if (mainFont != null) AddEmojiAsFallback(mainFont);

        isInitialized = true;
    }

    public static TMP_FontAsset GetMainFont()
    {
        try
        {
            if (HudManager.Instance != null && HudManager.Instance.IntroPrefab != null) return HudManager.Instance.IntroPrefab.ImpostorText.font;

            var anyText = Object.FindObjectOfType<TMP_Text>();
            if (anyText != null) return anyText.font;
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }

        return null;
    }

    public static void AddEmojiAsFallback(TMP_FontAsset mainFont)
    {
        if (mainFont == null || emojiFont == null) return;

        try
        {
            mainFont.fallbackFontAssetTable ??= new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
            bool exists = false;
            for (int i = 0; i < mainFont.fallbackFontAssetTable.Count; i++)
            {
                if (mainFont.fallbackFontAssetTable[i] == emojiFont)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                mainFont.fallbackFontAssetTable.Add(emojiFont);
                mainFont.material.SetFloat("_UseGradientScale", 1f);
                mainFont.material.EnableKeyword("_USE_GRADIENT_SCALE");
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }

    public static void SetupTextForEmoji(TMP_Text text)
    {
        if (text == null) return;

        InitEmoji();

        if (emojiFont == null) return;

        var currentFont = text.font;
        if (currentFont == null) return;

        if (currentFont != emojiFont)
        {
            AddEmojiAsFallback(currentFont);
        }

        text.SetAllDirty();
        text.SetVerticesDirty();
        text.SetLayoutDirty();
    }

    /// <summary>
    /// 检测字符串是否包含Emoji（正确处理代理对）
    /// </summary>
    public static bool ContainsEmoji(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        try
        {
            // 使用文本元素迭代器，正确处理代理对（如 U+1FAF8）
            var elementEnumerator = StringInfo.GetTextElementEnumerator(text);
            while (elementEnumerator.MoveNext())
            {
                string element = elementEnumerator.GetTextElement();
                // 获取完整的Unicode码点
                int codePoint = char.ConvertToUtf32(element, 0);
                if (Assets.IsEmoji(codePoint)) return true;
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }

        return false;
    }

    public static void CreateEmojiButton()
    {
        try
        {
            var chat = HudManager.Instance.Chat;
            var button = Object.Instantiate(chat.openKeyboardButton, chat.openKeyboardButton.transform.parent).GetComponent<PassiveButton>();
            var render = button.GetComponent<SpriteRenderer>();
            render.sprite = NebulaAPI.AddonAsset.GetResource("EmojiButtonHover.png")?.AsImage(140f)?.GetSprite();
            render.size = Vector3.one;
            button.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = NebulaAPI.AddonAsset.GetResource("EmojiButton.png")?.AsImage(100f)?.GetSprite();
            button.gameObject.SetActive(true);
            button.name = "EmojiButton";
            var pos = chat.openKeyboardButton.transform.localPosition;
            pos.y += 2;
            button.transform.localPosition = pos;
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener(new Action(EmojiPagination.OpenWindow));
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }

    public static string ConvertEmoji(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        try
        {
            return emojiRegex.Replace(text, m =>
            {
                try
                {
                    int code = Convert.ToInt32(m.Groups[1].Value, 16);
                    // 即使不在Emoji列表中，也尝试转换（可能是用户自定义）
                    if (code <= 0x10FFFF) return char.ConvertFromUtf32(code);
                    return m.Value;
                }
                catch
                {
                    return m.Value;
                }
            });
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
            return text;
        }
    }

    public static void AddEmojiFont(TMP_Text text)
    {
        if (text == null || text.font == null || emojiFont == null) return;

        try
        {
            if (text.font.fallbackFontAssetTable == null) text.font.fallbackFontAssetTable = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();

            bool exists = false;
            foreach (var f in text.font.fallbackFontAssetTable)
            {
                if (f == emojiFont)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists) text.font.fallbackFontAssetTable.Add(emojiFont);
            text.SetAllDirty();
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }
}