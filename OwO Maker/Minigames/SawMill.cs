using OwO_Maker.Core;
using OwO_Maker.Helpers;
using OwOMaker.Helpers;
using System;
using System.Threading.Tasks;
using static OwO_Maker.Helpers.Structs;

namespace OwO_Maker.Minigames
{
    class SawMill
    {
        private int playedGames = 0;

        Mem mem = new Mem();

        public async void RunTask(IntPtr hWnd, int Amount, ButtonResolutionHelper.ButtonResolution buttons, int BotID, int level, bool HumanTime, bool UseProdCoupon, int FailChance, uint ProductionsCouponKey, BotControl control, BotStats stats, bool unlimited)
        {
            var proc = mem.FindProcessByHandle(hWnd);

            mem.Init(proc);

            var TMinigameManger = mem.ReadMemory<IntPtr>(mem.FindPattern(Pattern.TMiniGameManager) + 1, [0x0]);
            var TMiniGamePoints = mem.ReadMemory<IntPtr>(mem.FindPattern(Pattern.TMiniGamePoints) + 1, [0x0]);
            var TArrowWidget = mem.ReadMemory<IntPtr>(mem.FindPattern(Pattern.TArrowWidget) + 1, [0x0]);

            var requiredPoints = SharedRoutines.GetRequiredPoints(Minigame.SawMill, level) + 100; // Add additional 100 in case we lose enough

            var Fail = SharedRoutines.CalculateFailChance(FailChance, new Random().NextDouble());

            if (FailChance <= 0)
                Fail = false;

            if (TMinigameManger is 0 || TMiniGamePoints is 0 || TArrowWidget is 0)
            {
                Program.form.RemoveBotFromList(BotID);
                Program.form.NotifyBotEnded(BotID, $"Unable to locate Memory signatures, Abort! — {stats.GetSummary()}");
                return;
            }

            string SuccessText() => stats.Attempts == 0 ? "-" : $"{stats.Successes}/{stats.Attempts} ({(int)Math.Round(stats.SuccessRate * 100)} %)";

            stats.StartRun();

            while (control.ShouldContinue)
            {
                await control.WaitIfPausedAsync();
                if (!control.ShouldContinue) return;

                if (proc.HasExited)
                {
                    Program.form.NotifyBotEnded(BotID, $"Client closed! {stats.GetSummary()}");
                    Program.form.RemoveBotFromList(BotID);
                    return;
                }

                var manager = TMinigameManger;
                var MiniGameID = SharedRoutines.GetCurrentMiniGameID(mem, TMinigameManger);
                var productionPoints = mem.ReadMemory<int>(TMiniGamePoints + Structs.TMiniGamePoints.ProductionPoints);
                var currentMiniGame = mem.ReadMemory<IntPtr>(manager + TMiniGameManager.CurrentMinigamePtr);
                var m_iCurrentMiniGame = mem.ReadMemory<byte>(manager + TMiniGameManager.CurrentMinigameType);

                if (mem.ReadMemory<bool>(currentMiniGame + Structs.SawMill.IsVisible) && m_iCurrentMiniGame is (int)Minigame.SawMill) // only process if we are on the right minigame
                {
                    var hp = mem.ReadMemory<byte>(currentMiniGame + Structs.SawMill.Hp);
                    var points = mem.ReadMemory<ushort>(currentMiniGame + Structs.SawMill.Points);
                    var combo = mem.ReadMemory<byte>(currentMiniGame + Structs.SawMill.Combo);

                    var leftPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.SawMill.LeftPaddle, [TimingShotGame.Data, 0x0], 400);
                    var rightPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.SawMill.RightPaddle, [TimingShotGame.Data, 0x0], 400);
                    var firstHitBox = mem.ReadMemory<int>(currentMiniGame + Structs.SawMill.LeftPaddle, [TimingShotGame.Hitbox, 0x0]);

                    var status = SharedRoutines.GetStatus(mem, currentMiniGame);

                    Program.form.UpdateStatus(BotID, "SawMill", level, points, productionPoints, $"{playedGames}/{(unlimited ? "∞" : Amount.ToString())}", SuccessText());

                    if (status is Status.Playing)
                    {
                        if (Fail && points >= requiredPoints / 2)
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        for (int i = leftPaddleData.Length - 1; i != (HumanTime && combo is >= 5 ? 397 - firstHitBox + 38 : 327); i--)
                        {
                            if (leftPaddleData[i] == 1 && points < requiredPoints)
                            {
                                await BackgroundHelper.SendKey(hWnd, BackgroundHelper.KeyCodes.VK_LEFT, 0);
                                await Task.Delay(50);
                                break;
                            }

                            if (rightPaddleData[i] == 1 && points < requiredPoints)
                            {
                                await BackgroundHelper.SendKey(hWnd, BackgroundHelper.KeyCodes.VK_RIGHT, 0);
                                await Task.Delay(50);
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (FailChance > 0)
                            Fail = SharedRoutines.CalculateFailChance(FailChance, new Random().NextDouble());

                        await Task.Delay(1_000 + new Random().Next(0, 100));

                        if (points >= requiredPoints && status is Status.GameEnd or Status.GameEnded1 or Status.GameEnded2)
                        {
                            await SharedRoutines.CollectReward(mem, TMiniGamePoints, playedGames + 1, Amount, hWnd, buttons, level);
                            stats.RecordSuccess();
                            playedGames++;
                        }
                        else
                        {
                            if (status is Status.GameEnd or Status.GameEnded1 or Status.GameEnded2)
                                stats.RecordFailure();
                            await SharedRoutines.FailTryAgain(hWnd, buttons);
                        }

                        if (playedGames >= Amount)
                        {
                            Program.form.UpdateStatus(BotID, "SawMill", level, points, productionPoints, $"{playedGames}/{(unlimited ? "∞" : Amount.ToString())}", SuccessText());
                            Program.form.RemoveBotFromList(BotID);
                            Program.form.NotifyBotEnded(BotID, $"Done! {stats.GetSummary()}");
                            return;
                        }

                        // read prod games again
                        productionPoints = mem.ReadMemory<int>(TMiniGamePoints + Structs.TMiniGamePoints.ProductionPoints);

                        if (productionPoints < 100)
                        {
                            if (UseProdCoupon)
                            {
                                await SharedRoutines.UseProductionCoupon(hWnd, buttons, (uint)ProductionsCouponKey, true);

                                if (productionPoints == mem.ReadMemory<int>(TMiniGamePoints + Structs.TMiniGamePoints.ProductionPoints))
                                {
                                    Program.form.RemoveBotFromList(BotID);
                                    Program.form.NotifyBotEnded(BotID, $"Failed to use Productions Coupon! — Wrong Key selected? — No Item in selected Slot? — Empty Coupons? — {productionPoints.ToString()} != {mem.ReadMemory<int>(TMiniGamePoints + 0xC8).ToString()} — {stats.GetSummary()}");
                                    return;
                                }
                            }
                            else
                            {
                                Program.form.RemoveBotFromList(BotID);
                                Program.form.NotifyBotEnded(BotID, unlimited ? $"Done! Ran out of production points. {stats.GetSummary()}" : $"no production points left! {stats.GetSummary()}");
                                return;
                            }
                        }
                    }
                    await Task.Delay(5);
                }
                else
                {
                    if (productionPoints < 100)
                    {
                        if (UseProdCoupon)
                        {
                            await SharedRoutines.UseProductionCoupon(hWnd, buttons, ProductionsCouponKey, false);

                            if (productionPoints == mem.ReadMemory<int>(TMiniGamePoints + Structs.TMiniGamePoints.ProductionPoints))
                            {
                                Program.form.RemoveBotFromList(BotID);
                                Program.form.NotifyBotEnded(BotID, $"Failed to use Productions Coupon! — Wrong Key selected? — No Item in selected Slot? — Empty Coupons? — {productionPoints.ToString()} != {mem.ReadMemory<int>(TMiniGamePoints + 0xC8).ToString()}");
                                return;
                            }
                        }
                        else
                        {
                            Program.form.RemoveBotFromList(BotID);
                            Program.form.NotifyBotEnded(BotID, "no production points left!");
                            return;
                        }
                    }

                    // Game not open, try to find nearest game
                    var arrow = SharedRoutines.FindMinigameArrowButton(mem, TArrowWidget, buttons);

                    if (arrow is not null)
                        await SharedRoutines.EnterMinigame(mem, hWnd, arrow, buttons);
                    else
                    {
                        Program.form.RemoveBotFromList(BotID);
                        Program.form.NotifyBotEnded(BotID, $"Failed to open Minigame, Abort! — {stats.GetSummary()}");
                        return;
                    }
                }
                await Task.Delay(5);
            }
        }
    }
}
