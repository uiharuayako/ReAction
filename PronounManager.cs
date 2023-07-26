using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.GeneratedSheets;

namespace ReAction;

public static unsafe class PronounHelpers
{
    public static float GetHPPercent(nint address) => (float)((Character*)address)->Health / ((Character*)address)->MaxHealth;

    public static uint GetHP(nint address) => ((Character*)address)->Health;

    public static GameObject* GetPartyMemberByStatus(uint status, uint sourceID) => (GameObject*)Common.GetPartyMembers().FirstOrDefault(address => ((Character*)address)->GetStatusManager()->HasStatus(status, sourceID));

    public static GameObject* GetPartyMemberByClassJobID(byte classJob) => (GameObject*)Common.GetPartyMembers().Skip(1).FirstOrDefault(address => ((Character*)address)->ClassJob == classJob);

    public static GameObject* GetPartyMemberByRoleID(byte role) => DalamudApi.DataManager.GetExcelSheet<ClassJob>() is { } sheet
        ? (GameObject*)Common.GetPartyMembers().Skip(1).FirstOrDefault(address => sheet.GetRow(((Character*)address)->ClassJob)?.Role == role)
        : null;

    public static GameObject* GetPartyMemberByLimitBreak1(uint actionID) => DalamudApi.DataManager.GetExcelSheet<ClassJob>() is { } sheet
        ? (GameObject*)Common.GetPartyMembers().Skip(1).FirstOrDefault(address => sheet.GetRow(((Character*)address)->ClassJob)?.LimitBreak1.Row == actionID)
        : null;
}

public interface IGamePronoun
{
    public string Name { get; }
    public string Placeholder { get; }
    public uint ID { get; }
    public unsafe GameObject* GetGameObject();
}

public class HardTargetPronoun : IGamePronoun
{
    public string Name => "目标";
    public string Placeholder => "<hard>";
    public uint ID => 10_000;
    public unsafe GameObject* GetGameObject() => (GameObject*)DalamudApi.TargetManager.Target?.Address;
}

public class SoftTargetPronoun : IGamePronoun
{
    public string Name => "软目标";
    public string Placeholder => "<soft>";
    public uint ID => 10_001;
    public unsafe GameObject* GetGameObject() => (GameObject*)DalamudApi.TargetManager.SoftTarget?.Address;
}

public class UITargetPronoun : IGamePronoun
{
    public string Name => "UI目标";
    public string Placeholder => "<ui>";
    public uint ID => 10_002;
    public unsafe GameObject* GetGameObject() => Common.UITarget;
}

public class FieldTargetPronoun : IGamePronoun
{
    public string Name => "地面目标";
    public string Placeholder => "<field>";
    public uint ID => 10_003;
    public unsafe GameObject* GetGameObject() => (GameObject*)DalamudApi.TargetManager.MouseOverTarget?.Address;
}

public class FieldTargetPartyMemberPronoun : IGamePronoun
{
    public string Name => "地面目标队员";
    public string Placeholder => "<fieldp>";
    public uint ID => 10_004;

    private nint prevObject = nint.Zero;
    private readonly Stopwatch prevObjectTimer = new();
    private GameObjectArray partyMemberArray = new();

    public unsafe GameObject* GetGameObject()
    {
        var i = 0;
        foreach (var partyMember in Common.GetPartyMembers().Skip(1))
            partyMemberArray.Objects[i++] = partyMember;
        partyMemberArray.Length = i;

        fixed (GameObjectArray* ptr = &partyMemberArray)
        {
            var ret = Game.GetMouseOverObject(ptr);
            if (ret == null)
            {
                if (prevObjectTimer.ElapsedMilliseconds <= 350)
                {
                    for (int j = 0; j < partyMemberArray.Length; j++)
                    {
                        if (partyMemberArray.Objects[j] == prevObject)
                            return (GameObject*)prevObject;
                    }
                }
                return null;
            }

            prevObject = (nint)ret;
            prevObjectTimer.Restart();
            return ret;
        }
    }
}

public class LowestHPPronoun : IGamePronoun
{
    public string Name => "血量最少队员";
    public string Placeholder => "<lowhp>";
    public uint ID => 10_010;
    public unsafe GameObject* GetGameObject()
    {
        var members = Common.GetPartyMembers().Where(address => PronounHelpers.GetHPPercent(address) is > 0 and < 1);
        return members.Any() ? (GameObject*)members.MinBy(PronounHelpers.GetHP) : null;
    }
}

