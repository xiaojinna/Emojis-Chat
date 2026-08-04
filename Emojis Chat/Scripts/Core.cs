using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.Logging;
using System;
using System.Reflection;
using HarmonyLib;

namespace EmojisChat;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class Core
{
    private static Harmony harmony;

    static Core()
    {
        Init();
    }

    public static void Init()
    {

        try
        {
            // 先加载Emoji列表
            Assets.InitializeEmojiList();

            // 应用Harmony补丁
            harmony = new Harmony("com.github.xiaojinna.Emoji");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e}");
        }
    }
}