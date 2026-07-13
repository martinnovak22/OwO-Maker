using OwO_Maker.Core;
using OwO_Maker.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static OwO_Maker.Helpers.Structs;
using static OwOMaker.Helpers.Mem;

namespace OwO_Maker
{
    public partial class Form1 : Form
    {

        private List<int> HWndList = new List<int>();
        private List<IntPtr> WindowList = new List<IntPtr>();
        private List<BotEntry> BotList = new List<BotEntry>();

        public bool Reset { get; private set; }

        private bool shutdownScheduled;

        [DllImport("user32.dll")]
        static extern bool SetWindowText(IntPtr hWnd, string text);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern int EnumWindows(CallbackDef callback, int lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(int hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        private delegate bool CallbackDef(int hWnd, int lParam);

        private void RefreshHandle()
        {
            this.HWndList.Clear();
            this.listBox1.Items.Clear();
            CallbackDef callback = new CallbackDef(this.ShowWindowHandler);
            EnumWindows(callback, 0);
        }

        private bool ShowWindowHandler(int hWnd, int lParam)
        {
            StringBuilder stringBuilder = new StringBuilder(255);
            GetWindowText(hWnd, stringBuilder, 255);
            string text = stringBuilder.ToString();
            if (text.Contains("NosTale"))
            {
                GetWindowRect(hWnd, out var tempRect);
                if (tempRect.Right != 0 && tempRect.Bottom != 0)
                {
                    this.HWndList.Insert(0, hWnd);
                    this.listBox1.Items.Insert(0, "NosTale - (" + hWnd.ToString() + ")");
                    if (!Reset)
                        SetWindowText((IntPtr)hWnd, "NosTale - (" + hWnd.ToString() + ")");
                    else
                        SetWindowText((IntPtr)hWnd, "NosTale");
                }
            }

            if (this.listBox1.Items.Count > 0)
                this.listBox1.SelectedIndex = listBox1.Items.Count - 1;

            return true;
        }


        public Form1()
        {
            InitializeComponent();

            // Per-bot context menu for the "Running Bots" list (built in code, not the Designer)
            var botMenu = new ContextMenuStrip();
            var startItem = new ToolStripMenuItem("Start");
            var pauseItem = new ToolStripMenuItem("Pause");
            var resumeItem = new ToolStripMenuItem("Resume");
            var stopItem = new ToolStripMenuItem("Stop");
            botMenu.Items.AddRange(new ToolStripItem[] { startItem, pauseItem, resumeItem, stopItem });

            botMenu.Opening += (s, e) =>
            {
                var entry = GetSelectedBotEntry();
                if (entry == null)
                {
                    e.Cancel = true;
                    return;
                }

                var state = entry.Control.State;
                startItem.Enabled = state == BotState.Created;
                pauseItem.Enabled = state == BotState.Running;
                resumeItem.Enabled = state == BotState.Paused;
                stopItem.Enabled = state != BotState.Stopped;
            };

            startItem.Click += (s, e) =>
            {
                var entry = GetSelectedBotEntry();
                if (entry != null && entry.Control.Start() && !entry.ThreadStarted)
                {
                    entry.Thread.Start();
                    entry.ThreadStarted = true;
                }
            };
            pauseItem.Click += (s, e) => GetSelectedBotEntry()?.Control.Pause();
            resumeItem.Click += (s, e) => GetSelectedBotEntry()?.Control.Resume();
            stopItem.Click += (s, e) =>
            {
                var entry = GetSelectedBotEntry();
                if (entry != null)
                {
                    entry.Control.Stop();
                    RemoveBotFromList(entry.BotId);
                }
            };

            listView1.ContextMenuStrip = botMenu;

            // Reduce flicker while the Progress column is owner-drawn.
            typeof(System.Windows.Forms.Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(listView1, true, null);

            // Render the Progress column (index 5) as a proportional bar and the Action column (index 7) as a button.
            listView1.OwnerDraw = true;
            listView1.DrawColumnHeader += (s, e) => { e.DrawDefault = true; };
            listView1.DrawItem += (s, e) => { /* subitems do the drawing in Details view */ };
            listView1.DrawSubItem += ListView1_DrawSubItem;

            // Clicking the Action cell toggles the bot: Start (Created) / Pause (Running) / Resume (Paused).
            listView1.MouseClick += (s, e) =>
            {
                var hit = listView1.HitTest(e.Location);
                if (hit.Item == null || hit.SubItem == null || hit.Item.SubItems.IndexOf(hit.SubItem) != 7)
                    return;

                if (!int.TryParse(hit.Item.SubItems[0].Text, out int botId))
                    return;

                var entry = BotList.Where(x => x.BotId == botId).FirstOrDefault();
                if (entry == null)
                    return;

                switch (entry.Control.State)
                {
                    case BotState.Created:
                        if (entry.Control.Start() && !entry.ThreadStarted)
                        {
                            entry.Thread.Start();
                            entry.ThreadStarted = true;
                        }
                        break;
                    case BotState.Running:
                        entry.Control.Pause();
                        break;
                    case BotState.Paused:
                        entry.Control.Resume();
                        break;
                }
            };

            if (!Properties.Settings.Default.Disclaimer)
            {
                MessageBox.Show("DISCLAIMER:\n\n IF U PAID FOR THIS SOFTWARE U GOT SCAMMED!!!\n\n\n" +
                  "This Bot was made by Panda~ from Elitepvpers.com.\n\nNo Bot can gurantee u won't get banned for using Bots, so if u get banned for using this Software its your own fault!\n");
                Properties.Settings.Default.Disclaimer = true;
                Properties.Settings.Default.Save();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                MessageBox.Show($"This program must be started with Administrator privileges!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }
            else
                this.Text = $"{this.Text} [ADMINISTRATOR]";

            LoadSettings();
            RefreshHandle();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RefreshHandle();
        }

        private void Form_Closed(object sender, FormClosedEventArgs e)
        {
            SaveSettings();

            Reset = true;
            foreach (BotEntry entry in BotList)
                entry.Control.Stop();
            RefreshHandle();
        }

        private bool ValidatePlaySettings(out int levelValue, out int failChanceValue, out int amountValue, out uint prodkey)
        {
            failChanceValue = 0;
            amountValue = 0;
            prodkey = 0;

            if (!int.TryParse(t_Level.Text, out levelValue))
            {
                MessageBox.Show("Invalid Number for Level!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!int.TryParse(t_FailChance.Text, out failChanceValue) || failChanceValue < 0 || failChanceValue > 100)
            {
                MessageBox.Show("Invalid Number for Random Fail Min!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            else if (failChanceValue == 100)
            {
                MessageBox.Show("a Fail Chance of 100 will result into failing everytime!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!MaxGames.Checked && (!int.TryParse(t_Times.Text, out amountValue) || amountValue <= 0))
            {
                MessageBox.Show("Invalid Number for Amount!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            prodkey = GetProdKey(ProductionCouponKey.Text);

            if (prodkey == 0)
            {
                MessageBox.Show("Invalid Production Key!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        // Adds one bot for the given client window title. Returns null on success, otherwise the reason it was skipped.
        private string AddBotForClient(string Title, int levelValue, int failChanceValue, int amountValue, uint prodkey)
        {
            IntPtr ClientHWND = FindWindow("TNosTaleMainF", Title);

            if (ClientHWND == IntPtr.Zero)
                return "window not found (refresh the Client List)";

            RECT rect;
            GetClientRect(ClientHWND, out rect);
            string Resolution = (rect.Right.ToString() + "x" + rect.Bottom.ToString());

            var buttons = ButtonResolutionHelper.GetButtonPositions(Resolution);

            if (buttons is null)
                return $"unsupported Resolution ({Resolution})";

            if (WindowList.Contains(ClientHWND))
                return "already in the List";

            string BotID = Title.Replace("NosTale", "").Replace("- (", "").Replace(")", "").Replace(" ", "");
            if (!int.TryParse(BotID, out int botId))
                return "could not read Bot ID from the window title (refresh the Client List)";

            Minigame Game = GetWantedMinigame();
            bool unlimited = MaxGames.Checked;
            int Amount = unlimited ? int.MaxValue : amountValue;
            int Level = levelValue;
            int failchance = failChanceValue;

            var entry = new BotEntry { BotId = botId, ClientHwnd = ClientHWND };

            if ((int)Game == 0) { entry.Thread = new Thread(() => new Minigames.StoneQuarry().RunTask(FindWindow("TNosTaleMainF", Title), Amount, buttons, botId, Level, HumanTime.Checked, ProductionCoupon.Checked, failchance, prodkey, entry.Control, entry.Stats, unlimited)); }
            if ((int)Game == 1) { entry.Thread = new Thread(() => new Minigames.SawMill().RunTask(FindWindow("TNosTaleMainF", Title), Amount, buttons, botId, Level, HumanTime.Checked, ProductionCoupon.Checked, failchance, prodkey, entry.Control, entry.Stats, unlimited)); }
            if ((int)Game == 2) { entry.Thread = new Thread(() => new Minigames.ShootingRange().RunTask(FindWindow("TNosTaleMainF", Title), Amount, buttons, botId, Level, HumanTime.Checked, ProductionCoupon.Checked, failchance, prodkey, entry.Control, entry.Stats, unlimited)); }
            if ((int)Game == 3) { entry.Thread = new Thread(() => new Minigames.FishPond().RunTask(FindWindow("TNosTaleMainF", Title), Amount, buttons, botId, Level, HumanTime.Checked, ProductionCoupon.Checked, failchance, prodkey, entry.Control, entry.Stats, unlimited)); }

            if (entry.Thread == null)
                return "no Minigame selected";

            WindowList.Add(ClientHWND);
            BotList.Add(entry);

            entry.Control.StateChanged += s =>
            {
                if (s == BotState.Paused) entry.Stats.PauseRun();
                else if (s == BotState.Running) { entry.Stats.StartRun(); entry.Stats.ResumeRun(); }

                if (!IsDisposed) BeginInvoke(new Action(() => { var it = FindListViewItemByBotID(entry.BotId); if (it != null && it.SubItems.Count > 7) it.SubItems[7].Text = ActionLabel(s); }));
            };

            string[] row = { BotID, Game.ToString(), t_Level.Text, "0", "0", unlimited ? "0/∞" : $"0/{t_Times.Text}", "-", ActionLabel(BotState.Created) };
            var bot = new ListViewItem(row);
            listView1.Items.Add(bot);

            Log($"Bot {BotID} added: {Game}, level {t_Level.Text}, amount {(unlimited ? "∞" : t_Times.Text)}");

            return null;
        }

        private string PlaySettingsSummary()
        {
            return $"Minigame: {GetWantedMinigame()}\nWanted Level: {t_Level.Text}\n" +
                $"Amount: {(MaxGames.Checked ? "∞ (until points run out)" : t_Times.Text)}\n" +
                $"Human Time: {HumanTime.Checked.ToString()}\n" +
                $"Use Productions Coupon: {ProductionCoupon.Checked.ToString()}";
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null)
            {
                MessageBox.Show("No Client selected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!ValidatePlaySettings(out int levelValue, out int failChanceValue, out int amountValue, out uint prodkey))
                return;

            string Title = listBox1.SelectedItem.ToString();
            string error = AddBotForClient(Title, levelValue, failChanceValue, amountValue, prodkey);

            if (error != null)
            {
                MessageBox.Show($"{Title}: {error}!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show($"{Title} added to the Bot List!\n\n{PlaySettingsSummary()}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            SaveSettings();
        }

        private void buttonAddAll_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0)
            {
                MessageBox.Show("No Clients found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!ValidatePlaySettings(out int levelValue, out int failChanceValue, out int amountValue, out uint prodkey))
                return;

            int added = 0;
            var skipped = new List<string>();

            foreach (var item in listBox1.Items)
            {
                string Title = item.ToString();
                string error = AddBotForClient(Title, levelValue, failChanceValue, amountValue, prodkey);

                if (error == null)
                    added++;
                else
                    skipped.Add($"{Title}: {error}");
            }

            string message = $"{added} of {listBox1.Items.Count} Clients added to the Bot List!\n\n{PlaySettingsSummary()}";
            if (skipped.Count > 0)
                message += "\n\nSkipped:\n" + string.Join("\n", skipped);

            MessageBox.Show(message, added > 0 ? "Success" : "Error", MessageBoxButtons.OK, added > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            if (added > 0)
                SaveSettings();
        }

        private Minigame GetWantedMinigame()
        {
            int wantedGame = -1;
            if (StoneQuarry.Checked) { wantedGame = 0; }
            if (SawMill.Checked) { wantedGame = 1; }
            if (ShootingRange.Checked) { wantedGame = 2; }
            if (FishPond.Checked) { wantedGame = 3; }

            return (Minigame)Enum.Parse(typeof(Minigame), wantedGame.ToString(), true);

        }

        private uint GetProdKey(string text)
        {
            return text switch
            {
                "0" => (uint)BackgroundHelper.KeyCodes.VK_0,
                "1" => (uint)BackgroundHelper.KeyCodes.VK_1,
                "2" => (uint)BackgroundHelper.KeyCodes.VK_2,
                "3" => (uint)BackgroundHelper.KeyCodes.VK_3,
                "4" => (uint)BackgroundHelper.KeyCodes.VK_4,
                "5" => (uint)BackgroundHelper.KeyCodes.VK_5,
                "6" => (uint)BackgroundHelper.KeyCodes.VK_6,
                "7" => (uint)BackgroundHelper.KeyCodes.VK_7,
                "8" => (uint)BackgroundHelper.KeyCodes.VK_8,
                "9" => (uint)BackgroundHelper.KeyCodes.VK_9,
                _ => 0,
            };
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (BotList.Count == 0)
            {
                MessageBox.Show("No Bots Added!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int Amount = BotList.Count;
            foreach (BotEntry entry in BotList)
                entry.Control.Stop();

            BotList.Clear();
            WindowList.Clear();
            listView1.Items.Clear();
            Log($"{Amount} Bots have been Stopped and removed from List!");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (BotList.Count == 0)
            {
                MessageBox.Show("No Bots Added!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int started = 0;
            foreach (BotEntry entry in BotList)
            {
                if (entry.Control.Start())
                {
                    if (!entry.ThreadStarted)
                    {
                        entry.Thread.Start();
                        entry.ThreadStarted = true;
                    }
                    started++;
                }
                else if (entry.Control.State == BotState.Paused)
                {
                    if (entry.Control.Resume())
                        started++;
                }
            }

            if (started == 0)
                MessageBox.Show("No bots to start!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                Log($"{started} Bots have been started!");
        }

        private void buttonPauseAll_Click(object sender, EventArgs e)
        {
            int n = 0;
            foreach (BotEntry entry in BotList)
                if (entry.Control.Pause())
                    n++;
            Log($"{n} bots paused");
        }

        public void UpdateStatus(int botID, string game, int Level, int points, int prodPoints, string progress, string success)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                Invoke(new Action(() =>
                {
                    // Find item by botID
                    var item = FindListViewItemByBotID(botID);
                    string[] row = { botID.ToString(), game, Level.ToString(), points.ToString(), prodPoints.ToString(), progress, success };
                    if (item != null)
                        for (int i = 0; i < row.Length; i++)
                            item.SubItems[i].Text = row[i].ToString();

                }));
            }
            catch (ObjectDisposedException) { } // form closed while a bot was still ticking
            catch (InvalidOperationException) { }
        }

        public void RemoveBotFromList(int botID, bool finishedByBot = false)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                Invoke(new Action(() =>
                {
                    var entry = BotList.Where(x => x.BotId == botID).FirstOrDefault();
                    if (entry != null)
                    {
                        // Defensive: make sure the worker loop is told to stop
                        entry.Control.Stop();

                        // Remove from BotList
                        BotList.Remove(entry);

                        // Remove the client HWND so it can be added again later
                        WindowList.Remove(entry.ClientHwnd);
                    }

                    // Remove from ListView
                    listView1.Items.Remove(FindListViewItemByBotID(botID));

                    // Only bot-initiated endings count towards the shutdown trigger — a manual Stop means the user is at the PC.
                    if (finishedByBot && BotList.Count == 0 && ShutdownWhenDone.Checked)
                        ScheduleShutdown();
                }));
            }
            catch (ObjectDisposedException) { } // form closed while a bot was still ticking
            catch (InvalidOperationException) { }
        }

        private void ScheduleShutdown()
        {
            if (shutdownScheduled) return;
            shutdownScheduled = true;

            Process.Start(new ProcessStartInfo("shutdown", "/s /t 60") { CreateNoWindow = true, UseShellExecute = false });
            Log("All bots finished — PC will shut down in 60 seconds! Uncheck 'Shutdown PC when done' to abort.");
            System.Media.SystemSounds.Exclamation.Play();
        }

        private void ShutdownWhenDone_CheckedChanged(object sender, EventArgs e)
        {
            if (!ShutdownWhenDone.Checked && shutdownScheduled)
            {
                Process.Start(new ProcessStartInfo("shutdown", "/a") { CreateNoWindow = true, UseShellExecute = false });
                shutdownScheduled = false;
                Log("Shutdown aborted.");
            }
        }

        private BotEntry GetSelectedBotEntry()
        {
            if (listView1.SelectedItems.Count == 0)
                return null;

            if (!int.TryParse(listView1.SelectedItems[0].SubItems[0].Text, out int botID))
                return null;

            return BotList.Where(x => x.BotId == botID).FirstOrDefault();
        }

        private ListViewItem FindListViewItemByBotID(int BotID)
        {
            foreach (ListViewItem item in listView1.Items)
                if (item.SubItems[0].Text == BotID.ToString())
                    return item;
            return null;
        }

        private static string ActionLabel(BotState state)
        {
            return state switch
            {
                BotState.Created => "Start",
                BotState.Running => "Pause",
                BotState.Paused => "Resume",
                _ => "-",
            };
        }

        private void ListView1_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            string text = e.SubItem?.Text ?? string.Empty;

            // The Action column (index 7) renders as a clickable button.
            if (e.ColumnIndex == 7)
            {
                var buttonBounds = Rectangle.Inflate(e.Bounds, -1, -1);
                ButtonRenderer.DrawButton(e.Graphics, buttonBounds, text, e.SubItem.Font, false,
                    System.Windows.Forms.VisualStyles.PushButtonState.Normal);
                return;
            }

            // Only the Progress column (index 5) gets the custom bar; everything else is default.
            var parts = text.Split('/');
            if (e.ColumnIndex != 5 || parts.Length != 2 ||
                !int.TryParse(parts[0], out int done) || !int.TryParse(parts[1], out int total) || total <= 0)
            {
                e.DrawDefault = true;
                return;
            }

            bool selected = e.Item.Selected;
            Color backColor = selected ? SystemColors.Highlight : e.SubItem.BackColor;
            using (var bg = new SolidBrush(backColor))
                e.Graphics.FillRectangle(bg, e.Bounds);

            double fraction = done / (double)total;
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;

            var barBounds = Rectangle.Inflate(e.Bounds, -2, -2);
            int barWidth = (int)(barBounds.Width * fraction);
            if (barWidth > 0)
                using (var bar = new SolidBrush(Color.FromArgb(120, 180, 120)))
                    e.Graphics.FillRectangle(bar, barBounds.X, barBounds.Y, barWidth, barBounds.Height);

            Color textColor = selected ? SystemColors.HighlightText : e.SubItem.ForeColor;
            TextRenderer.DrawText(e.Graphics, text, e.SubItem.Font, e.Bounds, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        public void Log(string message)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                logList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                while (logList.Items.Count > 500)
                    logList.Items.RemoveAt(0);
                logList.TopIndex = logList.Items.Count - 1;
            }));
        }

        public void NotifyBotEnded(int botID, string message)
        {
            Log($"Bot {botID}: {message}");
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.HumanTime = HumanTime.Checked;
            Properties.Settings.Default.UseProdCoupon = ProductionCoupon.Checked;

            if (t_FailChance.Text.All(Char.IsDigit) && Convert.ToInt32(t_FailChance.Text) <= 100 && Convert.ToInt32(t_FailChance.Text) > 0)
                Properties.Settings.Default.FailChance = t_FailChance.Text;


            if (ProductionCouponKey.Text.All(Char.IsDigit))
            {
                var check = Convert.ToInt32(ProductionCouponKey.Text);

                if (check >= 0 && check < 10)
                    Properties.Settings.Default.ProdKey = ProductionCouponKey.Text;
            }

            if (int.TryParse(t_Times.Text, out int savedTimes) && savedTimes > 0)
                Properties.Settings.Default.Times = t_Times.Text;

            Properties.Settings.Default.MaxGames = MaxGames.Checked;

            if (int.TryParse(t_Level.Text, out int savedLevel) && savedLevel >= 1 && savedLevel <= 5)
                Properties.Settings.Default.Level = t_Level.Text;

            uint lastMinigame = 0;
            if (StoneQuarry.Checked)
                lastMinigame = 0;
            else if (SawMill.Checked)
                lastMinigame = 1;
            else if (ShootingRange.Checked)
                lastMinigame = 2;
            else if (FishPond.Checked)
                lastMinigame = 3;

            Properties.Settings.Default.LastMinigame = lastMinigame;

            Properties.Settings.Default.Save();
        }

        private void LoadSettings()
        {
            HumanTime.Checked = Properties.Settings.Default.HumanTime;
            ProductionCoupon.Checked = Properties.Settings.Default.UseProdCoupon;

            if (Properties.Settings.Default.FailChance.All(Char.IsDigit) && Convert.ToInt32(Properties.Settings.Default.FailChance) <= 100 && Convert.ToInt32(Properties.Settings.Default.FailChance) > 0)
                t_FailChance.Text = Properties.Settings.Default.FailChance;

            if (Properties.Settings.Default.ProdKey.All(Char.IsDigit))
            {
                var check = Convert.ToInt32(Properties.Settings.Default.ProdKey);

                if (check >= 0 && check < 10)
                    ProductionCouponKey.Text = Properties.Settings.Default.ProdKey;
            }

            if (int.TryParse(Properties.Settings.Default.Times, out int loadedTimes) && loadedTimes > 0)
                t_Times.Text = Properties.Settings.Default.Times;

            MaxGames.Checked = Properties.Settings.Default.MaxGames;

            if (int.TryParse(Properties.Settings.Default.Level, out int loadedLevel) && loadedLevel >= 1 && loadedLevel <= 5)
                t_Level.Text = Properties.Settings.Default.Level;

            switch (Properties.Settings.Default.LastMinigame)
            {
                case 0:
                    StoneQuarry.Checked = true;
                    break;
                case 1:
                    SawMill.Checked = true;
                    break;
                case 2:
                    ShootingRange.Checked = true;
                    break;
                case 3:
                    FishPond.Checked = true;
                    break;
                default:
                    StoneQuarry.Checked = true;
                    break;
            }
        }

        private void MaxGames_CheckedChanged(object sender, EventArgs e)
        {
            t_Times.Enabled = !MaxGames.Checked;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://www.elitepvpers.com/forum/nostale-hacks-bots-cheats-exploits/4716766-OwO-maker-nostale-minigame-bot-source-poc.html");
        }
    }
}
