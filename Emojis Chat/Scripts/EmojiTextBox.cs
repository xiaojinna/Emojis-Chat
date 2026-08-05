using HarmonyLib;
using Il2CppSystem.Runtime.Remoting.Messaging;
using Nebula.Modules.Logging;
using System;
using System.Collections;
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

public static class EmojiTextBox
{
    public static TMP_FontAsset emojiFont;
    public static bool isInitialized = false;

    public static readonly Regex emojiRegex = new Regex(
        @";([0-9a-fA-F]+);",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );

    public static GameObject emojiSelectorInstance;
    public static bool isCreatingSelector = false;

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
        if (mainFont != null)
        {
            AddEmojiAsFallback(mainFont);
        }

        isInitialized = true;
    }

    public static TMP_FontAsset GetMainFont()
    {
        try
        {
            if (HudManager.Instance != null && HudManager.Instance.IntroPrefab != null)
            {
                return HudManager.Instance.IntroPrefab.ImpostorText.font;
            }

            var anyText = Object.FindObjectOfType<TMP_Text>();
            if (anyText != null)
            {
                return anyText.font;
            }
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
            if (mainFont.fallbackFontAssetTable == null)
            {
                mainFont.fallbackFontAssetTable = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
            }

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

                if (Assets.IsEmoji(codePoint))
                {
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
            // 备用方案：简单遍历
            foreach (char c in text)
            {
                if (Assets.IsEmoji(c))
                {
                    return true;
                }
            }
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
            button.OnClick.AddListener(new Action(OpenWindow));
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }

    public static bool IsDestroyed(GameObject obj)
    {
        return obj == null || ReferenceEquals(obj, null);
    }

    public static void InsertEmoji(int code)
    {
        try
        {
            var box = HudManager.Instance.Chat?.freeChatField?.textArea;
            if (box != null)
            {
                box.SetText(box.text + ";" + code.ToString("X") + ";");
            }
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
                    if (code <= 0x10FFFF)
                    {
                        // 即使不在Emoji列表中，也尝试转换（可能是用户自定义）
                        return char.ConvertFromUtf32(code);
                    }
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
            if (text.font.fallbackFontAssetTable == null)
            {
                text.font.fallbackFontAssetTable = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
            }

            bool exists = false;
            foreach (var f in text.font.fallbackFontAssetTable)
            {
                if (f == emojiFont)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                text.font.fallbackFontAssetTable.Add(emojiFont);
            }
            text.SetAllDirty();
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e.Message}");
        }
    }

    public static void OpenWindow()
    {
        EmojiPagination.OpenWindow();
    }

    /*public static void OpenWindow()
    {
        var window = MetaScreen.GenerateWindow(new Vector2(7.5f, 2.5f), AmongUsLLImpl.HudManagerInstance.transform, new Vector3(0, 1.1f, 0f), true, true, true);

        TextAttribute ButtonAttr = new(GUI.API.GetAttribute(AttributeAsset.OptionsButtonLonger))
        {
            Size = new(0.33f, 0.33f)
        };

        IEnumerator CoCloseOnResult()
        {
            while (HudManager.Instance.Chat.IsOpenOrOpening) yield return null;
            window.CloseScreen();
        }

        window.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());

        var emojiList = Assets.GetEmojiList().ToList();

        // 构建动态内容列表
        var dynamicContents = new List<GUIScrollDynamicInnerContent>();

        for (int i = 0; i < emojiList.Count; i += 14)
        {
            var rowEmojis = emojiList.Skip(i).Take(14).ToList();

            // 每行作为一个水平布局
            var rowWidget = GUI.API.HorizontalHolder(
                GUIAlignment.Center,
                rowEmojis.Select(emoji =>
                {
                    string emojiText = char.ConvertFromUtf32(emoji);
                    return new GUIModernButton(GUIAlignment.Center, ButtonAttr, GUI.API.RawTextComponent(emojiText))
                    {
                        OnClick = _ =>
                        {
                            window.CloseScreen();
                            var textBox = HudManager.Instance.Chat.freeChatField.textArea;
                            textBox.SetText(textBox.text + ";" + emoji.ToString("X") + ";");
                        },
                        SelectedDefault = false,
                        WithCheckMark = false,
                        EmphasizeOnSelected = true,
                        BlockSelectingOnClicked = true
                    };
                })
            );

            dynamicContents.Add(new GUIScrollDynamicInnerContent(
                GUIAlignment.Center,
                () => rowWidget,
                0.4f
            ));
        }

        // 使用虚拟化滚动视图
        var scrollView = new GUIScrollDynamicView(
            GUIAlignment.Center,
            new Vector2(7.4f, 2.4f),
            dynamicContents
        );

        var wrapped = new MetaWidgetOld.WrappedWidget(scrollView);
        window.SetWidget(wrapped);
    }*/
}