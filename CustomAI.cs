using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Runtime;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using YKF;
using static UICurrency;
using static UnityEngine.EventSystems.EventTrigger;
using System.Net.NetworkInformation;

namespace CustomAI {
    internal static class ModInfo {
        internal const string Guid = "air1068.elin.customai";
        internal const string Name = "CustomAI";
        internal const string Version = "0.1.7";
        internal const int MagicNumber = (int)(3416877565 % int.MaxValue);
        //using my last mod's Steam Workshop file ID for a reasonably unique value
        //(mods don't get a file ID until they're published, so I'm using the previous one instead)
    }

    internal static class AI {
        //A value of 0 is saved in RAM but not written to disk, so these have to start at 1.
        internal const int TARGET = 1;
        internal const int PLAYER = 2;
        internal const int SELF = 3;
        internal const int ALLY = 4;

        internal const int HP = 1;
        internal const int MP = 2;
        internal const int SP = 3;
        internal const int DISTANCE = 4;
        internal const int STATUS = 5;
        internal const int SUMMONS = 6;

        internal const int EQUALS = 1;
        internal const int DOES_NOT_EQUAL = 2;
        internal const int GREATER_THAN = 3;
        internal const int LESS_THAN = 4;
        internal const int GREATER_THAN_OR_EQUALS = 5;
        internal const int LESS_THAN_OR_EQUALS = 6;
    }

    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    public class CustomAI : BaseUnityPlugin {
        private void Awake() {
            var harmony = new Harmony(ModInfo.Guid);
            harmony.PatchAll();
        }

        public void OnStartCore() {
            var dir = Path.GetDirectoryName(Info.Location);
            var excel = dir + "/AI_Lang.xlsx";
            ModUtil.ImportExcel(excel, "General", Core.Instance.sources.langGeneral);
        }
    }

    [HarmonyPatch(typeof(Chara), nameof(Chara.SetAIAggro))]
    class Chara_SetAIAggro_Patch {
        static bool Prefix(Chara __instance) {
            if (__instance.GetObj<List<Instruction>>(ModInfo.MagicNumber) != null) {
                __instance.SetAI(new GoalCustomAICombat());
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ActPlan), nameof(ActPlan._Update))]
    class ActPlan__Update_Patch {
        static void Postfix(PointTarget target, ActPlan __instance) {
            if (__instance.altAction) {
                Chara targetNPC = target.pos.FirstVisibleChara();
                if (targetNPC != null && targetNPC != EClass.pc && EClass.pc.party.members.Contains(targetNPC)) {
                    __instance.TrySetAct("ActEditCustomAI", delegate {
                        YK.CreateLayer<CustomUILayer, Chara>(targetNPC);
                        return true;
                    }, targetNPC, null, 99);
                }
            }
        }
    }

    public class CustomUILayer : YKLayer<Chara> {
        public override void OnLayout() {
            CreateTab<CustomUITab>("Custom AI Editor", "air1068.elin.customai.tab");
        }
        public override Rect Bound { get; } = new Rect(0f, 0f, 1300f, 600f);

        public override void Close() {
            List<Instruction> instructions = Data.GetObj<List<Instruction>>(ModInfo.MagicNumber);
            if (instructions.Count > 0) {
                //prevent save errors by removing the stored UIInputTexts as the UI closes
                foreach (Instruction i in instructions) {
                    i.input = null;
                }
            } else {
                //if the last instruction was deleted then remove the empty object
                Data.mapObj.Remove(ModInfo.MagicNumber);
            }
            base.Close();
        }
    }

    public class CustomUITab : YKLayout<Chara> {
        YKGrid instructionsGrid;
        Chara c;
        List<string> actions = new List<string>() { "Attack (Melee)", "Attack (Ranged)", "Attack (Thrown)", "Move (Away)", "Move (Towards)", "And", "Do Nothing" };