public class LowestHPPPronoun : IGamePronoun
{
    public string Name => "血百分比最少队员";
    public string Placeholder => "<lowhpp>";
    public uint ID => 10_011;
    public unsafe GameObject* GetGameObject()
    {
        var members = Common.GetPartyMembers().Where(address => PronounHelpers.GetHPPercent(address) is > 0 and < 1);
        return members.Any() ? (GameObject*)members.MinBy(PronounHelpers.GetHPPercent) : null;
    }
}

public class KardionPronoun : IGamePronoun
{
    public string Name => "Kardion Target";
    public string Placeholder => "<kt>";
    public uint ID => 10_100;
    public unsafe GameObject* GetGameObject() => DalamudApi.ClientState.LocalPlayer is { } p ? PronounHelpers.GetPartyMemberByStatus(2605, p.ObjectId) : null;
}

public class TankPronoun : IGamePronoun
{
    public string Name => "T";
    public string Placeholder => "<tank>";
    public uint ID => 10_200;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByRoleID(1);
}

public class HealerPronoun : IGamePronoun
{
    public string Name => "奶";
    public string Placeholder => "<healer>";
    public uint ID => 10_203;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByRoleID(4);
}

/*public class PureHealerPronoun : IGamePronoun
{
    public string Name => "Pure Healer";
    public string Placeholder => "<phealer>";
    public uint ID => 10_204;
    public unsafe GameObject* GetGameObject() => ;
}

public class BarrierHealerPronoun : IGamePronoun
{
    public string Name => "Barrier Healer";
    public string Placeholder => "<bhealer>";
    public uint ID => 10_205;
    public unsafe GameObject* GetGameObject() => ;
}

public class DPSPronoun : IGamePronoun
{
    public string Name => "DPS";
    public string Placeholder => "<dps>";
    public uint ID => 10_206;
    public unsafe GameObject* GetGameObject() => ;
}*/

public class MeleeDPSPronoun : IGamePronoun
{
    public string Name => "近战";
    public string Placeholder => "<melee>";
    public uint ID => 10_207;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByRoleID(2);
}

public class RangedDPSPronoun : IGamePronoun
{
    public string Name => "远程";
    public string Placeholder => "<ranged>";
    public uint ID => 10_208;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByRoleID(3);
}

public class PhysicalRangedDPSPronoun : IGamePronoun
{
    public string Name => "物理远D";
    public string Placeholder => "<pranged>";
    public uint ID => 10_209;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByLimitBreak1(4238);
}

public class MagicalRangedDPSPronoun : IGamePronoun
{
    public string Name => "法师远D";
    public string Placeholder => "<mranged>";
    public uint ID => 10_210;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByLimitBreak1(203);
}

public class PaladinPronoun : IGamePronoun
{
    private const byte ClassJobID = 19;

