using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool createdNew;
        using (new Mutex(true, "Local\\UnofficialSingBoxTray", out createdNew))
        {
            if (!createdNew)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext(TrayOptions.FromArgs(args)));
        }
    }
}

internal sealed class TrayOptions
{
    public string WorkDir;
    public string SingBoxExe;
    public string ConfigFile;
    public string LogFile;
    public string IconFile;

    public static TrayOptions FromArgs(string[] args)
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string workDir = GetArg(args, "--workdir");
        if (String.IsNullOrEmpty(workDir))
        {
            workDir = exeDir;
        }

        string singBoxExe = GetArg(args, "--sing-box");
        if (String.IsNullOrEmpty(singBoxExe))
        {
            singBoxExe = Path.Combine(workDir, "sing-box.exe");
        }

        string configFile = GetArg(args, "--config");
        if (String.IsNullOrEmpty(configFile))
        {
            configFile = Path.Combine(workDir, "config.json");
        }

        string logFile = GetArg(args, "--log");
        if (String.IsNullOrEmpty(logFile))
        {
            logFile = Path.Combine(workDir, "logs", "sing-box.log");
        }

        string iconFile = GetArg(args, "--icon");
        if (String.IsNullOrEmpty(iconFile))
        {
            string workDirIcon = Path.Combine(workDir, "sing-box.ico");
            string exeDirIcon = Path.Combine(exeDir, "sing-box.ico");
            iconFile = File.Exists(workDirIcon) ? workDirIcon : exeDirIcon;
        }

        return new TrayOptions
        {
            WorkDir = Path.GetFullPath(workDir),
            SingBoxExe = Path.GetFullPath(singBoxExe),
            ConfigFile = Path.GetFullPath(configFile),
            LogFile = Path.GetFullPath(logFile),
            IconFile = Path.GetFullPath(iconFile)
        };
    }

    private static string GetArg(string[] args, string name)
    {
        if (args == null)
        {
            return null;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i].Substring(name.Length + 1).Trim('"');
            }
        }

        return null;
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly TrayOptions options;
    private readonly object logLock = new object();
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem startItem;
    private readonly ToolStripMenuItem stopItem;
    private readonly ToolStripMenuItem restartItem;
    private readonly System.Windows.Forms.Timer timer;

    private Process singBoxProcess;
    private bool desiredRunning = true;
    private DateTime lastStartAttempt = DateTime.MinValue;

    public TrayContext(TrayOptions trayOptions)
    {
        options = trayOptions;
        Directory.CreateDirectory(Path.GetDirectoryName(options.LogFile));

        statusItem = new ToolStripMenuItem("Status: starting") { Enabled = false };
        startItem = new ToolStripMenuItem("Start", null, delegate { desiredRunning = true; StartSingBox(); UpdateStatus(); });
        stopItem = new ToolStripMenuItem("Stop", null, delegate { desiredRunning = false; StopSingBox(); UpdateStatus(); });
        restartItem = new ToolStripMenuItem("Restart", null, delegate { desiredRunning = true; RestartSingBox(); UpdateStatus(); });

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(restartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Open log", null, delegate { OpenLog(); }));
        menu.Items.Add(new ToolStripMenuItem("Open folder", null, delegate { OpenFolder(); }));
        menu.Items.Add(new ToolStripMenuItem("Edit config", null, delegate { OpenConfig(); }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, delegate { ExitTray(); }));

        trayIcon = new NotifyIcon();
        trayIcon.Icon = LoadTrayIcon();
        trayIcon.Text = "sing-box: starting";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += delegate { OpenLog(); };

        timer = new System.Windows.Forms.Timer();
        timer.Interval = 2000;
        timer.Tick += delegate
        {
            if (desiredRunning && !IsSingBoxRunning() && DateTime.Now - lastStartAttempt > TimeSpan.FromSeconds(5))
            {
                StartSingBox();
            }

            UpdateStatus();
        };
        timer.Start();

        WriteLog("Tray manager started.");
        WriteLog("Work dir: " + options.WorkDir);
        StartSingBox();
        UpdateStatus();
    }

    private bool IsSingBoxRunning()
    {
        try
        {
            return singBoxProcess != null && !singBoxProcess.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private void StartSingBox()
    {
        if (IsSingBoxRunning())
        {
            return;
        }

        lastStartAttempt = DateTime.Now;

        if (!File.Exists(options.SingBoxExe))
        {
            WriteLog("ERROR: sing-box.exe not found: " + options.SingBoxExe);
            return;
        }

        if (!File.Exists(options.ConfigFile))
        {
            WriteLog("ERROR: config file not found: " + options.ConfigFile);
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = options.SingBoxExe;
            startInfo.Arguments = "run -c " + Quote(options.ConfigFile);
            startInfo.WorkingDirectory = options.WorkDir;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            Process process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    WriteLog(e.Data);
                }
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    WriteLog(e.Data);
                }
            };
            process.Exited += delegate
            {
                int exitCode = -1;
                try { exitCode = process.ExitCode; } catch { }
                WriteLog("sing-box exited. Exit code: " + exitCode);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            singBoxProcess = process;
            WriteLog("sing-box started. PID: " + process.Id);
        }
        catch (Exception ex)
        {
            WriteLog("ERROR: failed to start sing-box. " + ex);
        }
    }

    private void StopSingBox()
    {
        Process process = singBoxProcess;
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                WriteLog("Stopping sing-box. PID: " + process.Id);
                process.Kill();
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            WriteLog("ERROR: failed to stop sing-box. " + ex);
        }
        finally
        {
            singBoxProcess = null;
        }
    }

    private void RestartSingBox()
    {
        StopSingBox();
        Thread.Sleep(500);
        StartSingBox();
    }

    private void UpdateStatus()
    {
        bool running = IsSingBoxRunning();
        string status = running ? "running" : (desiredRunning ? "starting" : "stopped");
        statusItem.Text = "Status: " + status;
        trayIcon.Text = "sing-box: " + status;
        startItem.Enabled = !running;
        stopItem.Enabled = running;
        restartItem.Enabled = running;
    }

    private void OpenLog()
    {
        try
        {
            if (!File.Exists(options.LogFile))
            {
                File.WriteAllText(options.LogFile, "");
            }

            Process.Start(new ProcessStartInfo(options.LogFile) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WriteLog("ERROR: failed to open log. " + ex);
        }
    }

    private void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(options.WorkDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WriteLog("ERROR: failed to open folder. " + ex);
        }
    }

    private void OpenConfig()
    {
        try
        {
            Process.Start(new ProcessStartInfo(options.ConfigFile) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WriteLog("ERROR: failed to open config. " + ex);
        }
    }

    private void ExitTray()
    {
        desiredRunning = false;
        timer.Stop();
        StopSingBox();
        WriteLog("Tray manager exited.");
        trayIcon.Visible = false;
        trayIcon.Dispose();
        ExitThread();
    }

    private void WriteLog(string message)
    {
        try
        {
            lock (logLock)
            {
                File.AppendAllText(options.LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private Icon LoadTrayIcon()
    {
        if (File.Exists(options.IconFile))
        {
            return new Icon(options.IconFile);
        }

        Icon executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        return executableIcon ?? SystemIcons.Application;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