        void BuildMenu() {
            List<Instruction> instructions = c.GetObj<List<Instruction>>(ModInfo.MagicNumber);
            instructionsGrid.Clear();
            for (int i = 0; i < instructions.Count; i++) {
                int tmp = i;
                instructionsGrid.Dropdown(new List<string> { "Target", "Player", "Self", "Ally" }, (index) => {
                    instructions[tmp].entity = index+1;
                    instructions[tmp].validated = false;
                }, instructions[tmp].entity-1);
                instructionsGrid.Dropdown(new List<string> { "HP", "MP", "SP", "Distance", "Status", "Summons" }, (index) => {
                    instructions[tmp].condition = index+1;
                    instructions[tmp].validated = false;
                }, instructions[tmp].condition-1);
                instructionsGrid.Dropdown(new List<string> { "=", "!=", ">", "<", ">=", "<=" }, (index) => {
                    instructions[tmp].comparison = index+1;
                    instructions[tmp].validated = false;
                }, instructions[tmp].comparison-1);
                instructions[tmp].input = instructionsGrid.InputText(instructions[tmp].testvalue, (uselessint) => {
                    //the UIInputText is stored in the Instruction itself so that its Text can be accessed here
                    instructions[tmp].testvalue = instructions[tmp].input.Text;
                    instructions[tmp].validated = false;
                });
                instructionsGrid.Dropdown(actions, (index) => {
                    instructions[tmp].action = actions[index];
                    instructions[tmp].validated = false;
                }, actions.FindIndex(a => a == instructions[tmp].action));
                instructionsGrid.Toggle("Entity=Target", instructions[tmp].PreserveEntityAsTarget, (setting) => {
                    instructions[tmp].PreserveEntityAsTarget = setting;
                    instructions[tmp].validated = false;
                });
                YKGrid buttonsGrid = instructionsGrid.Grid();
                buttonsGrid.Layout.constraintCount = 3;
                buttonsGrid.Layout.cellSize = new Vector2(50f, 30f);
                if (i > 0) {
                    buttonsGrid.Button("▲", () => {
                        (instructions[tmp-1], instructions[tmp]) = (instructions[tmp], instructions[tmp-1]);
                        BuildMenu();
                    });
                } else {
                    buttonsGrid.Text("");
                }
                if (i < instructions.Count - 1) {
                    buttonsGrid.Button("▼", () => {
                        (instructions[tmp+1], instructions[tmp]) = (instructions[tmp], instructions[tmp+1]);
                        BuildMenu();
                    });
                } else {
                    buttonsGrid.Text("");
                }
                buttonsGrid.Button("-", () => {
                    instructions.RemoveAt(tmp);
                    if (instructions.Count > 0) {
                        BuildMenu();
                    } else {
                        Layer.Close();
                    }
                });
            }
            instructionsGrid.Button("+", () => {
                instructions.Add(new Instruction());
                BuildMenu();
            });
        }

        public override void OnLayout() {
            c = Layer.Data;
            foreach (ActList.Item item in c.ability.list.items) {
                actions.Add(item.act.Name);
            }
            if (c.GetObj<List<Instruction>>(ModInfo.MagicNumber) == null) {
                c.SetObj(ModInfo.MagicNumber, new List<Instruction>());
                c.GetObj<List<Instruction>>(ModInfo.MagicNumber).Add(new Instruction());
            }
            instructionsGrid = base.Grid();
            instructionsGrid.Layout.constraintCount = 7;
            instructionsGrid.Layout.cellSize = new Vector2(180f, 40f);
            BuildMenu();
        }
    }

