using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;

namespace ReAction;

public class ReAction : DalamudPlugin<Configuration>, IDalamudPlugin
{
    public string Name => "ReAction";

    public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action> actionSheet;
    public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action> mountActionsSheet;

    public ReAction(DalamudPluginInterface pluginInterface) : base(pluginInterface) { }

    protected override void Initialize()
    {
        Game.Initialize();
        PronounManager.Initialize();

        actionSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.Where(i => i.ClassJobCategory.Row > 0 && i.ActionCategory.Row <= 4 && i.RowId > 8).ToDictionary(i => i.RowId, i => i);
        mountActionsSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.Where(i => i.ActionCategory.Row == 12).ToDictionary(i => i.RowId, i => i);
        if (actionSheet == null || mountActionsSheet == null)
            throw new ApplicationException("加载失败！");
    }

    protected override void ToggleConfig() => PluginUI.IsVisible ^= true;

    [PluginCommand("/reaction", HelpMessage = "打开 / 关闭设置.")]
    private void ToggleConfig(string command, string argument) => ToggleConfig();

    [PluginCommand("/macroqueue", "/mqueue", HelpMessage = "[on|off] - 切换 (无冲突), 启用或禁用当前宏中的 /ac 排队。")]
    private void OnMacroQueue(string command, string argument)
    {
        if (!Common.IsMacroRunning)
        {
            DalamudApi.PrintError("该命令必须在宏运行中使用。闲鱼小店改json倒卖本插件必死全家，如果你是闲鱼购买，请退款举报");
            return;
        }

        switch (argument)
        {
            case "on":
                Game.queueACCommandPatch.Enable();
                break;
            case "off":
                Game.queueACCommandPatch.Disable();
                break;
            case "":
                if (!Config.EnableMacroQueue) // Bug, users could use two /macroqueue and would expect the second to disable it, but scenario is very unlikely
                    Game.queueACCommandPatch.Toggle();
                break;
            default:
                DalamudApi.PrintError("无效使用.");
                break;
        }
    }

    protected override void Update()
    {
        if (Config.EnableMacroQueue)
        {
            if (!Game.queueACCommandPatch.IsEnabled && !Common.IsMacroRunning)
                Game.queueACCommandPatch.Enable();
        }
        else
        {
            if (Game.queueACCommandPatch.IsEnabled && !Common.IsMacroRunning)
                Game.queueACCommandPatch.Disable();
        }
    }

    protected override void Draw() => PluginUI.Draw();

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        Game.Dispose();
    }
}