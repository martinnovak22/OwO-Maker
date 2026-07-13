using OwO_Maker.Core;
using OwO_Maker.Helpers;
using OwOMaker.Helpers;
using System;
using System.Threading.Tasks;

namespace OwO_Maker.Minigames
{
    class ShootingRange
    {
        private int playedGames = 0;

        Mem mem = new Mem();

        public async void RunTask(IntPtr hWnd, int Amount, ButtonResolutionHelper.ButtonResolution buttons, int BotID, int level, bool HumanTime, bool UseProdCoupon, int FailChance, uint ProductionsCouponKey, BotControl control, BotStats stats, bool unlimited)
        {
            var proc = mem.FindProcessByHandle(hWnd);

            mem.Init(proc);

            var TMinigameManger = mem.ReadMemory<IntPtr>(mem.FindPattern(Structs.Pattern.TMiniGameManager) + 1, [0x0]);
            var TMiniGamePoints = mem.ReadMemory<IntPtr>(mem.FindPattern(Structs.Pattern.TMiniGamePoints) + 1, [0x0]);
            var TArrowWidget = mem.ReadMemory<IntPtr>(mem.FindPattern(Structs.Pattern.TArrowWidget) + 1, [0x0]);

            var requiredPoints = SharedRoutines.GetRequiredPoints(Structs.Minigame.ShootingRange, level);

            var Fail = SharedRoutines.CalculateFailChance(FailChance, new Random().NextDouble());

            if (FailChance <= 0)
                Fail = false;

            if (TMinigameManger is 0 || TMiniGamePoints is 0 || TArrowWidget is 0)
            {
                Program.form.RemoveBotFromList(BotID, true);
                Program.form.NotifyBotEnded(BotID, $"Unable to locate Memory signatures, Abort! — {stats.GetSummary()}");
                return;
            }

            string SuccessText() => stats.Attempts == 0 ? "-" : $"{stats.Successes}/{stats.Attempts} ({(int)Math.Round(stats.SuccessRate * 100)} %)";

            while (control.ShouldContinue)
            {
                await control.WaitIfPausedAsync();
                if (!control.ShouldContinue) return;

                if (proc.HasExited)
                {
                    Program.form.NotifyBotEnded(BotID, $"Client closed! {stats.GetSummary()}");
                    Program.form.RemoveBotFromList(BotID, true);
                    return;
                }

                var manager = TMinigameManger;
                var MiniGameID = SharedRoutines.GetCurrentMiniGameID(mem, TMinigameManger);
                var productionPoints = mem.ReadMemory<int>(TMiniGamePoints + Structs.TMiniGamePoints.ProductionPoints);
                var currentMiniGame = mem.ReadMemory<IntPtr>(manager + Structs.TMiniGameManager.CurrentMinigamePtr);
                var m_iCurrentMiniGame = mem.ReadMemory<byte>(manager + Structs.TMiniGameManager.CurrentMinigameType);

                if (mem.ReadMemory<bool>(currentMiniGame + Structs.ShootingRange.IsVisible) && m_iCurrentMiniGame is (int)Structs.Minigame.ShootingRange) // only process if we are on the right minigame
                {
                    var hp = mem.ReadMemory<byte>(currentMiniGame + Structs.ShootingRange.HP);
                    var points = mem.ReadMemory<ushort>(currentMiniGame + Structs.ShootingRange.Points);
                    var ammo = mem.ReadMemory<byte>(currentMiniGame + Structs.ShootingRange.Ammo);

                    var leftChickenPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.ShootingRange.ChickenLeftPaddle, [Structs.TimingShotGame.Data, 0x0], 500);
                    var rightChickenPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.ShootingRange.ChickenRightPaddle, [Structs.TimingShotGame.Data, 0x0], 500);
                    var leftBatPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.ShootingRange.BatLeftPaddle, [Structs.TimingShotGame.Data, 0x0], 500);
                    var rightBatPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.ShootingRange.BatRightPaddle, [Structs.TimingShotGame.Data, 0x0], 500);
                    var leftRoosterPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.ShootingRange.RoosterLeftPaddle, [Structs.TimingShotGame.Data, 0x0], 500);
                    var rightRoosterPaddleData = mem.ReadMemoryData(currentMiniGame + Structs.ShootingRange.RoosterRightPaddle, [Structs.TimingShotGame.Data, 0x0], 500);

                    var firstHitBox = mem.ReadMemory<int>(currentMiniGame + Structs.ShootingRange.ChickenLeftPaddle, [Structs.TimingShotGame.Hitbox, 0x0]);

                    var status = SharedRoutines.GetStatus(mem, currentMiniGame);

                    Program.form.UpdateStatus(BotID, "ShootingRange", level, points, productionPoints, $"{playedGames}/{(unlimited ? "∞" : Amount.ToString())}", SuccessText());

                    if (status is Structs.Status.Playing)
                    {
                        if (Fail && points >= requiredPoints / 2)
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        for (nint i = 478; i != (478 - firstHitBox); i--)
                        {
                            if (leftChickenPaddleData[i] > 0 || leftBatPaddleData[i] > 0 || leftRoosterPaddleData[i] > 0)
                            {
                                if (points < requiredPoints)
                                {
                                    await BackgroundHelper.SendKey(hWnd, BackgroundHelper.KeyCodes.VK_LEFT, 0);
                                    await Task.Delay(10);
                                    break;
                                }
                            }

                            if (rightChickenPaddleData[i] > 0 || rightBatPaddleData[i] > 0 || rightRoosterPaddleData[i] > 0)
                            {
                                if (points < requiredPoints)
                                {
                                    await BackgroundHelper.SendKey(hWnd, BackgroundHelper.KeyCodes.VK_RIGHT, 0);
                                    await Task.Delay(10);
                                    break;
                                }
                            }

                            if (ammo <= 0)
                            {
                                await BackgroundHelper.SendKey(hWnd, BackgroundHelper.KeyCodes.VK_UP, 0);
                                await Task.Delay(2);
                                break;
                            }

                        }
                    }
                    else
                    {
                        if (FailChance > 0)
                            Fail = SharedRoutines.CalculateFailChance(FailChance, new Random().NextDouble());

                        await Task.Delay(1_000 + new Random().Next(0, 100));

                        if (points >= requiredPoints && status is Structs.Status.GameEnd or Structs.Status.GameEnded1 or Structs.Status.GameEnded2)
                        {
                            await SharedRoutines.CollectReward(mem, TMiniGamePoints, playedGames + 1, Amount, hWnd, buttons, level);
                            stats.RecordSuccess();
                            playedGames++;
                        }
                        else
                        {
                            if (status is Structs.Status.GameEnd or Structs.Status.GameEnded1 or Structs.Status.GameEnded2)
                                stats.RecordFailure();
                            await SharedRoutines.FailTryAgain(hWnd, buttons);
                        }

                        if (playedGames >= Amount)
                        {
                            Program.form.UpdateStatus(BotID, "ShootingRange", level, points, productionPoints, $"{playedGames}/{(unlimited ? "∞" : Amount.ToString())}", SuccessText());
                            Program.form.RemoveBotFromList(BotID, true);
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
                                    Program.form.RemoveBotFromList(BotID, true);
                                    Program.form.NotifyBotEnded(BotID, $"Failed to use Productions Coupon! — Wrong Key selected? — No Item in selected Slot? — Empty Coupons? — {productionPoints.ToString()} != {mem.ReadMemory<int>(TMiniGamePoints + 0xC8).ToString()} — {stats.GetSummary()}");
                                    return;
                                }
                            }
                            else
                            {
                                Program.form.RemoveBotFromList(BotID, true);
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
                                Program.form.RemoveBotFromList(BotID, true);
                                Program.form.NotifyBotEnded(BotID, $"Failed to use Productions Coupon! — Wrong Key selected? — No Item in selected Slot? — Empty Coupons? — {productionPoints.ToString()} != {mem.ReadMemory<int>(TMiniGamePoints + 0xC8).ToString()} — {stats.GetSummary()}");
                                return;
                            }
                        }
                        else
                        {
                            Program.form.RemoveBotFromList(BotID, true);
                            Program.form.NotifyBotEnded(BotID, unlimited ? $"Done! Ran out of production points. {stats.GetSummary()}" : $"no production points left! {stats.GetSummary()}");
                            return;
                        }
                    }

                    // Game not open, try to find nearest game
                    var arrow = SharedRoutines.FindMinigameArrowButton(mem, TArrowWidget, buttons);

                    if (arrow is not null)
                        await SharedRoutines.EnterMinigame(mem, hWnd, arrow, buttons);
                    else
                    {
                        Program.form.RemoveBotFromList(BotID, true);
                        Program.form.NotifyBotEnded(BotID, $"Failed to open Minigame, Abort! — {stats.GetSummary()}");
                        return;
                    }
                }
                await Task.Delay(5);
            }
        }
    }
}
