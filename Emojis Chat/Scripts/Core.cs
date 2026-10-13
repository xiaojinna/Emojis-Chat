using Nebula.Patches;

namespace EmojisChat;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class Core
{
    private static Harmony harmony;

    static Core()
    {
        NebulaLogger.Instance.Message($"Loading [Emojis]");
        harmony = new Harmony("com.github.xiaojinna.Emoji");
        Init();
    }

    public static void Init()
    {
        try
        {
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Assets.InitializeEmojiList();
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e}");
        }
    }
}