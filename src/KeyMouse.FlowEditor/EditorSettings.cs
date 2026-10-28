using System.Text.Json;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 编辑器自己的小设置：最近打开过哪些流程。
///
/// 空状态如果只说"还没有步骤"，对第一次打开的人是没用的。工具的空状态应该回答"从哪儿开始"，
/// 而"上次在改哪个文件"通常就是答案。存在 %LOCALAPPDATA%\KeyMouse\editor.json，跟流程文件无关，
/// 删掉它只会丢这份列表。
/// </summary>
internal sealed class EditorSettings
{
    private const int MaxRecent = 5;   // 起始页一屏能看全：再多就会把标题挤出可视区

    private static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMouse", "editor.json");

    public List<string> Recent { get; set; } = [];

    internal static EditorSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                EditorSettings? loaded = JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(Path));
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception)
        {
            // 设置读坏了不该拦住编辑器：当作没有设置。
        }
        return new EditorSettings();
    }

    internal void Remember(string path)
    {
        string full = System.IO.Path.GetFullPath(path);
        Recent.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, full);
        if (Recent.Count > MaxRecent) Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
        Save();
    }

    private void Save()
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(Path);
            if (directory is { Length: > 0 }) Directory.CreateDirectory(directory);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // 记不住最近文件是小事，不该弹错误。
        }
    }
}
