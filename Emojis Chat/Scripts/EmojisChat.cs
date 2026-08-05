using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Runtime.Remoting.Messaging;
using Nebula.Modules.Logging;
using NPinyin;
using Rewired.UI.ControlMapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

/// <summary>
/// 滚轮事件监听组件 - 需要注册到Il2Cpp
/// </summary>
public class EmojiScrollListener : MonoBehaviour
{
    // 静态构造函数，在类首次使用时注册
    static EmojiScrollListener()
    {
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<EmojiScrollListener>();
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"Failed to register EmojiScrollListener: {e.Message}");
        }
    }

    private MetaScreen window;
    private Action<float> onScroll;
    private float scrollAccumulator = 0f;
    private const float SCROLL_THRESHOLD = 0.1f;
    private bool isDestroyed = false;

    public void Setup(MetaScreen window, Action<float> onScroll)
    {
        this.window = window;
        this.onScroll = onScroll;
        isDestroyed = false;
    }

    void Update()
    {
        try
        {
            // 检查窗口是否有效
            if (isDestroyed || window == null || window.gameObject == null)
            {
                DestroyImmediate(this);
                return;
            }

            // 检测鼠标滚轮输入
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // 累积滚轮值，防止一次滚动触发多次
                scrollAccumulator += scroll;

                if (Mathf.Abs(scrollAccumulator) >= SCROLL_THRESHOLD)
                {
                    int direction = scrollAccumulator > 0 ? 1 : -1; // 向上滚动 -> 上一页
                    onScroll?.Invoke(direction);
                    scrollAccumulator = 0f;
                }
            }
            else
            {
                // 缓慢释放累积值
                scrollAccumulator *= 0.9f;
                if (Mathf.Abs(scrollAccumulator) < 0.001f) scrollAccumulator = 0f;
            }
        }
        catch (Exception e)
        {
            // 防止异常导致游戏崩溃
            NebulaLogger.Instance.Warning($"EmojiScrollListener Update error: {e.Message}");
            isDestroyed = true;
            DestroyImmediate(this);
        }
    }

    void OnDestroy()
    {
        isDestroyed = true;
        window = null;
        onScroll = null;
    }
}

// 1. 定义一个缓存类
public class EmojiSearchItem
{
    public int EmojiCode { get; set; }
    public string HexCode { get; set; }
    public string LocalizedName { get; set; }
    public string PinyinInitials { get; set; }  // 拼音首字母
    public string FullPinyin { get; set; }       // 完整拼音（可选）
}
public static class EmojiPagination
{
    // 每页行数
    public const int RowsPerPage = 4;
    // 每行Emoji数量
    public const int EmojisPerRow = 12;
    // 每页Emoji数量
    public const int EmojisPerPage = RowsPerPage * EmojisPerRow;

    // 当前页码
    private static int currentPage = 0;
    // 总页数
    private static int totalPages = 0;

    // 当前窗口引用
    private static MetaScreen currentWindow = null;

    // 所有Emoji列表
    private static List<int> emojiList = null;
    // 当前筛选后的Emoji列表
    private static List<int> filteredEmojiList = null;

    // 按钮属性缓存
    private static TextAttribute buttonAttr = null;

    // 页码输入框引用
    private static GUITextField pageInputField = null;

    // 滚轮监听器引用
    private static EmojiScrollListener scrollListener = null;

    // 是否启用滚轮
    public static bool EnableScrollWheel { get; set; } = true;

    // 确保EmojiScrollListener已注册
    private static bool isListenerRegistered = false;

    private static string EmojiCode = "";

    private static GUIWidget? ContentWidget = null;

    // 当前搜索关键词
    private static string currentSearchKeyword = "";

