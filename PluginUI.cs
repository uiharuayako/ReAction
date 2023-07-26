using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Hypostasis.Game.Structures;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace ReAction;

public static class PluginUI
{
    private static bool isVisible = false;
    private static int selectedStack = -1;
    private static int hotbar = 0;
    private static int hotbarSlot = 0;
    private static int commandType = 1;
    private static uint commandID = 0;

    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    private static Configuration.ActionStack CurrentStack => 0 <= selectedStack && selectedStack < ReAction.Config.ActionStacks.Count ? ReAction.Config.ActionStacks[selectedStack] : null;

    public static void Draw()
    {
        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 600) * ImGuiHelpers.GlobalScale, new Vector2(9999));
        ImGui.Begin("ReAction设置", ref isVisible);
        ImGuiEx.AddDonationHeader();

        if (ImGui.BeginTabBar("ReAction选项卡"))
        {
            if (ImGui.BeginTabItem("预设"))
            {
                DrawStackList();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("其他设置"))
            {
                ImGui.BeginChild("其他设置");
                DrawOtherSettings();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("自定义命令"))
            {
                DrawCustomPlaceholders();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("帮助"))
            {
                DrawStackHelp();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawStackList()
    {
        var currentStack = CurrentStack;
        var hasSelectedStack = currentStack != null;

        ImGui.PushFont(UiBuilder.IconFont);

        var buttonSize = ImGui.CalcTextSize(FontAwesomeIcon.SignOutAlt.ToIconString()) + ImGui.GetStyle().FramePadding * 2;

        if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), buttonSize))
        {
            ReAction.Config.ActionStacks.Add(new() { Name = "新预设" });
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignOutAlt.ToIconString(), buttonSize) && hasSelectedStack)
            ImGui.SetClipboardText(Configuration.ExportActionStack(CurrentStack));
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("导出预设至剪切板");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignInAlt.ToIconString(), buttonSize))
        {
            try
            {
                var stack = Configuration.ImportActionStack(ImGui.GetClipboardText());
                ReAction.Config.ActionStacks.Add(stack);
                ReAction.Config.Save();
            }
            catch (Exception e)
            {
                DalamudApi.PrintError($"导入失败\n{e.Message}");
            }
        }
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("导入预设从剪切板");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowUp.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Max(selectedStack - 1, 0);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowDown.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Min(selectedStack + 1, ReAction.Config.ActionStacks.Count);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.PopFont();

        ImGui.SameLine();

        if (ImGuiEx.DeleteConfirmationButton(buttonSize) && hasSelectedStack)
        {
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);
            selectedStack = Math.Min(selectedStack, ReAction.Config.ActionStacks.Count - 1);
            currentStack = CurrentStack;
            hasSelectedStack = currentStack != null;
            ReAction.Config.Save();
        }

        var firstColumnWidth = 250 * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
        ImGui.BeginChild("ReAction预设列表", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y / 2), true);
        ImGui.PopStyleColor();

        for (int i = 0; i < ReAction.Config.ActionStacks.Count; i++)
        {
            ImGui.PushID(i);

            var preset = ReAction.Config.ActionStacks[i];

            if (ImGui.Selectable(preset.Name, selectedStack == i))
                selectedStack = i;

            ImGui.PopID();
        }

        ImGui.EndChild();

        if (!hasSelectedStack) return;

        var lastCursorPos = ImGui.GetCursorPos();
        ImGui.SameLine();
        var nextLineCursorPos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(lastCursorPos);

        ImGui.BeginChild("ReAction预设编辑选项卡", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y), true);
        DrawStackEditorMain(currentStack);
        ImGui.EndChild();

        ImGui.SetCursorPos(nextLineCursorPos);
        ImGui.BeginChild("ReAction预设编辑列表", ImGui.GetContentRegionAvail(), false);
        DrawStackEditorLists(currentStack);
        ImGui.EndChild();
    }

    private static void DrawStackEditorMain(Configuration.ActionStack stack)
    {
        var save = false;

        save |= ImGui.InputText("名称", ref stack.Name, 64);
        save |= ImGui.CheckboxFlags("##Shift", ref stack.ModifierKeys, 1);
        ImGuiEx.SetItemTooltip("Shift");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Ctrl", ref stack.ModifierKeys, 2);
        ImGuiEx.SetItemTooltip("Control");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Alt", ref stack.ModifierKeys, 4);
        ImGuiEx.SetItemTooltip("Alt");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Exact", ref stack.ModifierKeys, 8);
        ImGuiEx.SetItemTooltip("精准匹配这些快捷键. 比如勾选Shift + Control将匹配按住Shift + Control,而不会匹配Shift + Control + Alt");
        ImGui.SameLine();
        ImGui.TextUnformatted("快捷键");
        save |= ImGui.Checkbox("预设操作失败时阻止原始操作", ref stack.BlockOriginal);
        save |= ImGui.Checkbox("超出范围则失败", ref stack.CheckRange);
        save |= ImGui.Checkbox("如果处于冷却状态则失败", ref stack.CheckCooldown);
        ImGuiEx.SetItemTooltip("如果操作因冷却而无法插入技能序列则失败。" +
            "\n> 冷却时间还剩 0.5 秒，或自上次使用后 < 0.5 秒（费用/GCD）.");

        if (save)
            ReAction.Config.Save();
    }

    private static void DrawStackEditorLists(Configuration.ActionStack stack)
    {
        DrawActionEditor(stack);
        DrawItemEditor(stack);
    }

    private static string FormatActionRow(Action a) => a.RowId switch
    {
        0 => "所有技能",
        1 => "所有伤害技能",
        2 => "所有治疗技能",
        _ => $"[#{a.RowId} {a.ClassJob.Value?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Action> actionComboOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = DalamudApi.DataManager.GetExcelSheet<Action>()?.Take(3).Concat(ReAction.actionSheet.Select(kv => kv.Value))
    };

    private static readonly ImGuiEx.ExcelSheetPopupOptions<Action> actionPopupOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = actionComboOptions.FilteredSheet
    };

    private static void DrawActionEditor(Configuration.ActionStack stack)
    {
        var contentRegion = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("ReAction技能编辑", contentRegion with { Y = contentRegion.Y / 2 }, true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        var buttonIndent = 0f;
        for (int i = 0; i < stack.Actions.Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var action = stack.Actions[i];

            ImGui.Button("≡");
            if (ImGuiEx.IsItemDraggedDelta(action, ImGuiMouseButton.Left, ImGui.GetFrameHeightWithSpacing(), false, out var dt) && dt.Y != 0)
                stack.Actions.Shift(i, dt.Y);

            if (i == 0)
                buttonIndent = ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X;

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##技能", ref action.ID, actionComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

            if (ImGui.Checkbox("调整ID", ref action.UseAdjustedID))
                ReAction.Config.Save();
            var detectedAdjustment = false;
            unsafe
            {
                if (!action.UseAdjustedID && (detectedAdjustment = Common.ActionManager->CS.GetAdjustedActionId(action.ID) != action.ID))
                    ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0x2000FF30, ImGui.GetStyle().FrameRounding);
            }
            ImGuiEx.SetItemTooltip("允许该技能与它转换成的任何其他技能相匹配." +
                "\n例如疾风将匹配天辉，出卡将匹配抽卡，注药将匹配均衡注药，等等。" +
                "\n启用此功能可转换技能，禁用此选项可与某些 XIVCombos 兼容，避免冲突。" +
                (detectedAdjustment ? "\n\n目前，此操作已根据特征、组合或插件进行了调整。推荐开启此选项." : string.Empty));

            ImGui.SameLine();

            if (!ImGuiEx.DeleteConfirmationButton()) continue;
            stack.Actions.RemoveAt(i);
            ReAction.Config.Save();
        }

        using (ImGuiEx.IndentBlock.Begin(buttonIndent))
        {
            ImGuiEx.FontButton(FontAwesomeIcon.Plus.ToIconString(), UiBuilder.IconFont, new Vector2(buttonWidth, 0));
            if (ImGuiEx.ExcelSheetPopup("ReAction添加技能弹出窗口", out var row, actionPopupOptions))
            {
                stack.Actions.Add(new() { ID = row });
                ReAction.Config.Save();
            }
        }

        ImGui.EndChild();
    }

    private static string FormatOverrideActionRow(Action a) => a.RowId switch
    {
        0 => "相同技能",
        _ => $"[#{a.RowId} {a.ClassJob.Value?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Action> actionOverrideComboOptions = new()
    {
        FormatRow = FormatOverrideActionRow,
        FilteredSheet = DalamudApi.DataManager.GetExcelSheet<Action>()?.Take(1).Concat(ReAction.actionSheet.Select(kv => kv.Value))
    };

    private static void DrawItemEditor(Configuration.ActionStack stack)
    {
        ImGui.BeginChild("ReAction对象编辑", ImGui.GetContentRegionAvail(), true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 3;
        var buttonIndent = 0f;
        for (int i = 0; i < stack.Items.Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var item = stack.Items[i];

            ImGui.Button("≡");
            if (ImGuiEx.IsItemDraggedDelta(item, ImGuiMouseButton.Left, ImGui.GetFrameHeightWithSpacing(), false, out var dt) && dt.Y != 0)
                stack.Items.Shift(i, dt.Y);

            if (i == 0)
                buttonIndent = ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X;

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (DrawTargetTypeCombo("##目标类型", ref item.TargetID))
                ReAction.Config.Save();

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##技能覆盖", ref item.ID, actionOverrideComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

            if (!ImGuiEx.DeleteConfirmationButton()) continue;
            stack.Items.RemoveAt(i);
            ReAction.Config.Save();
        }

        using (ImGuiEx.IndentBlock.Begin(buttonIndent))
        {
            if (ImGuiEx.FontButton(FontAwesomeIcon.Plus.ToIconString(), UiBuilder.IconFont, new Vector2(buttonWidth, 0)))
            {
                stack.Items.Add(new());
                ReAction.Config.Save();
            }
        }

        ImGui.EndChild();
    }

    private static bool DrawTargetTypeCombo(string label, ref uint currentSelection)
    {
        if (!ImGui.BeginCombo(label, PronounManager.GetPronounName(currentSelection))) return false;

        var ret = false;
        foreach (var id in PronounManager.OrderedIDs)
        {
            if (!ImGui.Selectable(PronounManager.GetPronounName(id), id == currentSelection)) continue;
            currentSelection = id;
            ret = true;
            break;
        }

        ImGui.EndCombo();
        return ret;
    }

    private static void DrawOtherSettings()
    {
        var save = false;

        if (ImGuiEx.BeginGroupBox("技能", 0.5f))
        {
            save |= ImGui.Checkbox("按住按键连发", ref ReAction.Config.EnableTurboHotbars);
            ImGuiEx.SetItemTooltip("字面意思，按住按键自动连打.\n注意！输出文本宏同样起作用。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableTurboHotbars))
            {
                ImGuiEx.Prefix(false);
                save |= ImGui.DragInt("间隔", ref ReAction.Config.TurboHotbarInterval, 0.5f, 0, 1000, "%d ms");

                ImGuiEx.Prefix(false);
                save |= ImGui.DragInt("启动延迟", ref ReAction.Config.InitialTurboHotbarInterval, 0.5f, 0, 1000, "%d ms");

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("副本外也生效", ref ReAction.Config.EnableTurboHotbarsOutOfCombat);
            }

            save |= ImGui.Checkbox("地面目标智能施法", ref ReAction.Config.EnableInstantGroundTarget);
            ImGuiEx.SetItemTooltip("一键缩地（让地面目标以鼠标悬浮处的单位或者地面为中心施放）！.");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableInstantGroundTarget))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("阻止其他地面目标", ref ReAction.Config.EnableBlockMiscInstantGroundTargets);
                ImGuiEx.SetItemTooltip("放置宠物等操作时禁用地面目标智能施法。");
            }

            save |= ImGui.Checkbox("更加智能的施法技能面向", ref ReAction.Config.EnableEnhancedAutoFaceTarget);
            ImGuiEx.SetItemTooltip("不需要面对目标的动作将不再自动面对目标，例如治疗.");

            save |= ImGui.Checkbox("更加智能的范围技能方向", ref ReAction.Config.EnableCameraRelativeDirectionals);
            ImGuiEx.SetItemTooltip("更改引导和定向动作，大翅膀（翅膀开在镜头后）和穿甲散弹，\n以相对于你的镜头所面对的方向，而不是你的角色。位移技能在下面开！！");

            save |= ImGui.Checkbox("更加智能的位移技能方向", ref ReAction.Config.EnableCameraRelativeDashes);
            ImGuiEx.SetItemTooltip("更改位移技能，例如前冲步和后跳，\n以相对于你的镜头所面对的方向，而不是你的角色。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableCameraRelativeDashes))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("锁定后跳", ref ReAction.Config.EnableNormalBackwardDashes);
                ImGuiEx.SetItemTooltip("向后的位移将不会起作用，想要帅气后跳建议别开这个");
            }

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("自动", 0.5f))
        {
            save |= ImGui.Checkbox("自动下车", ref ReAction.Config.EnableAutoDismount);
            ImGuiEx.SetItemTooltip("放技能自动下车");

            save |= ImGui.Checkbox("自动断读条", ref ReAction.Config.EnableAutoCastCancel);
            ImGuiEx.SetItemTooltip("目标死了自动中断读条");

            save |= ImGui.Checkbox("自动选敌", ref ReAction.Config.EnableAutoTarget);
            ImGuiEx.SetItemTooltip("当没有指定目标进行定向攻击时，自动瞄准最近的敌人。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableAutoTarget))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("启用自动更改目标", ref ReAction.Config.EnableAutoChangeTarget);
                ImGuiEx.SetItemTooltip("当您的主要目标不适合有针对性的攻击时，还会瞄准最近的敌人。");
            }

            var _ = ReAction.Config.AutoFocusTargetID != 0;
            if (ImGui.Checkbox("自动焦点", ref _))
            {
                ReAction.Config.AutoFocusTargetID = _ ? PronounManager.OrderedIDs.First() : 0;
                save = true;
            }
            ImGuiEx.SetItemTooltip("自动焦点你设定的对象");

            using (ImGuiEx.DisabledBlock.Begin(!_))
            {
                ImGuiEx.Prefix(false);
                save |= DrawTargetTypeCombo("##自动焦点ID", ref ReAction.Config.AutoFocusTargetID);

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("副本外也使用自动焦点", ref ReAction.Config.EnableAutoFocusTargetOutOfCombat);
            }

            save |= ImGui.Checkbox("自动重新焦点", ref ReAction.Config.EnableAutoRefocusTarget);
            ImGuiEx.SetItemTooltip("在副本中，如果焦点目标丢失，则尝试焦点之前焦点的目标。比如托尔丹。");

            save |= ImGui.Checkbox("启用法师自动攻击", ref ReAction.Config.EnableSpellAutoAttacks);
            ImGuiEx.SetItemTooltip("各种法师的读条时的平A，请在有物理反弹场景禁用eg假面狂欢");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableSpellAutoAttacks))
            {
                ImGuiEx.Prefix(true);
                if (ImGui.Checkbox("副本外也启用##法师平A", ref ReAction.Config.EnableSpellAutoAttacksOutOfCombat))
                {
                    if (ReAction.Config.EnableSpellAutoAttacksOutOfCombat)
                        Game.spellAutoAttackPatch.Enable();
                    else
                        Game.spellAutoAttackPatch.Disable();
                    save = true;
                }
                ImGuiEx.SetItemTooltip("物理反弹场景请禁用");
            }

            ImGuiEx.EndGroupBox();
        }

        if (ImGuiEx.BeginGroupBox("技能队列", 0.5f))
        {
            if (ImGui.Checkbox("地面目标技能进入队列", ref ReAction.Config.EnableGroundTargetQueuing))
            {
                Game.queueGroundTargetsPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("将把地面目标技能插入到技能队列中，\n使它们能够像其他能力技一样尽快被使用。");

            save |= ImGui.Checkbox("允许某些技能的插入", ref ReAction.Config.EnableQueuingMore);
            ImGuiEx.SetItemTooltip("疾跑，药品，LB也能插！");

            save |= ImGui.Checkbox("宏插入技能队列", ref ReAction.Config.EnableMacroQueue);
            ImGuiEx.SetItemTooltip("所有宏的行为就像使用了/macroqueue一样。");

            save |= ImGui.Checkbox("开挂(BETA)", ref ReAction.Config.EnableQueueAdjustments);
            ImGuiEx.SetItemTooltip("改GCD时间和动画锁。\n这是BETA功能，如果有任何问题无法按预期运行，就关掉。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableQueueAdjustments))
            using (ImGuiEx.ItemWidthBlock.Begin(ImGui.CalcItemWidth() / 2))
            {
                ImGuiEx.Prefix(false);
                save |= ImGui.Checkbox("##启用 GCD 调整阈值", ref ReAction.Config.EnableGCDAdjustedQueueThreshold);
                ImGuiEx.SetItemTooltip("根据当前 GCD 修改阈值。");

                ImGui.SameLine();
                save |= ImGui.SliderFloat("技能队列阈值", ref ReAction.Config.QueueThreshold, 0.1f, 2.5f, "%.1f");
                ImGuiEx.SetItemTooltip("动作冷却剩余时间，\n以便游戏在提前按下时可以插入下一个动作。默认值：0.5。" +
                    (ReAction.Config.EnableGCDAdjustedQueueThreshold ? $"\nGCD Adjusted Threshold: {ReAction.Config.QueueThreshold * ActionManager.GCDRecast / 2500f}" : string.Empty));

                ImGui.BeginGroup();
                ImGuiEx.Prefix(false);
                save |= ImGui.Checkbox("##启用重新插入", ref ReAction.Config.EnableRequeuing);
                using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableRequeuing))
                {
                    ImGui.SameLine();
                    save |= ImGui.SliderFloat("队列锁定阈值", ref ReAction.Config.QueueLockThreshold, 0.1f, 2.5f, "%.1f");
                }
                ImGui.EndGroup();
                ImGuiEx.SetItemTooltip("启用后，允许重新插，直到队列技能的冷却时间低于此值。");

                ImGuiEx.Prefix(false);
                save |= ImGui.SliderFloat("技能锁定", ref ReAction.Config.QueueActionLockout, 0, 2.5f, "%.1f");
                ImGuiEx.SetItemTooltip("如果同一技能的冷却时间小于此值，则阻止该操作再次插入。");

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("启用GCD滑步插入", ref ReAction.Config.EnableSlidecastQueuing);
                ImGuiEx.SetItemTooltip("允许下一个GCD技能在GCD施法的最后0.5秒内排队。");
            }

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("拆分", 0.5f))
        {
            save |= ImGui.Checkbox("拆分斗气", ref ReAction.Config.EnableDecomboMeditation);
            ImGuiEx.SetItemTooltip("移除斗气 <-> 铁山靠 / 阴阳斗气斩连击. 你需要使用\n下面的热键栏功能可将其中之一放在您的热键栏上，以便再次使用它们。\n铁山靠 ID: 25761\n阴阳斗气斩 ID: 3547");

            save |= ImGui.Checkbox("拆分分身之术", ref ReAction.Config.EnableDecomboBunshin);
            ImGuiEx.SetItemTooltip("移除分身之术 <-> 残影镰鼬连击.你需要使用\n下面的热键栏功能可将其中之一放在您的热键栏上，以便再次使用它们。\n残影镰鼬 ID: 25774");

            save |= ImGui.Checkbox("拆分放浪神的小步舞曲", ref ReAction.Config.EnableDecomboWanderersMinuet);
            ImGuiEx.SetItemTooltip("移除放浪神的小步舞曲 -> 完美音调连击. 你需要使用\n下面的热键栏功能可将其中之一放在您的热键栏上，以便再次使用它们。\n完美音调 ID: 7404");

            save |= ImGui.Checkbox("拆分礼仪之铃", ref ReAction.Config.EnableDecomboLiturgy);
            ImGuiEx.SetItemTooltip("移除礼仪之铃连击. 你需要使用\n下面的热键栏功能可将其中之一放在您的热键栏上，以便再次使用它们。\n铃铛 (Detonate) ID: 28509");

            save |= ImGui.Checkbox("拆分地星", ref ReAction.Config.EnableDecomboEarthlyStar);
            ImGuiEx.SetItemTooltip("移除地星连击. 您将需要使用\n下面的热键栏功能可将其中之一放在您的热键栏上，以便再次使用它们。\n星体爆轰 ID: 8324");

            save |= ImGui.Checkbox("拆分小奥秘卡", ref ReAction.Config.EnableDecomboMinorArcana);
            ImGuiEx.SetItemTooltip("移除小奥秘卡 -> 王冠之领主/贵妇连击. 你需要使用\n下面的热键栏功能可将其中之一放在您的热键栏上，以便再次使用它们。\n王冠之领主 ID: 7444\n王冠之贵妇 ID: 7445");

            save |= ImGui.Checkbox("拆分武神枪", ref ReAction.Config.EnableDecomboGeirskogul);
            ImGuiEx.SetItemTooltip("移除武神枪 -> 死者之岸连击. 你需要使用\n下面的热键栏功能可将其中之一放在您的热键栏上，以便再次使用它们。\n死者之岸 ID: 7400");

            ImGuiEx.EndGroupBox();
        }

        if (ImGuiEx.BeginGroupBox("杂项", 0.5f))
        {
            save |= ImGui.Checkbox("帧率对齐", ref ReAction.Config.EnableFrameAlignment);
            ImGuiEx.SetItemTooltip("将游戏的帧率与 GCD 和动画锁定对齐。\n 注意：当这些计时器中的任何一个结束时，此选项将导致几乎不明显的卡顿。");

            if (ImGui.Checkbox("宏小数等待（分数）", ref ReAction.Config.EnableFractionality))
            {
                if (!DalamudApi.PluginInterface.PluginNames.Contains("分数") || !ReAction.Config.EnableFractionality)
                {
                    Game.waitSyntaxDecimalPatch.Toggle();
                    Game.waitCommandDecimalPatch.Toggle();
                    save = true;
                }
                else
                {
                    ReAction.Config.EnableFractionality = false;
                    DalamudApi.PrintError("在启用此功能之前，请使用插件安装程序上的垃圾桶图标禁用并删除 Fractionality插件!");
                }
            }
            ImGuiEx.SetItemTooltip("允许等待宏中使用小数并取消 60 秒的上限（例如 <wait.0.5> 或 /wait 0.5）。");

            if (ImGui.Checkbox("在宏中启用原本不能用的技能", ref ReAction.Config.EnableUnassignableActions))
            {
                Game.allowUnassignableActionsPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("允许使用“/ac”中通常不可用的动作，例如阴阳斗气斩或星体爆轰。");

            save |= ImGui.Checkbox("在宏中启用玩家名称", ref ReAction.Config.EnablePlayerNamesInCommands);
            ImGuiEx.SetItemTooltip("允许对任何需要目标的命令使用“First Last@World”语法。");

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("热键栏功能（将鼠标悬停在我的上方以获取信息）", 0.5f, new ImGuiEx.GroupBoxOptions
        {
            HeaderTextAction = () => ImGuiEx.SetItemTooltip(
                "这将允许您在快捷栏上放置通常无法放置的各种内容。" +
                "\n如果您不知道它有什么用，就别打开。无论您在上面放置什么，都必须移动（挪一个位置放回来），否则将无法保存。" +
                "\n您可以执行的操作的一些示例：" +
                "\n\t在热键栏上放置特定操作，以与“拆分”功能配合使用。 ID 位于每个设置的工具提示中。" +
                "\n\t打瞌睡并在热键栏上做出表情（表情、88 和 95）。" +
                "\n\t将货币（诗学之类的，1-99）放在快捷栏上即可查看您有多少，而无需打开货币菜单。" +
                "\n\t还可以放随机飞行坐骑 (共通技能, 24).")
        }))
        {
            ImGui.Combo("栏", ref hotbar, "1\02\03\04\05\06\07\08\09\010\0XHB 1\0XHB 2\0XHB 3\0XHB 4\0XHB 5\0XHB 6\0XHB 7\0XHB 8");
            ImGui.Combo("格", ref hotbarSlot, "1\02\03\04\05\06\07\08\09\010\011\012\013\014\015\016");
            var hotbarSlotType = Enum.GetName(typeof(HotbarSlotType), commandType) ?? commandType.ToString();
            if (ImGui.BeginCombo("类型", hotbarSlotType))
            {
                for (int i = 1; i <= 32; i++)
                {
                    if (!ImGui.Selectable($"{Enum.GetName(typeof(HotbarSlotType), i) ?? i.ToString()}##{i}", commandType == i)) continue;
                    commandType = i;
                }
                ImGui.EndCombo();
            }

            DrawHotbarIDInput((HotbarSlotType)commandType);

            if (ImGui.Button("执行"))
            {
                Game.SetHotbarSlot(hotbar, hotbarSlot, (byte)commandType, commandID);
                DalamudApi.PrintEcho("请务必移动您刚刚放置在热键栏上的任何内容，否则它将不会保存。将其移至另一个格子然后再移回即可。");
            }
            ImGuiEx.SetItemTooltip("您需要移动放置在快捷栏上的任何内容才能保存它。");
            ImGuiEx.EndGroupBox();
        }

        if (save)
            ReAction.Config.Save();
    }

    public static void DrawHotbarIDInput(HotbarSlotType slotType)
    {
        switch ((HotbarSlotType)commandType)
        {
            case HotbarSlotType.Action:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Action> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.Item:
                const int hqID = 1_000_000;
                var _ = commandID >= hqID ? commandID - hqID : commandID;
                if (ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref _, new ImGuiEx.ExcelSheetComboOptions<Item> { FormatRow = r => $"[#{r.RowId}] {r.Name}" }))
                    commandID = commandID >= hqID ? _ + hqID : _;
                var hq = commandID >= hqID;
                if (ImGui.Checkbox("HQ", ref hq))
                    commandID = hq ? commandID + hqID : commandID - hqID;
                break;
            case HotbarSlotType.EventItem:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<EventItem> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.Emote:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Emote> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.Marker:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Marker> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.CraftAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<CraftAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.GeneralAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<GeneralAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.CompanionOrder:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<BuddyAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.MainCommand:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<MainCommand> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.Minion:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Companion> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            case HotbarSlotType.PetOrder:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<PetAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.Mount:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Mount> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            case HotbarSlotType.FieldMarker:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<FieldMarker> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.Recipe:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Recipe> { FormatRow = r => $"[#{r.RowId}] {r.ItemResult.Value?.Name}" });
                break;
            case HotbarSlotType.ChocoboRaceAbility:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ChocoboRaceAbility> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.ChocoboRaceItem:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ChocoboRaceItem> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.ExtraCommand:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ExtraCommand> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.PvPQuickChat:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<QuickChat> { FormatRow = r => $"[#{r.RowId}] {r.NameAction}" });
                break;
            case HotbarSlotType.PvPCombo:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ActionComboRoute> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case HotbarSlotType.SquadronOrder:
                // Sheet is BgcArmyAction, but it doesn't appear to be in Lumina
                var __ = (int)commandID;
                if (ImGui.Combo("ID", ref __, "[#0]\0[#1] 启用\0[#2] 禁用\0[#3] 再启用\0[#4] 启用LB\0[#5] 启用顺序热键栏"))
                    commandID = (uint)__;
                break;
            case HotbarSlotType.PerformanceInstrument:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Perform> { FormatRow = r => $"[#{r.RowId}] {r.Instrument}" });
                break;
            case HotbarSlotType.Collection:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<McGuffin> { FormatRow = r => $"[#{r.RowId}] {r.UIData.Value?.Name}" });
                break;
            case HotbarSlotType.FashionAccessory:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Ornament> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            // Doesn't appear to have a sheet
            //case HotbarSlotType.LostFindsItem:
            //    ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
            //    break;
            default:
                var ___ = (int)commandID;
                if (ImGui.InputInt("ID", ref ___))
                    commandID = (uint)___;
                break;
        }
    }

    private static unsafe void DrawCustomPlaceholders()
    {
        if (!ImGui.BeginTable("自定义代词信息表", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("姓名");
        ImGui.TableSetupColumn("占位符");
        ImGui.TableSetupColumn("当前目标");
        ImGui.TableHeadersRow();

        foreach (var (placeholder, pronoun) in PronounManager.CustomPlaceholders)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TextUnformatted(pronoun.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(placeholder);

            var p = pronoun.GetGameObject();
            if (p == null) continue;
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(((nint)p->Name).ReadCString());
        }

        ImGui.EndTable();
    }

    private static void DrawStackHelp()
    {
        ImGui.Text("创建预设");
        ImGui.Indent();
        ImGui.TextWrapped("首先，单击左上角的 + 按钮，这将创建一个新预设，您可以开始向其中添加操作和功能。" +
            "闲鱼小店改json倒卖本插件必死全家，如果你是闲鱼购买，请退款举报");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("编辑预设");
        ImGui.Indent();
        ImGui.TextWrapped("单击左上方列表中的预设显示该预设的编辑窗口。 左下窗格是 " +
            "主要设置内容, 这些将改变预设本身的基本功能.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("编辑预设的操作");
        ImGui.Indent();
        ImGui.TextWrapped("右上方的窗格是您可以添加操作的位置，单击 + 会弹出一个框，您可以通过该框搜索它们。 " +
            "添加您想要更改其功能的每个操作后，您还可以选择您想要更改的操作" +
            "\"调整\". 这意味着所选操作将与热键栏上替换它的任何其他操作相匹配。这可能是由于一个特质" +
            "(神圣 <-> 神圣 III)，buff (出卡 -> 太阳神) 或其他插件 (XIVCombo)。您可能希望将其关闭的一个示例情况是 " +
            "调整后的动作有一个单独的用例，例如 XIVCombo 将 出卡 变成 抽卡。您可以更改个人的功能 " +
            "通过将每张卡添加到列表中，同时不影响抽卡。此外，如果游戏当前调整了动作，" +
            "选项将以绿色突出显示作为指示符。");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("编辑预设的功能");
        ImGui.Indent();
        ImGui.TextWrapped("在右下窗格中，您可以通过将目标列表设置为来更改所选操作的功能" +
            "扩展或替换游戏。使用该操作时，插件将尝试从上到下确定哪个目标是有效选择。" +
            "这将在游戏自己的目标优先级系统之前执行，并且仅在未被预设阻止的情况下才允许其继续。如果有任何一个目标" +
            "是有效的选择，插件会将操作的目标更改为新目标，此外，如果设置了覆盖，则将操作替换为覆盖。");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("预设优先级");
        ImGui.Indent();
        ImGui.TextWrapped("执行的预设将取决于从上到下哪个预设首先包含正在使用的节能并考虑其快捷键" +
            "如果您想在预设中使用“所有技能”的同时还用单独技能，则在单独技能上使用覆盖 ");
        ImGui.Unindent();
    }
}