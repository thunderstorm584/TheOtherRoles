  
using HarmonyLib;
using static BonusRoles.BonusRoles;
using static BonusRoles.GameHistory;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Hazel;
using UnhollowerBaseLib;
using System;
using System.Text;

namespace BonusRoles {
    enum WinCondition {
        Default,
        LoversTeamWin,
        LoversSoloWin,
        JesterWin,
        ChildDied,
        JackalWin
    }

    static class AdditionalTempData {
        // Should be implemented using a proper GameOverReason in the future
        public static WinCondition winCondition = WinCondition.Default;
        public static bool localIsLover = false;


        public static void clear() {
            winCondition = WinCondition.Default;
            localIsLover = false;

        }
    }

    

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public class OnGameEndPatch {
        public static void Postfix(AmongUsClient __instance, GameOverReason OFLKLGMHBEL, bool JFFPAKGPNJA) {
            AdditionalTempData.clear();

            // Remove shifter from winners
            if (Shifter.shifter != null) {
                WinningPlayerData shifterWinner = null;
                foreach (WinningPlayerData winner in TempData.winners)
                    if (winner.Name == Shifter.shifter.Data.PlayerName) shifterWinner = winner;
                
                if (shifterWinner != null) TempData.winners.Remove(shifterWinner);
            }
            
            // Remove Jester from winners (on Jester win he will be added again, see below)
            if (Jester.jester != null) {
                WinningPlayerData jesterWinner = null;
                foreach (WinningPlayerData winner in TempData.winners)
                    if (winner.Name == Jester.jester.Data.PlayerName) jesterWinner = winner;
                
                if (jesterWinner != null) TempData.winners.Remove(jesterWinner);
            }

            // Remove Jackal and Sidekick from winners (on Jackal win he will be added again, see below)
            if (Jackal.jackal != null || Sidekick.sidekick != null) {
                List<WinningPlayerData> winnersToRemove = new List<WinningPlayerData>();
                WinningPlayerData jackalWinner = null;
                WinningPlayerData sidekickWinner = null;
                foreach (WinningPlayerData winner in TempData.winners) {
                    if (winner.Name == Jackal.jackal?.Data?.PlayerName) winnersToRemove.Add(winner);
                    if (winner.Name == Sidekick.sidekick?.Data?.PlayerName) winnersToRemove.Add(winner);
                    foreach(var player in Jackal.formerJackals) {
                        if (winner.Name == player.Data.PlayerName) {
                            winnersToRemove.Add(winner);
                        }
                    }
                }
                
                if (jackalWinner != null) TempData.winners.Remove(jackalWinner);
                if (sidekickWinner != null) TempData.winners.Remove(sidekickWinner);
            }

            // Child win condition (should be implemented using a proper GameOverReason in the future)
            if (Child.child != null && Child.child.Data.IsImpostor) {
                AdditionalTempData.winCondition = WinCondition.ChildDied;
                TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
                WinningPlayerData wpd = new WinningPlayerData(Child.child.Data);
                wpd.IsImpostor = false;
                wpd.IsYou = false;
                TempData.winners.Add(wpd);
            }

            // Jester win condition (should be implemented using a proper GameOverReason in the future)
            else if (Jester.jester != null && Jester.jester.Data.IsImpostor) {
                AdditionalTempData.winCondition = WinCondition.JesterWin;
                TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
                WinningPlayerData wpd = new WinningPlayerData(Jester.jester.Data);
                wpd.IsImpostor = false; 
                TempData.winners.Add(wpd);
            }
            
            // Jackal win condition (should be implemented using a proper GameOverReason in the future)
            else if ((Jackal.jackal != null && !Jackal.jackal.Data.IsDead)) {
                BonusRolesPlugin.Logger.LogMessage("OnGameEndPatch: Jackal win");
                // Jackal wins if nobody except jackal is alive
                AdditionalTempData.winCondition = WinCondition.JackalWin;
                TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
                WinningPlayerData wpd = new WinningPlayerData(Jackal.jackal.Data);
                wpd.IsImpostor = false; 
                TempData.winners.Add(wpd);
                // If there is a sidekick. The sidekick also wins
                if (Sidekick.sidekick != null) {
                    WinningPlayerData wpdSidekick = new WinningPlayerData(Sidekick.sidekick.Data);
                    wpdSidekick.IsImpostor = false; 
                    TempData.winners.Add(wpdSidekick);
                }
                foreach(var player in Jackal.formerJackals) {
                    WinningPlayerData wpdFormerJackal = new WinningPlayerData(player.Data);
                    wpdFormerJackal.IsImpostor = false; 
                    TempData.winners.Add(wpdFormerJackal);
                }
            }

            // Lovers win conditions (should be implemented using a proper GameOverReason in the future)
            else if (Lovers.existingAndAlive()) {
                AdditionalTempData.localIsLover = (PlayerControl.LocalPlayer == Lovers.lover1 || PlayerControl.LocalPlayer == Lovers.lover2);
                // Double win for lovers, crewmates also win
                if (TempData.DidHumansWin(OFLKLGMHBEL)) {
                    AdditionalTempData.winCondition = WinCondition.LoversTeamWin;
                }
                // Lovers solo win
                else if (Lovers.existingWithImpLover()){
                    AdditionalTempData.winCondition = WinCondition.LoversSoloWin;
                    TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
                    TempData.winners.Add(new WinningPlayerData(Lovers.lover1.Data));
                    TempData.winners.Add(new WinningPlayerData(Lovers.lover2.Data));
                }
            }

            BonusRolesPlugin.Logger.LogMessage("OnGameEndPatch: Resetting roles");
            // Reset Bonus Roles Settings
            clearAndReloadRoles();
            clearGameHistory();
        }
    }

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
    public class EndGameManagerSetUpPatch {
        public static void Postfix(EndGameManager __instance) {
            GameObject bonusText = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
            bonusText.transform.position = new Vector3(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.8f, __instance.WinText.transform.position.z);
            bonusText.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            TextRenderer textRenderer = bonusText.GetComponent<TextRenderer>();
            textRenderer.Text = "";

            if (AdditionalTempData.winCondition == WinCondition.ChildDied) {
                textRenderer.Text = "Child Died";
                textRenderer.Color = Child.color;
            }
            else if (AdditionalTempData.winCondition == WinCondition.JesterWin) {
                textRenderer.Text = "Jester Wins";
                textRenderer.Color = Jester.color;
            }
            else if (AdditionalTempData.winCondition == WinCondition.LoversTeamWin) {
                if (AdditionalTempData.localIsLover) {
                    __instance.WinText.Text = "Double Victory";
                } 
                textRenderer.Text = "Lovers And Crewmates Win";
                textRenderer.Color = Lovers.color;
                __instance.BackgroundBar.material.SetColor("_Color", Lovers.color);
            } 
            else if (AdditionalTempData.winCondition == WinCondition.LoversSoloWin) {
                textRenderer.Text = "Lovers Win";
                textRenderer.Color = Lovers.color;
                __instance.BackgroundBar.material.SetColor("_Color", Lovers.color);
            }
            else if (AdditionalTempData.winCondition == WinCondition.JackalWin) {
                textRenderer.Text = "Jackal Wins";
                textRenderer.Color = Jackal.color;
            }
            
            AdditionalTempData.clear();
        }
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CheckEndCriteria))] 
    class CheckEndCriteriaPatch{
        private static void ReviveEveryone() {
            for (int i = 0; i < GameData.Instance.PlayerCount; i++)
                GameData.Instance.AllPlayers[i].Object.Revive();
            DeadBody[] array = UnityEngine.Object.FindObjectsOfType<DeadBody>();
            for (int i = 0; i < array.Length; i++) UnityEngine.Object.Destroy(array[i].gameObject);
        }

        private static void EndGameForSabotage(ShipStatus __instance)
        {
            BonusRolesPlugin.Logger.LogMessage("Ending game due to sabotage");
            if (!DestroyableSingleton<TutorialManager>.InstanceExists)
            {
                __instance.enabled = false;
                ShipStatus.RpcEndGame(GameOverReason.ImpostorBySabotage, false);
                return;
            }
            DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverSabotage, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
        }

        public static bool Prefix(ShipStatus __instance) {
            if (!GameData.Instance)
            {
                return false;
            }
            ISystemType systemType = __instance.Systems.ContainsKey(SystemTypes.LifeSupp) ? __instance.Systems[SystemTypes.LifeSupp] : null;
            if (systemType != null)
            {
                LifeSuppSystemType lifeSuppSystemType = systemType.TryCast<LifeSuppSystemType>();
                if (lifeSuppSystemType.Countdown < 0f)
                {
                    EndGameForSabotage(__instance);
                    lifeSuppSystemType.Countdown = 10000f;
                }
            }
            ISystemType systemType2 = __instance.Systems.ContainsKey(SystemTypes.Reactor) ? __instance.Systems[SystemTypes.Reactor] : null;
            if (systemType2 == null) systemType2 = __instance.Systems.ContainsKey(SystemTypes.Laboratory) ? __instance.Systems[SystemTypes.Laboratory] : null;
            if (systemType2 != null)
            {
                ReactorSystemType reactorSystemType = systemType2.TryCast<ReactorSystemType>();
                if (reactorSystemType.Countdown < 0f)
                {
                    EndGameForSabotage(__instance);
                    reactorSystemType.Countdown = 10000f;
                }
            }

           
            int numNonImpostorAlive = 0;
            int numImpostorsAlive = 0;
            int numImpostorsDeadOrAlive = 0;
            for (int i = 0; i < GameData.Instance.PlayerCount; i++)
            {
                GameData.PlayerInfo playerInfo = GameData.Instance.AllPlayers[i];
                if (!playerInfo.Disconnected)
                {
                    if (playerInfo.IsImpostor)
                    {
                        numImpostorsDeadOrAlive++;
                    }
                    if (!playerInfo.IsDead)
                    {
                        if (playerInfo.IsImpostor)
                        {
                            numImpostorsAlive++;
                        }
                        else
                        {
                            numNonImpostorAlive++;
                        }
                    }
                }
            }

            var numTeamJackalAlive = 0;
            if (Jackal.jackal?.Data?.IsDead == false) numTeamJackalAlive++;
            if (Sidekick.sidekick?.Data?.IsDead == false) numTeamJackalAlive++;
            // BonusRolesPlugin.Logger.LogMessage($"{numTeamJackalAlive} Jackal members of total {numNonImpostorAlive + numImpostorsAlive} alive.");
            if (numTeamJackalAlive > 0 && numImpostorsAlive > 0 && numNonImpostorAlive + numImpostorsAlive > numTeamJackalAlive ) {
                // There is still a jackal/sidekick and an impostor alive
                return false;
            } 
            else if ((numNonImpostorAlive - numTeamJackalAlive) <= numTeamJackalAlive) {
                BonusRolesPlugin.Logger.LogMessage("No Impostors alive and Team Jackal is at least half of the members alive. Jackal Win");
                if (!DestroyableSingleton<TutorialManager>.InstanceExists)
                {
                    __instance.enabled = false;
                    ShipStatus.RpcEndGame(GameOverReason.ImpostorByKill, false);
                    return false;
                }
                DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverImpostorKills, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                ReviveEveryone();
                return false;
            }


            if (numImpostorsAlive <= 0 && (!DestroyableSingleton<TutorialManager>.InstanceExists || numImpostorsDeadOrAlive > 0))
            {
                BonusRolesPlugin.Logger.LogMessage("All impostors are dead");
                if (!DestroyableSingleton<TutorialManager>.InstanceExists)
                {
                    __instance.enabled = false;
                    ShipStatus.RpcEndGame(GameOverReason.HumansByVote, false);
                    return false;
                }
                DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverImpostorDead, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                ReviveEveryone();
                return false;
            }
            else
            {
                if (numNonImpostorAlive > numImpostorsAlive)
                {
                    bool localCompletedAllTasks = true;
                    foreach (PlayerTask t in PlayerControl.LocalPlayer.myTasks) {
                        localCompletedAllTasks = localCompletedAllTasks && t.IsComplete;
                    }

                    if (!DestroyableSingleton<TutorialManager>.InstanceExists)
                    {
                        if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
                        {
                            BonusRolesPlugin.Logger.LogMessage("All tasks were completed");
                            __instance.enabled = false;
                            ShipStatus.RpcEndGame(GameOverReason.HumansByTask, false);
                            return false;
                        }
                    }
                    else if (localCompletedAllTasks)
                    {
                        DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverTaskWin, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                        __instance.Begin();
                    }
                    if (numNonImpostorAlive + numImpostorsAlive == 3 && Lovers.existingAndAlive()) { // 3 players with 2 lovers is always a lover win (either shared with crewmates or solo for lovers, marked as impostor win)
                        __instance.enabled = false;
                        BonusRolesPlugin.Logger.LogMessage("Lovers win");
                        ShipStatus.RpcEndGame(Lovers.existingWithImpLover() ? GameOverReason.ImpostorByKill : GameOverReason.HumansByVote, false); // should be implemented using a proper GameOverReason in the future
                        return false;
                    }
                    return false;
                }
                if (numNonImpostorAlive == numImpostorsAlive && Lovers.existingAndAlive() && Lovers.existingWithImpLover()) { // 3 vs 3 or 2 vs 2 is not win if both lovers are alive and one is an impostor
                    BonusRolesPlugin.Logger.LogMessage("Lovers do not win?");
                    return false;
                }
                
                BonusRolesPlugin.Logger.LogMessage("Impostors win after kill / exile");
                if (!DestroyableSingleton<TutorialManager>.InstanceExists)
                {
                    __instance.enabled = false;
                    GameOverReason endReason;
                    switch (TempData.LastDeathReason)
                    {
                    case DeathReason.Exile:
                        endReason = GameOverReason.ImpostorByVote;
                        break;
                    case DeathReason.Kill:
                        endReason = GameOverReason.ImpostorByKill;
                        break;
                    default:
                        endReason = GameOverReason.ImpostorByVote;
                        break;
                    }
                    ShipStatus.RpcEndGame(endReason, false);
                    return false;
                }
                DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverImpostorKills, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                
                ReviveEveryone();
                return false;
            }
        }
    }
}