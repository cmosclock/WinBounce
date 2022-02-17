using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Vanara.PInvoke;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;


namespace WinBounce
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PhysicsWorld _physicsWorld = new (ScreenWidth, ScreenHeight);
        private bool _shown;
        NotifyIcon _notifyIcon = new ();
        private HashSet<IntPtr> _movingHwndSet = new ();
        private List<Process> _processes = new ();
        public static double ScreenHeight => SystemParameters.MaximizedPrimaryScreenHeight;
        public static double ScreenWidth => SystemParameters.MaximizedPrimaryScreenWidth;

        public MainWindow()
        {
            InitializeComponent();
            Hide();
            // https://stackoverflow.com/questions/69102624/executionengineexception-on-lowlevel-keyboard-hook
            _targetMovedProc = TargetMoved;
            _notifyIcon.Icon = new Icon(@"D:\SteamLibrary\steamapps\common\Hyperdimension Neptunia Re;Birth3\DeluxeSet\PC THEME\ICONS\ピーシェ_ネットワーク.ico");
            _notifyIcon.Visible = true;
            _notifyIcon.ShowBalloonTip(5000, "WinBounce.", "WinBounce is running in the background",  System.Windows.Forms.ToolTipIcon.Info);
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            var quitBtn = new ToolStripMenuItem("Exit?");
            _notifyIcon.ContextMenuStrip.Items.Add(quitBtn);
            quitBtn.Click += (sender, args) =>
            {
                _notifyIcon.Visible = false;
                Application.Current.Shutdown();
            };
            Simulate();
            Dispatcher.UnhandledException += Cleanup;
            AppDomain.CurrentDomain.UnhandledException += Cleanup;
            AppDomain.CurrentDomain.ProcessExit += Cleanup;
        }
        
        private static User32.WinEventProc _targetMovedProc;
        private List<IntPtr> _hWndList = new ();

        public void TargetMoved(User32.HWINEVENTHOOK hwineventhook, uint eventType, HWND hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            var hwndPtr = hwnd.DangerousGetHandle();
            Dispatcher.Invoke(() =>
            {
                switch (eventType)
                {
                    case User32.EventConstants.EVENT_SYSTEM_MOVESIZESTART:
                    {
                        _movingHwndSet.Add(hwndPtr);
                        _physicsWorld.UpdateEntityVelocity($"{hwndPtr}", 0);
                        _physicsWorld.UpdateEntityHeld($"{hwndPtr}", true);
                        break;
                    }
                    case User32.EventConstants.EVENT_SYSTEM_MOVESIZEEND:
                    {
                        User32.GetWindowRect(hwndPtr, out var rect);
                        _physicsWorld.UpdateEntityCoord($"{hwndPtr}", rect.left, ScreenHeight - rect.top, rect.Width, rect.Height);
                        _physicsWorld.UpdateEntityHeld($"{hwndPtr}", false);
                        _movingHwndSet.Remove(hwndPtr);
                        break;
                    }
                    case User32.EventConstants.EVENT_OBJECT_LOCATIONCHANGE:
                    {
                        if (_movingHwndSet.Contains(hwndPtr))
                        {
                            User32.GetWindowRect(hwndPtr, out var rect);
                            _physicsWorld.UpdateEntityCoord($"{hwndPtr}", rect.left, ScreenHeight - rect.top, rect.Width, rect.Height);
                        }
                        break;
                    }
                }
            });
        }

        public void Cleanup(object? self, EventArgs? _)
        {
            _notifyIcon.Visible = false;
            foreach (var hWnd in _hWndList)
            {
                User32.SetWindowPos(hWnd, HWND.HWND_NOTOPMOST, 0, 0, 0, 0,
                    User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
            }
            foreach (var proc in _processes)
            {
                proc.Kill();
            }
        }
        
        public void Simulate()
        {
            // add a flag for this
            // foreach (var _ in Enumerable.Repeat(0, 20))
            // {
            //     var proc = Process.Start("notepad.exe");
            //     _processes.Add(proc);
            //     proc.WaitForInputIdle();
            //     hWndList.Add(proc.MainWindowHandle);
            // }

            var processTimer = new Timer(1000);
            processTimer.Elapsed += (_, _) => Dispatcher.Invoke(CheckProcesses);
            processTimer.Start();
            
            var tickTimer = new Timer(8);
            tickTimer.Elapsed += (_, _) => Dispatcher.Invoke(UpdateWorld);
            tickTimer.Start();
            
            var renderTimer = new Timer(16);
            renderTimer.Elapsed += (_, _) => Dispatcher.Invoke(RenderWorld);
            renderTimer.Start();
        }

        public void CheckProcesses()
        {
            var processes = Process.GetProcessesByName("notepad").ToList();
            var newHwndList = processes
                .Select(p => p.MainWindowHandle)
                .Where(ptr => !User32.IsIconic(ptr))
                .ToList();
            foreach (var hWnd in newHwndList.Except(_hWndList).ToList())
            {
                User32.GetWindowRect(hWnd, out var rect);
                _physicsWorld.AddEntity($"{hWnd}", rect.left, ScreenHeight - rect.top, rect.Width, rect.Height);
                User32.SetWindowPos(hWnd, HWND.HWND_TOPMOST, 0, 0, 0, 0,
                    User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                var targetThreadId = User32.GetWindowThreadProcessId(hWnd, out var pId);
                User32.SetWinEventHook(
                    User32.EventConstants.EVENT_SYSTEM_MOVESIZESTART,
                    User32.EventConstants.EVENT_OBJECT_LOCATIONCHANGE,
                    IntPtr.Zero,
                    _targetMovedProc,
                    pId,
                    targetThreadId,
                    User32.WINEVENT.WINEVENT_OUTOFCONTEXT | User32.WINEVENT.WINEVENT_SKIPOWNPROCESS);
                _hWndList.Add(hWnd);
            }
            foreach (var hWndToRemove in _hWndList.Except(newHwndList).ToList())
            {
                _physicsWorld.RemoveEntity($"{hWndToRemove}");
                _hWndList.Remove(hWndToRemove);
            }
        }

        public void UpdateWorld()
        {
            _physicsWorld.Update();
        }

        public void RenderWorld()
        {
            foreach (var freeHwnd in _hWndList.Except(_movingHwndSet))
            {
                if (_physicsWorld.GetEntityCoordTranslated($"{freeHwnd}") is not { } coord) continue;
                var (x, y, width, height) = coord;
                User32.GetWindowRect(freeHwnd, out var rect);
                x = PhysicsUtils.Lerp(rect.X, x, 0.5);
                y = PhysicsUtils.Lerp(rect.Y, y, 0.5);
                User32.MoveWindow(freeHwnd, (int)x, (int)y, (int)width, (int)height, false);
            }
        }
    }
}