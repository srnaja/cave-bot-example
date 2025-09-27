using cavebot_otp.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace MemoryReader
{
    public partial class Form1 : Form
    {
        // Importações da API do Windows
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_READ = 0x0010;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const int WS_EX_TRANSPARENT = 0x20;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private TextBox txtPlayerName;
        private Button btnStartMonitor;
        private Button btnStopMonitor;
        private Button btnReattachProcess;
        private Label lblStatus;
        private Label lblProcessInfo;
        private CheckBox chkShowOverlay;
        private System.Timers.Timer monitorTimer;
        private System.Timers.Timer processCheckTimer;

        private OverlayForm overlayForm;

        private Process targetProcess;
        private IntPtr processHandle;
        private IntPtr gameWindowHandle;
        private List<PlayerData> currentPlayers;

        private Dictionary<string, CachedPlayerAddress> addressCache;
        private DateTime lastFullScanTime;
        private TimeSpan cacheDuration = TimeSpan.FromMinutes(5);

        public class PlayerData
        {
            public string Name { get; set; }
            public ushort X { get; set; }
            public ushort Y { get; set; }
            public byte Z { get; set; }
            public byte Direction { get; set; }
            public bool IsLocalPlayer { get; set; }
            public IntPtr NameAddress { get; set; }
            public IntPtr XAddress { get; set; }
            public IntPtr YAddress { get; set; }
            public IntPtr ZAddress { get; set; }
            public IntPtr DirectionAddress { get; set; }
            public string Status { get; set; }
        }

        private class CachedPlayerAddress
        {
            public IntPtr NameAddress { get; set; }
            public IntPtr XAddress { get; set; }
            public IntPtr YAddress { get; set; }
            public IntPtr ZAddress { get; set; }
            public IntPtr DirectionAddress { get; set; }
            public DateTime LastFound { get; set; }
            public string PlayerName { get; set; }
        }

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
            currentPlayers = new List<PlayerData>();
            addressCache = new Dictionary<string, CachedPlayerAddress>();
            lastFullScanTime = DateTime.MinValue;
        }

        private void InitializeComponent()
        {
            this.Size = new Size(800, 500);
            this.Text = "Cave Bot Example";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 40);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9);
            this.TopMost = true;


            // Title
            Label lblTitle = new Label();
            lblTitle.Location = new Point(20, 20);
            lblTitle.Size = new Size(500, 30);
            lblTitle.Text = "MEMORY READER WITH 5x5 GRID";
            lblTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(0, 150, 255);
            this.Controls.Add(lblTitle);

            // Process info
            lblProcessInfo = new Label();
            lblProcessInfo.Location = new Point(20, 60);
            lblProcessInfo.Size = new Size(400, 25);
            lblProcessInfo.Text = "Process: Not attached";
            lblProcessInfo.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            this.Controls.Add(lblProcessInfo);

            // Player name search
            Label lblPlayerName = new Label();
            lblPlayerName.Location = new Point(20, 100);
            lblPlayerName.Size = new Size(120, 20);
            lblPlayerName.Text = "Player Name:";
            lblPlayerName.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            this.Controls.Add(lblPlayerName);

            txtPlayerName = new TextBox();
            txtPlayerName.Location = new Point(140, 98);
            txtPlayerName.Size = new Size(200, 25);
            txtPlayerName.BackColor = Color.FromArgb(50, 50, 65);
            txtPlayerName.ForeColor = Color.White;
            txtPlayerName.BorderStyle = BorderStyle.FixedSingle;
            txtPlayerName.Text = "Player";
            this.Controls.Add(txtPlayerName);

            // Show Overlay checkbox
            chkShowOverlay = new CheckBox();
            chkShowOverlay.Location = new Point(350, 100);
            chkShowOverlay.Size = new Size(150, 25);
            chkShowOverlay.Text = "Show Grid Overlay";
            chkShowOverlay.ForeColor = Color.White;
            chkShowOverlay.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            chkShowOverlay.Checked = true;
            this.Controls.Add(chkShowOverlay);

            // Monitor buttons
            btnStartMonitor = new Button();
            btnStartMonitor.Location = new Point(20, 140);
            btnStartMonitor.Size = new Size(120, 35);
            btnStartMonitor.Text = "Start Monitor";
            btnStartMonitor.BackColor = Color.FromArgb(0, 120, 0);
            btnStartMonitor.ForeColor = Color.White;
            btnStartMonitor.FlatStyle = FlatStyle.Flat;
            btnStartMonitor.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnStartMonitor.Click += BtnStartMonitor_Click;
            this.Controls.Add(btnStartMonitor);

            btnStopMonitor = new Button();
            btnStopMonitor.Location = new Point(150, 140);
            btnStopMonitor.Size = new Size(120, 35);
            btnStopMonitor.Text = "Stop Monitor";
            btnStopMonitor.BackColor = Color.FromArgb(180, 0, 0);
            btnStopMonitor.ForeColor = Color.White;
            btnStopMonitor.FlatStyle = FlatStyle.Flat;
            btnStopMonitor.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnStopMonitor.Click += BtnStopMonitor_Click;
            btnStopMonitor.Enabled = false;
            this.Controls.Add(btnStopMonitor);

            // Reattach button
            btnReattachProcess = new Button();
            btnReattachProcess.Location = new Point(280, 140);
            btnReattachProcess.Size = new Size(120, 35);
            btnReattachProcess.Text = "Reattach";
            btnReattachProcess.BackColor = Color.FromArgb(0, 100, 200);
            btnReattachProcess.ForeColor = Color.White;
            btnReattachProcess.FlatStyle = FlatStyle.Flat;
            btnReattachProcess.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnReattachProcess.Click += BtnReattachProcess_Click;
            this.Controls.Add(btnReattachProcess);

            // Status
            lblStatus = new Label();
            lblStatus.Location = new Point(20, 190);
            lblStatus.Size = new Size(750, 50);
            lblStatus.Text = "Ready - Enter player name and click Start Monitor\nGrid overlay will show 5x5 tiles with player position";
            lblStatus.Font = new Font("Segoe UI", 10);
            this.Controls.Add(lblStatus);

            // Instructions
            Label lblInstructions = new Label();
            lblInstructions.Location = new Point(20, 260);
            lblInstructions.Size = new Size(750, 200);
            lblInstructions.Text = @"INSTRUCTIONS:

