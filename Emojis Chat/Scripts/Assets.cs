using Cpp2IL.Core.Extensions;
using Nebula.Modules.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EmojisChat;

public static class Assets
{
    private static TMP_FontAsset emojiFont;
    private static bool isEmojiLoaded = false;

    private static readonly HashSet<int> emojiSet = new();
    private static bool isEmojiListLoaded = false;

    public static int EmojiCount => emojiSet.Count;

    public static TMP_FontAsset GetEmojiFont()
    {
        if (isEmojiLoaded && emojiFont != null) return emojiFont;
        if (isEmojiLoaded) return null;

        try
        {
            var resource = NebulaAPI.AddonAsset.GetResource("emoji_font");
            if (resource == null)
            {
                NebulaLogger.Instance.Error("emoji_font does not exist");
                isEmojiLoaded = true;
                return null;
            }

            byte[] bytes = resource.AsStream().ReadBytes();
            if (bytes == null || bytes.Length == 0)
            {
                NebulaLogger.Instance.Error("Bundle Is Null");
                isEmojiLoaded = true;
                return null;
            }

            var bundle = AssetBundle.LoadFromMemory(bytes);
            if (bundle == null)
            {
                NebulaLogger.Instance.Error("AssetBundle Failed to load");
                isEmojiLoaded = true;
                return null;
            }

            emojiFont = bundle.LoadAsset<TMP_FontAsset>("Assets/Font/NotoEmoji SDF.asset");
            if (emojiFont == null)
            {
                NebulaLogger.Instance.Error("TMP_FontAsset Failed to load");
                isEmojiLoaded = true;
                return null;
            }

            emojiFont.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            isEmojiLoaded = true;

            NebulaLogger.Instance.Message("Emoji Font Loaded successfully");
            return emojiFont;
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e}");
            isEmojiLoaded = true;
            return null;
        }
    }

    public static void InitializeEmojiList()
    {
        if (isEmojiListLoaded) return;

        try
        {
            var resource = NebulaAPI.AddonAsset.GetResource("EmojiEncoding.txt");
            if (resource == null)
            {
                NebulaLogger.Instance.Warning("EmojiEncoding.txt does not exist");
                LoadDefaultEmojis();
                isEmojiListLoaded = true;
                return;
            }

            string text;
            using (var reader = new StreamReader(resource.AsStream()))
            {
                text = reader.ReadToEnd();
            }

            if (string.IsNullOrEmpty(text))
            {
                NebulaLogger.Instance.Warning("EmojiEncoding.txt is Null");
                LoadDefaultEmojis();
                isEmojiListLoaded = true;
                return;
            }

            emojiSet.Clear();
            var items = text.Split(',');
            int parsedCount = 0;

            foreach (var item in items)
            {
                var trimmed = item.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (ParseRange(trimmed))
                {
                    parsedCount++;
                }
            }

            isEmojiListLoaded = true;
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e}");
            LoadDefaultEmojis();
            isEmojiListLoaded = true;
        }
    }

    private static void LoadDefaultEmojis()
    {
        emojiSet.Clear();
        // 基本Emoji范围（作为备用）
        AddRange(0x2600, 0x27BF);
        AddRange(0x1F300, 0x1F6FF);
        AddRange(0x1F900, 0x1F9FF);
        AddRange(0x1FA70, 0x1FAF8);
    }

    private static bool ParseRange(string value)
    {
        try
        {
            if (value.Contains("-"))
            {
                var range = value.Split('-');
                if (range.Length == 2)
                {
                    int start = Convert.ToInt32(range[0].Trim(), 16);
                    int end = Convert.ToInt32(range[1].Trim(), 16);
                    AddRange(start, end);
                    return true;
                }
            }
            else
            {
                int code = Convert.ToInt32(value, 16);
                emojiSet.Add(code);
                return true;
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Warning($"{e.Message}");
        }
        return false;
    }

    private static void AddRange(int start, int end)
    {
        if (start < 0 || end > 0x10FFFF || start > end) return;

        for (int i = start; i <= end; i++)
        {
            emojiSet.Add(i);
        }
    }

    /// <summary>
    /// 检测指定码点是否为Emoji
    /// </summary>
    public static bool IsEmoji(int codePoint)
    {
        if (!isEmojiListLoaded)
        {
            InitializeEmojiList();
        }
        return emojiSet.Contains(codePoint);
    }

    /// <summary>
    /// 检测指定字符是否为Emoji
    /// </summary>
    public static bool IsEmoji(char c)
    {
        return IsEmoji((int)c);
    }

    /// <summary>
    /// 获取所有Emoji码点（只读）
    /// </summary>
    public static IReadOnlyCollection<int> GetEmojiList()
    {
        if (!isEmojiListLoaded)
        {
            InitializeEmojiList();
        }
        return emojiSet;
    }
}