    public string Name => "骑士";
    public string Placeholder => "<pld>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class WarriorPronoun : IGamePronoun
{
    private const byte ClassJobID = 21;

    public string Name => "战士";
    public string Placeholder => "<war>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class DarkKnightPronoun : IGamePronoun
{
    private const byte ClassJobID = 32;

    public string Name => "DK";
    public string Placeholder => "<drk>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class GunbreakerPronoun : IGamePronoun
{
    private const byte ClassJobID = 37;

    public string Name => "绝枪";
    public string Placeholder => "<gnb>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class WhiteMagePronoun : IGamePronoun
{
    private const byte ClassJobID = 24;

    public string Name => "白魔";
    public string Placeholder => "<whm>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class ScholarPronoun : IGamePronoun
{
    private const byte ClassJobID = 28;

    public string Name => "学者";
    public string Placeholder => "<sch>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class AstrologianPronoun : IGamePronoun
{
    private const byte ClassJobID = 33;

    public string Name => "占星";
    public string Placeholder => "<ast>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class SagePronoun : IGamePronoun
{
    private const byte ClassJobID = 40;

    public string Name => "贤者";
    public string Placeholder => "<sge>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class MonkPronoun : IGamePronoun
{
    private const byte ClassJobID = 20;

    public string Name => "武僧";
    public string Placeholder => "<mnk>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class DragoonPronoun : IGamePronoun
{
    private const byte ClassJobID = 22;

    public string Name => "龙骑";
    public string Placeholder => "<drg>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class NinjaPronoun : IGamePronoun
{
    private const byte ClassJobID = 30;

    public string Name => "忍者";
    public string Placeholder => "<nin>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class SamuraiPronoun : IGamePronoun
{
    private const byte ClassJobID = 34;

    public string Name => "武士";
    public string Placeholder => "<sam>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class ReaperPronoun : IGamePronoun
{
    private const byte ClassJobID = 39;

    public string Name => "镰刀";
    public string Placeholder => "<rpr>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class BardPronoun : IGamePronoun
{
    private const byte ClassJobID = 23;

    public string Name => "诗人";
    public string Placeholder => "<brd>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class MachinistPronoun : IGamePronoun
{
    private const byte ClassJobID = 31;

    public string Name => "机工";
    public string Placeholder => "<mch>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class DancerPronoun : IGamePronoun
{
    private const byte ClassJobID = 38;

    public string Name => "舞者";
    public string Placeholder => "<dnc>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class BlackMagePronoun : IGamePronoun
{
    private const byte ClassJobID = 25;

    public string Name => "黑魔";
    public string Placeholder => "<blm>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class SummonerPronoun : IGamePronoun
{
    private const byte ClassJobID = 27;

    public string Name => "召唤";
    public string Placeholder => "<smn>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class RedMagePronoun : IGamePronoun
{
    private const byte ClassJobID = 35;

    public string Name => "赤魔";
    public string Placeholder => "<rdm>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public class BlueMagePronoun : IGamePronoun
{
    private const byte ClassJobID = 36;

    public string Name => "青魔";
    public string Placeholder => "<blu>";
    public uint ID => 10_220 + ClassJobID;
    public unsafe GameObject* GetGameObject() => PronounHelpers.GetPartyMemberByClassJobID(ClassJobID);
}

public static class PronounManager
{
    public const int MinimumCustomPronounID = 10_000;

    public static Dictionary<uint, IGamePronoun> CustomPronouns { get; set; } = new();
    public static Dictionary<string, IGamePronoun> CustomPlaceholders { get; set; } = new();
    public static List<uint> OrderedIDs { get; set; } = new()
    {
        10_000, // Target
        10_001, // SoftTarget
        (uint)PronounID.FocusTarget,
        10_002, // UITarget
        10_003, // FieldTarget
        10_004,
        (uint)PronounID.TargetsTarget,
        (uint)PronounID.LastTarget,
        (uint)PronounID.LastEnemy,
        (uint)PronounID.LastAttacker,
        (uint)PronounID.Me,
        (uint)PronounID.P2,
        (uint)PronounID.P3,
        (uint)PronounID.P4,
        (uint)PronounID.P5,
        (uint)PronounID.P6,
        (uint)PronounID.P7,
        (uint)PronounID.P8,
        (uint)PronounID.Companion,
        (uint)PronounID.Pet
    };

    private static readonly Dictionary<PronounID, string> formalPronounIDName = new()
    {
        [PronounID.FocusTarget] = "焦点目标",
        [PronounID.TargetsTarget] = "目标的目标",
        [PronounID.LastTarget] = "最后目标",
        [PronounID.LastEnemy] = "最后敌人",
        [PronounID.LastAttacker] = "最后攻击者",
        [PronounID.Me] = "自己"
    };

    public static void Initialize()
    {
        foreach (var t in Util.Assembly.GetTypes<IGamePronoun>())
        {
            var pronoun = (IGamePronoun)Activator.CreateInstance(t);
            if (pronoun == null) continue;

            if (pronoun.ID < MinimumCustomPronounID)
                throw new ApplicationException("自定义代词 ID 必须大于 10000");

            CustomPronouns.Add(pronoun.ID, pronoun);
            CustomPlaceholders.Add(pronoun.Placeholder, pronoun);
            if (!OrderedIDs.Contains(pronoun.ID))
                OrderedIDs.Add(pronoun.ID);
        }
    }

    public static string GetPronounName(uint id) => id >= MinimumCustomPronounID && CustomPronouns.TryGetValue(id, out var pronoun)
        ? pronoun.Name
        : formalPronounIDName.TryGetValue((PronounID)id, out var name) ? name : ((PronounID)id).ToString();

    public static unsafe GameObject* GetGameObjectFromID(uint id) => PluginModuleManager.GetModule<Modules.ActionStacks>().IsValid ?
            id >= MinimumCustomPronounID && CustomPronouns.TryGetValue(id, out var pronoun)
                ? pronoun.GetGameObject()
                : Common.GetGameObjectFromPronounID((PronounID)id)
            : null;
}