1. Make sure the game (otpdx.exe) is running
2. Enter the player name you want to monitor
3. Check 'Show Grid Overlay' to display position grid
4. Click 'Start Monitor' to begin tracking
5. The 5x5 grid will appear over the game window

GRID FEATURES:
• 5x5 tile grid showing player position
• Yellow center tile = current player position
• Gray tiles = surrounding positions
• Coordinates displayed on each tile
• Real-time position updates
• Other players shown if in range

TROUBLESHOOTING:
• If overlay doesn't appear, click 'Reattach'
• Run as Administrator if needed
• Make sure game window is visible";
            lblInstructions.Font = new Font("Consolas", 8);
            lblInstructions.ForeColor = Color.LightGray;
            this.Controls.Add(lblInstructions);

            // Initialize timers
            monitorTimer = new System.Timers.Timer(500);
            monitorTimer.Elapsed += MonitorTimer_Elapsed;
            monitorTimer.AutoReset = true;

            processCheckTimer = new System.Timers.Timer(500);
            processCheckTimer.Elapsed += ProcessCheckTimer_Elapsed;
            processCheckTimer.AutoReset = true;
            processCheckTimer.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AttachToProcess();
        }

        private bool AttachToProcess()
        {
            try
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    processHandle = IntPtr.Zero;
                }

                Process[] processes = Process.GetProcessesByName("otpdx");

                if (processes.Length > 0)
                {
                    targetProcess = processes[0];
                    processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (uint)targetProcess.Id);

                    if (processHandle != IntPtr.Zero)
                    {
                        gameWindowHandle = FindGameWindow();
                        lblProcessInfo.Text = $"Process: otpdx.exe (PID: {targetProcess.Id}) - Attached";
                        lblProcessInfo.ForeColor = Color.LightGreen;
                        lblStatus.Text = "Process attached successfully. Ready to monitor.";
                        return true;
                    }
                    else
                    {
                        lblProcessInfo.Text = "Process: otpdx.exe - Failed to open handle";
                        lblProcessInfo.ForeColor = Color.Red;
                        lblStatus.Text = "Failed to open process handle. Try running as Administrator!";
                        return false;
                    }
                }
                else
                {
                    lblProcessInfo.Text = "Process: otpdx.exe - Not found";
                    lblProcessInfo.ForeColor = Color.Red;
                    lblStatus.Text = "Process otpdx.exe is not running";
                    return false;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                return false;
            }
        }

        private IntPtr FindGameWindow()
        {
            if (targetProcess == null) return IntPtr.Zero;

            IntPtr[] windows = GetProcessWindows(targetProcess.Id);
            foreach (IntPtr window in windows)
            {
                if (IsWindowVisible(window))
                {
                    StringBuilder title = new StringBuilder(256);
                    GetWindowText(window, title, 256);
                    if (title.Length > 0)
                    {
                        return window;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private IntPtr[] GetProcessWindows(int processId)
        {
            List<IntPtr> windows = new List<IntPtr>();
            IntPtr hWnd = IntPtr.Zero;

            while (true)
            {
                hWnd = FindWindowEx(IntPtr.Zero, hWnd, null, null);
                if (hWnd == IntPtr.Zero) break;

                uint windowProcessId;
                GetWindowThreadProcessId(hWnd, out windowProcessId);

                if (windowProcessId == processId)
                {
                    windows.Add(hWnd);
                }
            }
            return windows.ToArray();
        }

        private void BtnReattachProcess_Click(object sender, EventArgs e)
        {
            try
            {
                bool wasMonitoring = monitorTimer.Enabled;
                if (wasMonitoring)
                {
                    monitorTimer.Stop();
                }

                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    processHandle = IntPtr.Zero;
                }

                addressCache.Clear();
                lastFullScanTime = DateTime.MinValue;

                if (AttachToProcess())
                {
                    lblStatus.Text = "Process reattached successfully! Cache cleared.";

                    if (wasMonitoring)
                    {
                        monitorTimer.Start();
                        lblStatus.Text += " Monitoring resumed.";
                        btnStartMonitor.Enabled = false;
                        btnStopMonitor.Enabled = true;

                        if (chkShowOverlay.Checked && overlayForm != null)
                        {
                            overlayForm.Close();
                            overlayForm = null;
                            CreateOverlay();
                        }
                    }
                }
                else
                {
                    lblStatus.Text = "Failed to reattach to process. Make sure the game is running!";
                    if (wasMonitoring)
                    {
                        btnStartMonitor.Enabled = true;
                        btnStopMonitor.Enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error during reattach: {ex.Message}";
            }
        }

        private void ProcessCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                ClearExpiredCache();

                if (processHandle != IntPtr.Zero && targetProcess != null)
                {
                    if (targetProcess.HasExited)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblProcessInfo.Text = "Process: otpdx.exe - Terminated";
                            lblProcessInfo.ForeColor = Color.Red;
                            lblStatus.Text = "Game process terminated. Click Reattach when game is restarted.";
                            processHandle = IntPtr.Zero;
                            targetProcess = null;

                            addressCache.Clear();
                            lastFullScanTime = DateTime.MinValue;

                            if (monitorTimer.Enabled)
                            {
                                StopMonitoring();
                            }
                        }));
                    }
                }
            }
            catch
            {
            }
        }

        private void ClearExpiredCache()
        {
            try
            {
                var expiredPlayers = new List<string>();
                var now = DateTime.Now;

                foreach (var cache in addressCache)
                {
                    if (now - cache.Value.LastFound > TimeSpan.FromMinutes(10))
                    {
                        expiredPlayers.Add(cache.Key);
                    }
                }

                foreach (var playerName in expiredPlayers)
                {
                    addressCache.Remove(playerName);
                }

                if (expiredPlayers.Count > 0)
                {
                    Debug.WriteLine($"Cache expirado removido: {expiredPlayers.Count} jogadores");
                }
            }
            catch
            {
            }
        }

        private void BtnStartMonitor_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtPlayerName.Text))
            {
                MessageBox.Show("Please enter a player name to monitor!", "Input Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (processHandle == IntPtr.Zero)
            {
                if (!AttachToProcess())
                {
                    MessageBox.Show("Cannot attach to process! Make sure the game is running.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            StartMonitoring();
        }

        private void StartMonitoring()
        {
            btnStartMonitor.Enabled = false;
            btnStopMonitor.Enabled = true;
            txtPlayerName.Enabled = false;
            chkShowOverlay.Enabled = false;

            addressCache.Clear();
            lastFullScanTime = DateTime.MinValue;

            if (chkShowOverlay.Checked)
            {
                CreateOverlay();
            }

            monitorTimer.Start();
            lblStatus.Text = "Monitoring started... Performing initial full scan...";

            Task.Run(() => SearchForPlayers());
        }

        private void CreateOverlay()
        {
            try
            {
                if (gameWindowHandle == IntPtr.Zero)
                {
                    gameWindowHandle = FindGameWindow();
                }

                if (gameWindowHandle != IntPtr.Zero)
                {
                    overlayForm = new OverlayForm();
                    overlayForm.SetGameWindow(gameWindowHandle);
                    overlayForm.Show();
                    overlayForm.UpdatePlayerData(currentPlayers);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error creating overlay: {ex.Message}";
            }
        }

        private void BtnStopMonitor_Click(object sender, EventArgs e)
        {
            StopMonitoring();
        }

        private void StopMonitoring()
        {
            monitorTimer.Stop();

            btnStartMonitor.Enabled = true;
            btnStopMonitor.Enabled = false;
            txtPlayerName.Enabled = true;
            chkShowOverlay.Enabled = true;

            if (overlayForm != null)
            {
                overlayForm.Close();
                overlayForm = null;
            }

            lblStatus.Text = "Monitoring stopped";
        }

        private void MonitorTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SearchForPlayers();
        }

        private void SearchForPlayers()
        {
            try
            {
                if (processHandle == IntPtr.Zero)
                    return;

                var newPlayers = new List<PlayerData>();
                string searchName = txtPlayerName.Text;

                bool shouldDoFullScan = ShouldDoFullScan();

                List<IntPtr> addressesToCheck;

                if (shouldDoFullScan)
                {
                    addressesToCheck = FindPlayerNameAddresses(searchName);
                    lastFullScanTime = DateTime.Now;
                    Debug.WriteLine($"FULL SCAN: Encontrados {addressesToCheck.Count} endereços para {searchName}");
                }
                else
                {
                    addressesToCheck = GetCachedAddresses(searchName);
                    Debug.WriteLine($"CACHE: Verificando {addressesToCheck.Count} endereços em cache para {searchName}");
                }

                foreach (var address in addressesToCheck)
                {
                    var playerData = ReadPlayerStructure(address);
                    if (playerData != null && playerData.Status == "Valid")
                    {
                        newPlayers.Add(playerData);

                        UpdateAddressCache(playerData);
                    }
                }

                this.Invoke(new Action(() =>
                {
                    currentPlayers = newPlayers;
                    UpdateStatus(shouldDoFullScan);

                    if (overlayForm != null && !overlayForm.IsDisposed)
                    {
                        overlayForm.UpdatePlayerData(currentPlayers);
                        overlayForm.RepositionOverlay();
                    }
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Error: {ex.Message}";
                }));
            }
        }

        private bool ShouldDoFullScan()
        {

            if (lastFullScanTime == DateTime.MinValue)
                return true;

            if (DateTime.Now - lastFullScanTime > cacheDuration)
                return true;

            if (!addressCache.ContainsKey(txtPlayerName.Text))
                return true;

            return false;
        }

        private List<IntPtr> GetCachedAddresses(string playerName)
        {
            var addresses = new List<IntPtr>();

            if (addressCache.ContainsKey(playerName))
            {
                var cache = addressCache[playerName];
                addresses.Add(cache.NameAddress);
            }

            return addresses;
        }

        private void UpdateAddressCache(PlayerData playerData)
        {
            string playerName = playerData.Name;

            if (!addressCache.ContainsKey(playerName))
            {
                addressCache[playerName] = new CachedPlayerAddress();
            }

            var cache = addressCache[playerName];
            cache.NameAddress = playerData.NameAddress;
            cache.XAddress = playerData.XAddress;
            cache.YAddress = playerData.YAddress;
            cache.ZAddress = playerData.ZAddress;
            cache.DirectionAddress = playerData.DirectionAddress;
            cache.LastFound = DateTime.Now;
            cache.PlayerName = playerName;
        }

        private void UpdateStatus(bool isFullScan)
        {
            string scanType = isFullScan ? "Full Scan" : "Fast Cache";
            string cacheInfo = addressCache.Count > 0 ? $"[Cache: {addressCache.Count}]" : "[No Cache]";

            if (currentPlayers.Count == 0)
            {
                lblStatus.Text = $"No players found: {txtPlayerName.Text} | {scanType} | {cacheInfo} | Searching...";
            }
            else
            {
                var localPlayers = currentPlayers.Count(p => p.IsLocalPlayer);
                lblStatus.Text = $"Found {currentPlayers.Count} players | Local: {localPlayers} | {scanType} | {cacheInfo} | Updated: {DateTime.Now:HH:mm:ss}";
            }
        }

        private List<IntPtr> FindPlayerNameAddresses(string playerName)
        {
            var addresses = new List<IntPtr>();

            try
            {
                if (processHandle == IntPtr.Zero)
                    return addresses;

                var searchPatterns = new[]
                {
                    Encoding.UTF8.GetBytes(playerName),
                    Encoding.ASCII.GetBytes(playerName),
                    Encoding.Unicode.GetBytes(playerName)
                };

                long[] searchRanges = {
                    0x00000000, 0x00FFFFFF,
                    0x01000000, 0x0FFFFFFF,
                    0x10000000, 0x1FFFFFFF,
                    0x40000000, 0x7FFFFFFF
                };

                for (int i = 0; i < searchRanges.Length; i += 2)
                {
                    long start = searchRanges[i];
                    long end = searchRanges[i + 1];
                    long chunkSize = 0x10000;

                    for (long addr = start; addr < end; addr += chunkSize)
                    {
                        try
                        {
                            byte[] buffer = new byte[chunkSize];
                            IntPtr bytesRead;

                            if (ReadProcessMemory(processHandle, new IntPtr(addr), buffer, (int)chunkSize, out bytesRead))
                            {
                                foreach (var pattern in searchPatterns)
                                {
                                    var matches = FindPattern(buffer, pattern);
                                    foreach (int offset in matches)
                                    {
                                        addresses.Add(new IntPtr(addr + offset));
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding addresses: {ex.Message}");
            }

            return addresses.Distinct().ToList();
        }

        private List<int> FindPattern(byte[] data, byte[] pattern)
        {
            var positions = new List<int>();
            if (pattern.Length == 0) return positions;

            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    positions.Add(i);
                }
            }

            return positions;
        }

        private PlayerData ReadPlayerStructure(IntPtr playerNameAddress)
        {
            var player = new PlayerData
            {
                NameAddress = playerNameAddress,
                Status = "Invalid"
            };

            try
            {
                if (processHandle == IntPtr.Zero)
                    return null;

                player.Name = ReadString(playerNameAddress, 20);
                if (string.IsNullOrEmpty(player.Name)) return null;

                player.XAddress = new IntPtr(playerNameAddress.ToInt64() - 0x10);
                player.YAddress = new IntPtr(playerNameAddress.ToInt64() - 0x0C);
                player.ZAddress = new IntPtr(playerNameAddress.ToInt64() - 0x08);
                player.DirectionAddress = new IntPtr(playerNameAddress.ToInt64() + 0x20);
                IntPtr localPlayerAddress = new IntPtr(playerNameAddress.ToInt64() - 0x2C);

                string localPlayerText = ReadString(localPlayerAddress, 20);
                player.IsLocalPlayer = localPlayerText?.Equals("LocalPlayer", StringComparison.OrdinalIgnoreCase) == true;

                player.X = ReadUInt16(player.XAddress);
                player.Y = ReadUInt16(player.YAddress);
                player.Z = ReadByte(player.ZAddress);
                player.Direction = ReadByte(player.DirectionAddress);

                bool isValid = IsValidPlayerData(player);
                player.Status = isValid ? "Valid" : "Invalid";

                return isValid ? player : null;
            }
            catch
            {
                return null;
            }
        }

        private bool IsValidPlayerData(PlayerData player)
        {
            return player.Z >= 0 && player.Z <= 15 &&
                   player.X != 0 && player.Y != 0 &&
                   player.Direction >= 0 && player.Direction <= 7;
        }

        private string ReadString(IntPtr address, int maxLength)
        {
            try
            {
                if (processHandle == IntPtr.Zero)
                    return null;

                byte[] buffer = new byte[maxLength];
                IntPtr bytesRead;

                if (ReadProcessMemory(processHandle, address, buffer, maxLength, out bytesRead))
                {
                    int length = 0;
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        if (buffer[i] == 0)
                        {
                            length = i;
                            break;
                        }
                    }

                    if (length > 0)
                    {
                        return Encoding.UTF8.GetString(buffer, 0, length);
                    }
                }
            }
            catch { }

            return null;
        }

        private ushort ReadUInt16(IntPtr address)
        {
            try
            {
                if (processHandle == IntPtr.Zero)
                    return 0;

                byte[] buffer = new byte[2];
                IntPtr bytesRead;

                if (ReadProcessMemory(processHandle, address, buffer, 2, out bytesRead) && bytesRead.ToInt32() == 2)
                {
                    return BitConverter.ToUInt16(buffer, 0);
                }
            }
            catch { }

            return 0;
        }

        private byte ReadByte(IntPtr address)
        {
            try
            {
                if (processHandle == IntPtr.Zero)
                    return 0;

                byte[] buffer = new byte[1];
                IntPtr bytesRead;

                if (ReadProcessMemory(processHandle, address, buffer, 1, out bytesRead) && bytesRead.ToInt32() == 1)
                {
                    return buffer[0];
                }
            }
            catch { }

            return 0;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            monitorTimer?.Stop();
            monitorTimer?.Dispose();
            processCheckTimer?.Stop();
            processCheckTimer?.Dispose();

            if (overlayForm != null)
            {
                overlayForm.Close();
            }

            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
            }
            base.OnFormClosed(e);
        }
    }

    public class OverlayForm : Form
    {
        private IntPtr gameWindowHandle;
        private List<Form1.PlayerData> playerData;
        private System.Windows.Forms.Timer updateTimer;
        private int gridTileSize = 32;
        private int gridSize = 5;
        private int minTileSize = 35;
        private int maxTileSize = 100;
        private float gridProportion = 0.5f;

        public OverlayForm()
        {
            InitializeOverlay();
        }

        private void InitializeOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            this.StartPosition = FormStartPosition.Manual;

            int extendedStyle = GetWindowLong(this.Handle, -20);
            SetWindowLong(this.Handle, -20, extendedStyle | 0x80000 | 0x20);

            this.Paint += OverlayForm_Paint;

            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 1000;
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            playerData = new List<Form1.PlayerData>();
        }

        public void SetGameWindow(IntPtr gameWindow)
        {
            gameWindowHandle = gameWindow;
            RepositionOverlay();
        }

        public void RepositionOverlay()
        {
            if (gameWindowHandle != IntPtr.Zero)
            {
                RECT gameRect;
                if (GetWindowRect(gameWindowHandle, out gameRect))
                {
                    int gameWidth = gameRect.Right - gameRect.Left;
                    int gameHeight = gameRect.Bottom - gameRect.Top;

                    CalculateOptimalTileSize(gameWidth, gameHeight);

                    int overlayWidth = gridSize * gridTileSize + 40;
                    int overlayHeight = gridSize * gridTileSize + 140;

                    if (overlayWidth > gameWidth * 0.8f)
                    {
                        overlayWidth = (int)(gameWidth * 0.8f);
                        gridTileSize = (overlayWidth - 40) / gridSize;
                    }

                    if (overlayHeight > gameHeight * 0.8f)
                    {
                        overlayHeight = (int)(gameHeight * 0.8f);
                        int newTileSize = (overlayHeight - 140) / gridSize;
                        gridTileSize = Math.Min(gridTileSize, newTileSize);
                    }

                    gridTileSize = Math.Max(minTileSize, Math.Min(maxTileSize, gridTileSize));

                    overlayWidth = gridSize * gridTileSize + 40;
                    overlayHeight = gridSize * gridTileSize + 140;

                    this.Size = new Size(overlayWidth, overlayHeight);

                    int overlayX = gameRect.Left + (gameWidth - overlayWidth) / 2;
                    int overlayY = gameRect.Top + (gameHeight - overlayHeight) / 2;

                    this.Location = new Point(overlayX, overlayY);

                    SetWindowPos(this.Handle, new IntPtr(-1), overlayX, overlayY, overlayWidth, overlayHeight, 0x0010);
                }
            }
        }

        private void CalculateOptimalTileSize(int gameWidth, int gameHeight)
        {
            int referenceSize = Math.Min(gameWidth, gameHeight);
            int idealGridSize = (int)(referenceSize * gridProportion);
            gridTileSize = idealGridSize / gridSize;
            gridTileSize = Math.Max(minTileSize, Math.Min(maxTileSize, gridTileSize));
        }

        public void UpdatePlayerData(List<Form1.PlayerData> data)
        {
            playerData = data ?? new List<Form1.PlayerData>();
            this.Invalidate();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            RepositionOverlay();
        }

        private void OverlayForm_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            if (playerData == null || playerData.Count == 0)
            {
                DrawInfoBox(g, "Searching for players...", Color.FromArgb(150, 50, 50, 70), Color.Yellow);
                return;
            }

            var localPlayer = playerData.FirstOrDefault(p => p.IsLocalPlayer);
            if (localPlayer == null) localPlayer = playerData.FirstOrDefault();

            if (localPlayer != null)
            {
                DrawPositionGrid(g, localPlayer);
            }
            else
            {
                DrawInfoBox(g, "No player data", Color.FromArgb(150, 50, 50, 70), Color.White);
            }
        }

        private void DrawPositionGrid(Graphics g, Form1.PlayerData centerPlayer)
        {
            int startX = 20;
            int startY = 40;

            int titleFontSize = Math.Max(6, Math.Min(12, gridTileSize / 4));  // ÷4
            int coordFontSize = Math.Max(5, Math.Min(10, gridTileSize / 5));  // ÷5
            int infoFontSize = Math.Max(6, Math.Min(9, gridTileSize / 5));    // ÷5

            using (Font titleFont = new Font("Consolas", titleFontSize, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.Yellow))
            {
                string title = $"Position Grid - {centerPlayer.Name}";
                g.DrawString(title, titleFont, titleBrush, startX, 10);
            }

            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    int gameX = centerPlayer.X + (col - 2);
                    int gameY = centerPlayer.Y + (row - 2);

                    Rectangle tileRect = new Rectangle(
                        startX + col * gridTileSize,
                        startY + row * gridTileSize,
                        gridTileSize - 2,
                        gridTileSize - 2
                    );

                    Color tileColor;
                    if (row == 2 && col == 2)
                    {
                        tileColor = Color.FromArgb(180, 255, 255, 0);
                    }
                    else
                    {
                        tileColor = Color.FromArgb(100, 100, 100, 100);
                    }

                    using (SolidBrush tileBrush = new SolidBrush(tileColor))
                    {
                        g.FillRectangle(tileBrush, tileRect);
                    }

                    Color borderColor = (row == 2 && col == 2) ? Color.Gold : Color.Gray;
                    using (Pen borderPen = new Pen(borderColor, 2))
                    {
                        g.DrawRectangle(borderPen, tileRect);
                    }

                    using (Font coordFont = new Font("Consolas", coordFontSize, FontStyle.Bold))
                    using (SolidBrush coordBrush = new SolidBrush(Color.White))
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                    {
                        string coordText = $"{gameX},{gameY}";
                        SizeF textSize = g.MeasureString(coordText, coordFont);

                        RectangleF textBg = new RectangleF(
                            tileRect.X + 2,
                            tileRect.Y + 2,
                            textSize.Width + 2,
                            textSize.Height
                        );
                        g.FillRectangle(bgBrush, textBg);
                        g.DrawString(coordText, coordFont, coordBrush, tileRect.X + 3, tileRect.Y + 3);
                    }

                    using (Font zFont = new Font("Consolas", coordFontSize - 1, FontStyle.Bold))
                    using (SolidBrush zBrush = new SolidBrush(Color.Cyan))
                    using (SolidBrush zBgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                    {
                        string zText = $"Z:{centerPlayer.Z}";
                        SizeF zSize = g.MeasureString(zText, zFont);

                        RectangleF zBg = new RectangleF(
                            tileRect.Right - zSize.Width - 4,
                            tileRect.Bottom - zSize.Height - 2,
                            zSize.Width + 2,
                            zSize.Height
                        );
                        g.FillRectangle(zBgBrush, zBg);
                        g.DrawString(zText, zFont, zBrush,
                                   tileRect.Right - zSize.Width - 3,
                                   tileRect.Bottom - zSize.Height - 1);
                    }
                }
            }

            int playerIndicatorSize = Math.Max(8, Math.Min(20, gridTileSize / 3));
            DrawPlayerIndicator(g, startX + 2 * gridTileSize + gridTileSize / 2,
                              startY + 2 * gridTileSize + gridTileSize / 2,
                              centerPlayer, playerIndicatorSize);

            foreach (var player in playerData.Where(p => !p.IsLocalPlayer))
            {
                DrawOtherPlayerOnGrid(g, player, centerPlayer, startX, startY);
            }

            int infoY = startY + gridSize * gridTileSize + 10;
            using (Font infoFont = new Font("Consolas", infoFontSize))
            using (SolidBrush infoBrush = new SolidBrush(Color.White))
            {
                g.DrawString($"Player: {centerPlayer.Name}", infoFont, infoBrush, startX, infoY);
                g.DrawString($"Position: X:{centerPlayer.X} Y:{centerPlayer.Y} Z:{centerPlayer.Z}",
                           infoFont, infoBrush, startX, infoY + 15);
                g.DrawString($"Direction: {GetDirectionName(centerPlayer.Direction)}",
                           infoFont, infoBrush, startX, infoY + 30);
                g.DrawString($"Updated: {DateTime.Now:HH:mm:ss}",
                           infoFont, infoBrush, startX, infoY + 45);
            }
        }

        private void DrawPlayerIndicator(Graphics g, float centerX, float centerY, Form1.PlayerData player, int size)
        {
            using (SolidBrush playerBrush = new SolidBrush(Color.Lime))
            {
                g.FillEllipse(playerBrush, centerX - size / 2, centerY - size / 2, size, size);
            }

            using (Pen borderPen = new Pen(Color.DarkGreen, 2))
            {
                g.DrawEllipse(borderPen, centerX - size / 2, centerY - size / 2, size, size);
            }

            if (gridTileSize >= 40)
            {
                DrawDirectionArrow(g, centerX, centerY, player.Direction, Color.White, size);
            }
        }

        private void DrawOtherPlayerOnGrid(Graphics g, Form1.PlayerData player, Form1.PlayerData centerPlayer, int gridStartX, int gridStartY)
        {
            int relativeX = player.X - centerPlayer.X;
            int relativeY = player.Y - centerPlayer.Y;

            if (relativeX < -2 || relativeX > 2 || relativeY < -2 || relativeY > 2)
                return;

            int gridX = relativeX + 2;
            int gridY = relativeY + 2;

            float screenX = gridStartX + gridX * gridTileSize + gridTileSize / 2;
            float screenY = gridStartY + gridY * gridTileSize + gridTileSize / 2;

            int otherPlayerSize = Math.Max(6, Math.Min(12, gridTileSize / 4));
            using (SolidBrush otherPlayerBrush = new SolidBrush(Color.Cyan))
            {
                g.FillEllipse(otherPlayerBrush, screenX - otherPlayerSize / 2, screenY - otherPlayerSize / 2,
                            otherPlayerSize, otherPlayerSize);
            }

            using (Pen otherBorderPen = new Pen(Color.Blue, 1))
            {
                g.DrawEllipse(otherBorderPen, screenX - otherPlayerSize / 2, screenY - otherPlayerSize / 2,
                            otherPlayerSize, otherPlayerSize);
            }

            if (gridTileSize >= 45)
            {
                DrawDirectionArrow(g, screenX, screenY, player.Direction, Color.White, otherPlayerSize);
            }

            if (gridTileSize >= 50)
            {
                using (Font nameFont = new Font("Arial", Math.Max(5, gridTileSize / 10), FontStyle.Bold))  // ÷10
                using (SolidBrush nameBrush = new SolidBrush(Color.White))
                using (SolidBrush nameBgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                {
                    SizeF nameSize = g.MeasureString(player.Name, nameFont);
                    RectangleF nameBg = new RectangleF(
                        screenX - nameSize.Width / 2 - 2,
                        screenY - otherPlayerSize / 2 - nameSize.Height - 2,
                        nameSize.Width + 4,
                        nameSize.Height + 2
                    );

                    g.FillRectangle(nameBgBrush, nameBg);
                    g.DrawString(player.Name, nameFont, nameBrush,
                               screenX - nameSize.Width / 2,
                               screenY - otherPlayerSize / 2 - nameSize.Height - 1);
                }
            }
        }

        private void DrawDirectionArrow(Graphics g, float centerX, float centerY, byte direction, Color color, int baseSize)
        {
            float angle;
            switch (direction)
            {
                case 0: angle = 270f; break;  // North - para cima
                case 1: angle = 0f; break;    // East - para a direita
                case 2: angle = 90f; break;   // South - para baixo
                case 3: angle = 180f; break;  // West - para a esquerda
                case 4: angle = 315f; break;  // Northeast - diagonal superior direita
                case 5: angle = 45f; break;   // Southeast - diagonal inferior direita
                case 6: angle = 135f; break;  // Southwest - diagonal inferior esquerda
                case 7: angle = 225f; break;  // Northwest - diagonal superior esquerda
                default: angle = 0f; break;
            }
            float radians = (float)(angle * Math.PI / 180.0);
            float arrowLength = baseSize * 1.5f;

            float endX = centerX + (float)(Math.Cos(radians) * arrowLength);
            float endY = centerY + (float)(Math.Sin(radians) * arrowLength);

            using (Pen arrowPen = new Pen(color, Math.Max(1, baseSize / 4)))
            {
                g.DrawLine(arrowPen, centerX, centerY, endX, endY);

                float headLength = baseSize * 0.8f;
                float headAngle = 0.5f;

                float head1X = endX - (float)(headLength * Math.Cos(radians - headAngle));
                float head1Y = endY - (float)(headLength * Math.Sin(radians - headAngle));
                float head2X = endX - (float)(headLength * Math.Cos(radians + headAngle));
                float head2Y = endY - (float)(headLength * Math.Sin(radians + headAngle));

                g.DrawLine(arrowPen, endX, endY, head1X, head1Y);
                g.DrawLine(arrowPen, endX, endY, head2X, head2Y);
            }
        }

        private string GetDirectionName(byte direction)
        {
            switch (direction)
            {
                case 0: return "North";
                case 1: return "East";
                case 2: return "South";
                case 3: return "West";
                case 4: return "NorthEast";
                case 5: return "SouthEast";
                case 6: return "SouthWest";
                case 7: return "NorthWest";
                default: return "Unknown";
            }
        }

        private void DrawInfoBox(Graphics g, string message, Color bgColor, Color textColor)
        {
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                Rectangle rect = new Rectangle(20, this.Height / 2 - 20, this.Width - 40, 40);
                g.FillRectangle(bgBrush, rect);

                SizeF textSize = g.MeasureString(message, font);
                float textX = (this.Width - textSize.Width) / 2;
                float textY = (this.Height - textSize.Height) / 2;

                g.DrawString(message, font, textBrush, textX, textY);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            base.OnFormClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}