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
            Assets.InitializeEmojiList();

            harmony = new Harmony("com.github.xiaojinna.Emoji");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"{e}");
        }
    }
}