    static EmojiPagination()
    {
        try
        {
            // 预注册，确保类在Il2Cpp中可用
            ClassInjector.RegisterTypeInIl2Cpp<EmojiScrollListener>();
            isListenerRegistered = true;
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"Failed to register EmojiScrollListener in static constructor: {e.Message}");
            isListenerRegistered = false;
        }
    }

    /// <summary>
    /// 打开Emoji选择器
    /// </summary>
    public static void OpenWindow()
    {
        // 防止重复打开
        if (currentWindow != null && !currentWindow.IsDestroyed())
        {
            return;
        }

        // 初始化数据
        emojiList = Assets.GetEmojiList().ToList();
        filteredEmojiList = new List<int>(emojiList);
        totalPages = Mathf.CeilToInt((float)filteredEmojiList.Count / EmojisPerPage);
        currentPage = 0;
        currentWindow = null;
        pageInputField = null;
        scrollListener = null;
        currentSearchKeyword = "";

        // 创建按钮属性
        buttonAttr = new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsButtonLonger))
        {
            Size = new Virial.Compat.Size(0.33f, 0.33f)
        };

        // 生成窗口（稍微加宽以容纳搜索框）
        var window = MetaScreen.GenerateWindow(
            new Vector2(7.1f, 2.3f),  // 加宽以容纳左侧搜索框
            AmongUsLLImpl.HudManagerInstance.transform,
            new Vector3(0, 1.2f, 0f),
            true,
            true,
            true
        );

        currentWindow = window;

        // 添加滚轮监听器 - 使用安全方法
        if (EnableScrollWheel)
        {
            AddScrollListener(window);
        }

        // 自动关闭协程（保留原有逻辑）
        window.StartCoroutine(CoCloseOnResult(window).WrapToIl2Cpp());

        // 构建内容
        BuildPageContent(window);
    }

    /// <summary>
    /// 安全添加滚轮监听器
    /// </summary>
    private static void AddScrollListener(MetaScreen window)
    {
        try
        {
            // 确保类已注册
            if (!isListenerRegistered)
            {
                try
                {
                    ClassInjector.RegisterTypeInIl2Cpp<EmojiScrollListener>();
                    isListenerRegistered = true;
                }
                catch (Exception e)
                {
                    NebulaLogger.Instance.Error($"Failed to register EmojiScrollListener: {e.Message}");
                    return;
                }
            }

            // 尝试使用反射方式添加组件
            scrollListener = window.gameObject.GetComponent<EmojiScrollListener>();
            if (scrollListener == null)
            {
                // 使用 Activator 创建实例
                scrollListener = window.gameObject.AddComponent<EmojiScrollListener>();
            }

            if (scrollListener != null)
            {
                scrollListener.Setup(window, direction =>
                {
                    if (direction < 0)
                        NextPage(window);
                    else
                        PreviousPage(window);
                });
                NebulaLogger.Instance.Message("EmojiScrollListener added successfully");
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"Failed to add scroll listener: {e.Message}");
            // 滚轮功能不可用，但不影响其他功能
            scrollListener = null;
        }
    }

    /// <summary>
    /// 执行搜索过滤
    /// </summary>
    private static void PerformSearch(string keyword)
    {
        var searchCache = emojiList.Select(e =>
        {
            
            var hex = e.ToString("X");
            var name = Language.Translate($"emojichat.button.{hex}");

            return new EmojiSearchItem
            {
                EmojiCode = e,
                HexCode = hex, // 预先转为 "1F60A"
                LocalizedName = name.Replace(" ", "").ToUpperInvariant(), // 预先翻译
                PinyinInitials = Pinyin.GetInitials(name).Replace(" ", "").ToUpperInvariant(),  // "笑脸" → "XL"
                FullPinyin = Pinyin.GetPinyin(name).Replace(" ", "").ToUpperInvariant()
            };
        }).ToList();

        currentSearchKeyword = keyword?.Trim() ?? "";

        if (string.IsNullOrEmpty(currentSearchKeyword))
        {
            // 清空搜索，显示所有Emoji
            filteredEmojiList = new List<int>(emojiList);
        }
        else
        {
            string keywordUpper = currentSearchKeyword.ToUpperInvariant();

            filteredEmojiList = searchCache
                .Where(item => item.HexCode.Contains(keywordUpper, StringComparison.Ordinal) ||
                               item.LocalizedName.Contains(keywordUpper, StringComparison.Ordinal) ||
                               item.PinyinInitials.Contains(keywordUpper, StringComparison.Ordinal) ||
                               item.FullPinyin.Contains(keywordUpper, StringComparison.Ordinal))
                .Select(item => item.EmojiCode).ToList();

        }

        // 重置到第一页
        currentPage = 0;
        totalPages = Mathf.CeilToInt((float)filteredEmojiList.Count / EmojisPerPage);
        if (totalPages == 0) totalPages = 1;

        // 更新页码输入框
        UpdatePageInputField();

        // 重建页面
        if (currentWindow != null && !currentWindow.IsDestroyed())
        {
            BuildPageContent(currentWindow);
        }
    }

    /// <summary>
    /// 构建指定页的内容
    /// </summary>
    private static void BuildPageContent(MetaScreen window)
    {
        if (window == null) return;

        // 清空旧内容 - 保留 BorderLine 和 EmojiScrollListener
        try
        {
            var children = window.gameObject.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                if (child == window.transform) continue;
                var go = child.gameObject;
                if (go == null) continue;
                if (go.name != "BorderLine" && go.name != "EmojiScrollListener(Clone)" && go.name != "EmojiScrollListener")
                {
                    GameObject.Destroy(go);
                }
            }
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Warning($"Error clearing children: {e.Message}");
        }

        // 计算当前页的Emoji范围（使用筛选后的列表）
        int startIndex = currentPage * EmojisPerPage;
        int endIndex = Mathf.Min(startIndex + EmojisPerPage, filteredEmojiList.Count);
        int actualCount = endIndex - startIndex;

        if (actualCount <= 0 && filteredEmojiList.Count > 0)
        {
            // 如果超出范围，回到第一页
            currentPage = 0;
            BuildPageContent(window);
            return;
        }

        // 构建所有行
        var rowWidgets = new List<Virial.Media.GUIWidget>();

        for (int row = 0; row < RowsPerPage; row++)
        {
            int rowStart = startIndex + row * EmojisPerRow;
            int rowEnd = Mathf.Min(rowStart + EmojisPerRow, endIndex);

            if (rowStart >= endIndex) break;

            var rowEmojis = filteredEmojiList.Skip(rowStart).Take(rowEnd - rowStart).ToList();

            // 构建一行Emoji按钮
            var rowWidget = BuildEmojiRow(rowEmojis);
            rowWidgets.Add(rowWidget);
        }

        // 如果没有任何Emoji，显示提示信息
        if (rowWidgets.Count == 0)
        {
            var emptyText = NebulaAPI.GUI.RawText(
                GUIAlignment.Center,
                new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.CenteredBold))
                {
                    Size = new Virial.Compat.Size(4f, 0.5f)
                },
                Language.Translate("emojichat.ui.noResults")
            );
            rowWidgets.Add(emptyText);
        }

        // 页码输入框（在翻页区域上方）
        var pageInput = new GUITextField(GUIAlignment.Center, new Virial.Compat.Size(0.5f, 0.3f))
        {
            HintText = Language.Translate("emojichat.ui.pageInput"),
            DefaultText = (currentPage + 1).ToString(),
            FontSize = 1.8f,
            MaxLines = 1,
            WithMaskMaterial = true,
            GainFocus = false,
            // 输入过滤：只允许数字
            TextPredicate = c => char.IsDigit(c),
            // 回车跳转 - Predicate<string> 需要返回 bool
            EnterAction = text =>
            {
                if (!string.IsNullOrEmpty(text))
                {
                    TryJumpToPage(text);
                    return true;
                }
                return false;
            },
            // 失去焦点 - Action<string> 不需要返回值
            LostFocusAction = text =>
            {
                if (!string.IsNullOrEmpty(text))
                {
                    TryJumpToPage(text);
                }
            }
        };

        // 保存输入框引用以便后续更新
        pageInputField = pageInput;

        // 翻页按钮区域（右侧）
        var pageControls = NebulaAPI.GUI.HorizontalHolder(
            GUIAlignment.BottomLeft,

            // 上一页按钮
            new GUIModernButton(GUIAlignment.Center, buttonAttr, NebulaAPI.GUI.RawTextComponent("▲"))
            {
                OnClick = _ => PreviousPage(window),
                SelectedDefault = false
            },

            // 页码输入框
            pageInput,

            // 页码总数显示
            NebulaAPI.GUI.RawText(
                GUIAlignment.Center,
                new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsValue))
                {
                    Size = new Virial.Compat.Size(1f, 0.33f)
                },
                Language.Translate("emojichat.ui.totalPage").Replace("%NUM%", totalPages.ToString())
            ),

            // 下一页按钮
            new GUIModernButton(GUIAlignment.Center, buttonAttr, NebulaAPI.GUI.RawTextComponent("▼"))
            {
                OnClick = _ => NextPage(window),
                SelectedDefault = false
            },

            NebulaAPI.GUI.RawText(GUIAlignment.Center, new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsValue))
            {
                Size = new Virial.Compat.Size(1f, 0.33f)
            }, EmojiCode)
        );
        rowWidgets.Add(pageControls);

        // 垂直布局所有行
        ContentWidget = NebulaAPI.GUI.VerticalHolder(
            GUIAlignment.Bottom,
            rowWidgets
        );

        // 创建主容器：搜索框 + 内容 + 翻页按钮
        var mainLayout = BuildMainLayout(window, ContentWidget);

        // 创建适配器并设置到窗口
        var wrapped = new MetaWidgetOld.WrappedWidget(mainLayout);
        window.SetWidget(wrapped);

        // 更新翻页按钮状态
        UpdatePaginationButtons(window);

        // 更新页码输入框
        UpdatePageInputField();
    }

    /// <summary>
    /// 构建一行Emoji
    /// </summary>
    private static Virial.Media.GUIWidget BuildEmojiRow(List<int> emojis)
    {
        var buttons = emojis.Select(emoji =>
        {
            string emojiText = char.ConvertFromUtf32(emoji);
            return new GUIModernButton(
                GUIAlignment.Center,
                buttonAttr,
                NebulaAPI.GUI.RawTextComponent(emojiText)
            )
            {
                OnClick = _ =>
                {
                    var textBox = HudManager.Instance.Chat?.freeChatField?.textArea;
                    if (textBox != null)
                    {
                        textBox.SetText(textBox.text + ";" + emoji.ToString("X") + ";");
                    }
                },
                OnMouseOver = _ =>
                {
                    if (currentWindow != null && ContentWidget != null && EmojiCode != Language.Translate("emojichat.button." + emoji.ToString("X")))
                    {
                        EmojiCode = Language.Translate("emojichat.button." + emoji.ToString("X"));
                        BuildPageContent(currentWindow);
                    }
                },
                OnMouseOut = _ =>
                {
                    if (currentWindow != null && ContentWidget != null)
                    {
                        EmojiCode = "";
                        BuildPageContent(currentWindow);
                    }
                },
                SelectedDefault = false,
                WithCheckMark = false,
                EmphasizeOnSelected = true,
                BlockSelectingOnClicked = true
            };
        });

        return NebulaAPI.GUI.HorizontalHolder(
            GUIAlignment.Center,
            buttons
        );
    }

    /// <summary>
    /// 构建左侧搜索区域
    /// </summary>
    private static Virial.Media.GUIWidget BuildSearchArea()
    {
        // 搜索输入框
        var searchInput = new GUITextField(
            GUIAlignment.Center,
            new Virial.Compat.Size(1.2f, 0.35f)
        )
        {
            HintText = Language.Translate("emojichat.ui.searchHint"),
            DefaultText = currentSearchKeyword,
            FontSize = 1.5f,
            MaxLines = 1,
            WithMaskMaterial = true,
            GainFocus = false,
            // 输入过滤：只允许十六进制字符（数字和A-F）
            //TextPredicate = c => true,
            // 回车执行搜索
            EnterAction = text =>
            {
                PerformSearch(text);
                return true;
            },
            // 失去焦点时自动搜索
            LostFocusAction = text =>
            {
                PerformSearch(text);
            }
        };

        // 垂直排列搜索区域
        return NebulaAPI.GUI.VerticalHolder(
            GUIAlignment.TopLeft,
            // 搜索标题
            searchInput
        );
    }

    /// <summary>
    /// 构建主布局（搜索框 + 内容 + 翻页按钮）
    /// </summary>
    private static Virial.Media.GUIWidget BuildMainLayout(MetaScreen window, Virial.Media.GUIWidget content)
    {
        // 翻页按钮属性
        var pageButtonAttr = new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsButton))
        {
            Size = new Virial.Compat.Size(0.6f, 0.25f)
        };

        // 行间距
        const float rowGap = 0.05f;

        // 构建垂直内容
        var verticalContent = NebulaAPI.GUI.VerticalHolder(
            GUIAlignment.Center,
            content,
            NebulaAPI.GUI.Margin(new FuzzySize(0f, rowGap * 2))
        );

        // 左侧搜索区域
        var searchArea = BuildSearchArea();

        // 水平布局：搜索框 | 内容 | 翻页按钮
        return NebulaAPI.GUI.HorizontalHolder(
            GUIAlignment.TopRight,
            searchArea,
            NebulaAPI.GUI.Margin(new FuzzySize(0.1f, 0f)),
            verticalContent
        );
    }

    /// <summary>
    /// 尝试跳转到指定页码
    /// </summary>
    private static void TryJumpToPage(string input)
    {
        if (string.IsNullOrEmpty(input)) return;

        // 尝试解析数字
        if (int.TryParse(input, out int targetPage))
        {
            // 转换为0基索引
            targetPage = targetPage - 1;

            // 限制在有效范围内
            targetPage = Mathf.Clamp(targetPage, 0, totalPages - 1);

            // 如果页码有变化，则跳转
            if (targetPage != currentPage)
            {
                currentPage = targetPage;
                if (currentWindow != null && !currentWindow.IsDestroyed())
                {
                    BuildPageContent(currentWindow);
                }
            }
            else
            {
                // 如果页码相同，只更新输入框显示
                UpdatePageInputField();
            }
        }
        else
        {
            // 无效输入，恢复显示当前页码
            UpdatePageInputField();
        }
    }

    /// <summary>
    /// 更新页码输入框的显示
    /// </summary>
    private static void UpdatePageInputField()
    {
        if (pageInputField == null) return;

        // 通过Artifact获取TextField并更新文本
        var artifact = pageInputField.Artifact;
        if (artifact != null && artifact.Values.Count > 0)
        {
            var textField = artifact.Values[0];
            if (textField != null)
            {
                // 使用 SetText 方法
                try
                {
                    var setTextMethod = textField.GetType().GetMethod("SetText");
                    if (setTextMethod != null)
                    {
                        setTextMethod.Invoke(textField, new object[] { (currentPage + 1).ToString() });
                    }
                }
                catch (Exception e)
                {
                    NebulaLogger.Instance.Warning($"Failed to set page text: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 更新翻页按钮状态
    /// </summary>
    private static void UpdatePaginationButtons(MetaScreen window)
    {
        // 由于我们是重新构建整个页面，状态已经是最新的
        // 这里可以留空，或者添加额外的状态更新逻辑
    }

    /// <summary>
    /// 上一页
    /// </summary>
    private static void PreviousPage(MetaScreen window)
    {
        if (currentPage > 0)
        {
            currentPage--;
            BuildPageContent(window);
        }
    }

    /// <summary>
    /// 下一页
    /// </summary>
    private static void NextPage(MetaScreen window)
    {
        if (currentPage < totalPages - 1)
        {
            currentPage++;
            BuildPageContent(window);
        }
    }

    /// <summary>
    /// 自动关闭协程
    /// </summary>
    private static System.Collections.IEnumerator CoCloseOnResult(MetaScreen window)
    {
        while (HudManager.Instance.Chat.IsOpenOrOpening)
        {
            yield return null;
        }

        if (window != null && !window.IsDestroyed())
        {
            window.CloseScreen();
            currentWindow = null;
        }
    }

    /// <summary>
    /// 检查窗口是否已销毁
    /// </summary>
    private static bool IsDestroyed(this MetaScreen window)
    {
        try
        {
            return window == null || window.gameObject == null || ReferenceEquals(window, null);
        }
        catch
        {
            return true;
        }
    }
}