    public class GoalCustomAICombat : GoalCombat {
        public override IEnumerable<Status> Run() {
            //begin default combat behavior
            if (EClass._zone.isPeace) {
                owner.enemy = null;
                owner.ShowEmo(Emo.happy);
                yield return Success();
            }
            if (EClass.pc.isHidden && owner.isHidden) {
                owner.enemy = null;
                yield return Success();
            }
            tc = owner.enemy;
            if (tc == null || tc.isDead || !tc.ExistsOnMap || !tc.pos.IsInBounds || !owner.CanSee(tc)) {
                owner.FindNewEnemy();
                if (owner.enemy == null) {
                    yield return Success();
                }
                tc = owner.enemy;
            }
            if (tc.IsPCFaction && EClass.rnd(5) == 0) {
                if (tc.enemy == owner) {
                    tc.enemy = null;
                    tc.hostility = tc.OriginalHostility;
                }
                owner.enemy = null;
                owner.Say("calmdown", owner);
                yield return Success();
            }
            if (tc.enemy != null) {
                tc.TrySetEnemy(owner);
            }
            if (EClass.rnd(20) == 0 && owner.isRestrained) {
                owner.Talk("restrained");
            }
            //end default combat behavior
            List<Instruction> andqueue = new List<Instruction>();
            foreach (Instruction i in owner.GetObj<List<Instruction>>(ModInfo.MagicNumber)) {
                //First, if there's a block of "And" conditions, collect all of them.
                if (i.action == "And") {
                    andqueue.Add(i);
                    continue;
                }
                //Then, check every "And" condition at once.
                //If they're all valid, then the following instruction gets checked as normal.
                //If any of them is invalid, the following instruction gets ignored.
                //In either case, the queue is cleared and ready for the next "And" block.
                if (andqueue.Count > 0) {
                    if (!andqueue.All(andi => andi.IsValid(owner))) {
                        andqueue.Clear();
                        continue;
                    }
                    andqueue.Clear();
                }
                if (i.IsValid(owner)) {
                    Chara target = tc;
                    if (i.PreserveEntityAsTarget) {
                        if (i.entity == AI.SELF) {
                            target = owner;
                        } else if (i.entity == AI.PLAYER) {
                            target = EClass.pc;
                        } else if (i.entity == AI.ALLY) {
                            target = i.GetAlly(owner);
                        }
                    }
                    if (i.action == "Attack (Melee)") {
                        owner.UseAbility(ACT.Melee, target);
                    } else if (i.action == "Attack (Ranged)") {
                        owner.UseAbility(ACT.Ranged, target);
                    } else if (i.action == "Attack (Thrown)") {
                        if (owner.Dist(target) <= owner.GetSightRadius()) {
                            Thing throwingweapon = owner.TryGetThrowable();
                            if (throwingweapon != null && ACT.Throw.CanPerform(owner, target, target.pos)) {
                                ActThrow.Throw(owner, target.pos, target, throwingweapon.HasElement(410) ? throwingweapon : throwingweapon.Split(1));
                            }
                        }
                    } else if (i.action == "Move (Towards)") {
                        if (owner.isBlind) {
                            owner.MoveRandom();
                        } else {
                            if (!owner.HasCondition<ConFear>() || owner.Dist(target) > 2 || target == EClass.pc) {
                                owner.TryMoveTowards(target.pos);
                            }
                        }
                    } else if (i.action == "Move (Away)") {
                        if (owner.isBlind) {
                            owner.MoveRandom();
                        } else {
                            owner.TryMoveFrom(target.pos);
                        }
                    } else if (i.action == "Do Nothing") {
                        owner.UseAbility(ACT.Wait);
                    } else {
                        owner.UseAbility(owner.ability.list.items.First(item => item.act.Name == i.action).act, target);
                    }
                    yield return Success();
                }
            }
            Msg.Say(owner.NameSimple + " doesn't know how to respond to the situation based on the instructions you've given " + (owner.IsMale ? "him!" : "her!"));
            yield return Success();
        }
    }

    public class Instruction {
        public bool validated;
        public int entity;
        public int condition;
        public int comparison;
        public string testvalue;
        public string action;
        public bool PreserveEntityAsTarget;
        public UIInputText input;

        public Instruction() {
            validated = false;
            entity = AI.TARGET;
            condition = AI.DISTANCE;
            comparison = AI.GREATER_THAN;
            testvalue = "1";
            action = "Move (Towards)";
            PreserveEntityAsTarget = false;
        }

        public override string ToString() {
            return entity.ToString() + "," + condition.ToString() + "," + comparison.ToString() + "," + testvalue + "," + action + "," + PreserveEntityAsTarget.ToString();
        }

        public bool IsValid(Chara owner, Chara target = null) {
            Chara tc = target;
            if (tc == null) {
                if (entity == AI.ALLY) {
                    foreach (Chara ally in EClass.pc.party.members) {
                        if (this.IsValid(owner, ally)) { return true; }
                    }
                    return false;
                } else if (entity == AI.SELF) {
                    tc = owner;
                } else if (entity == AI.PLAYER) {
                    tc = EClass.pc;
                } else {
                    tc = owner.enemy;
                }
            }
            if (!this.validated) {
                if (entity == AI.SELF && PreserveEntityAsTarget && (action == "Move (Away)" || action == "Move (Towards)")) {
                    Msg.Say("Invalid move target.");
                    return false;
                }
                if (action != "Attack (Melee)" && action != "Attack (Ranged)" && action != "Attack (Thrown)" && action != "Move (Away)" && action != "Move (Towards)" && action != "And" && action != "Do Nothing") {
                    if (!owner.ability.list.items.Any(i => i.act.Name == action)) {
                        Msg.Say("Action not available: " + action);
                        return false;
                    }
                }
                if (condition == AI.HP || condition == AI.MP || condition == AI.SP) {
                    if (!int.TryParse(testvalue, out _)) {
                        if (testvalue[testvalue.Length - 1] == '%') {
                            if (!int.TryParse(testvalue.Substring(0, testvalue.Length - 1), out _)) {
                                Msg.Say("Invalid number: " + testvalue);
                                return false;
                            }
                        }
                    }
                }
                else if (condition == AI.DISTANCE) {
                    if (!int.TryParse(testvalue, out _)) {
                        Msg.Say("Invalid number: " + testvalue);
                        return false;
                    } else if (int.Parse(testvalue) < 1) {
                        Msg.Say("Distance too low: " + testvalue);
                        return false;
                    }
                }
                else if (condition == AI.SUMMONS) {
                    if (!int.TryParse(testvalue, out _) && testvalue != "100%") {
                        Msg.Say("Invalid number: " + testvalue);
                        return false;
                    } else if (int.Parse(testvalue) < 0) {
                        Msg.Say("Summon count too low: " + testvalue);
                        return false;
                    }
                }
                else if (condition == AI.STATUS) {
                    if (comparison != AI.EQUALS && comparison != AI.DOES_NOT_EQUAL) {
                        Msg.Say("Status comparison can only be \"=\" or \"!=\".");
                        return false;
                    }
                    if (testvalue != "Spiky" && testvalue != "SuicideBomb" && testvalue != "Haste" && testvalue != "Slow" && !EClass.sources.stats.alias.ContainsKey(testvalue)) {
                        foreach (var row in EClass.sources.stats.rows) {
                            if (row.name == testvalue || row.name_JP == testvalue) {
                                testvalue = row.alias;
                                break;
                            }
                        }
                        if (!EClass.sources.stats.alias.ContainsKey(testvalue)) {
                            Msg.Say("Unknown status: " + testvalue);
                            return false;
                        }
                    }
                }
                this.validated = true;
            }
            if (tc == owner && PreserveEntityAsTarget && (action == "Move (Away)" || action == "Move (Towards)")) {
                return false; //in case "Ally" selects the pet (validation only checks for "Self")
            }
            int left = 0;
            float right = 0f;
            if (condition == AI.HP) {
                left = tc.hp;
                if (testvalue[testvalue.Length - 1] == '%') {
                    right = (float.Parse(testvalue.Substring(0, testvalue.Length - 1)) / 100) * tc.MaxHP;
                } else {
                    right = int.Parse(testvalue);
                }
            } else if (condition == AI.MP) {
                left = tc.mana.value;
                if (testvalue[testvalue.Length - 1] == '%') {
                    right = (float.Parse(testvalue.Substring(0, testvalue.Length - 1)) / 100) * tc.mana.max;
                } else {
                    right = int.Parse(testvalue);
                }
            } else if (condition == AI.SP) {
                left = tc.stamina.value;
                if (testvalue[testvalue.Length - 1] == '%') {
                    right = (float.Parse(testvalue.Substring(0, testvalue.Length - 1)) / 100) * tc.stamina.max;
                } else {
                    right = int.Parse(testvalue);
                }
            } else if (condition == AI.DISTANCE) {
                left = owner.pos.Distance(tc.pos);
                right = int.Parse(testvalue);
            } else if (condition == AI.SUMMONS) {
                left = EClass._zone.CountMinions(tc);
                if (testvalue == "100%") {
                    right = tc.MaxSummon;
                } else {
                    right = int.Parse(testvalue);
                }
            } else if (condition == AI.STATUS) {
                if (testvalue == "Spiky") {
                    if (tc.HasElement(1221)) { //featSpike
                        left = 1;
                    }
                } else if (testvalue == "SuicideBomb") {
                    if (tc.ability.list.items.Any(i => i.act.Name == "Suicide Bomb")) {
                        left = 1;
                    }
                } else {
                    foreach (Condition cond in tc.conditions) {
                        if (testvalue == "Haste" || testvalue == "Slow") {
                            if (cond.id == 34) { //ConBuffStats
                                if (cond.refVal2 == 222 && testvalue == "Slow") { //isDebuff
                                    left = 1;
                                    break;
                                } else if (cond.refVal2 != 222 && testvalue == "Haste") {
                                    left = 1;
                                    break;
                                }
                            }
                        } else if (cond.id == EClass.sources.stats.alias[testvalue].id) {
                            left = 1;
                            break;
                        }
                    }
                }
                right = 1;
            }
            switch (comparison) {
                case AI.EQUALS:
                    if (left != right) {
                        return false;
                    }
                    break;
                case AI.DOES_NOT_EQUAL:
                    if (left == right) {
                        return false;
                    }
                    break;
                case AI.GREATER_THAN:
                    if (left <= right) {
                        return false;
                    }
                    break;
                case AI.LESS_THAN:
                    if (left >= right) {
                        return false;
                    }
                    break;
                case AI.GREATER_THAN_OR_EQUALS:
                    if (left < right) {
                        return false;
                    }
                    break;
                case AI.LESS_THAN_OR_EQUALS:
                    if (left > right) {
                        return false;
                    }
                    break;
            }
            return true;
        }
        public Chara GetAlly(Chara owner) {
            foreach (Chara ally in EClass.pc.party.members) {
                if (this.IsValid(owner, ally)) { return ally; }
            }
            Msg.Say("ERROR: A valid ally was found before but is now inexplicably gone.");
            return owner;
        }
    }
}
