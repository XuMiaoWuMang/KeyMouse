using System.Runtime.InteropServices;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 录制期间的悬浮停止按钮：**原生 Win32 分层窗口**（不是 WinUI 窗口）。
///
/// 为什么不用 WinUI：那个窗口的尺寸被 presenter 反复改写（实测出现过 202x66、184x57、大白块），
/// 也没法做逐像素透明；而原生分层窗口的尺寸、形状、点击命中全由这里决定，没得改。
///
/// 做法：
/// * WS_POPUP + WS_EX_LAYERED/TOPMOST/TOOLWINDOW/NOACTIVATE，不抢焦点；
/// * LWA_COLORKEY：把洋红当透明色 → 画一个红色椭圆，四角自然透明，得到真正的圆；
/// * 整块窗口都能点、也能拖；拖动超过 4px 不算点击。
/// 窗口跑在自己的线程上，有自己的消息泵，不碰 WinUI 的调度队列。
/// </summary>
internal sealed class NativeIsland : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;

    private const int LwaColorKey = 0x00000001;
    private const int SwpNoSize = 0x0001;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoActivate = 0x0010;

    private const int WmDestroy = 0x0002;
    private const int WmPaint = 0x000F;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmMouseMove = 0x0200;
    private const int WmNchitTest = 0x0084;
    private const int HtCaption = 2;

    private const int SpiGetWorkArea = 0x0030;

    private static readonly IntPtr KeyColor = (IntPtr)0x00FF00FF;   // COLORREF 洋红 = 透明
    private static NativeIsland? _current;
    private static readonly IntPtr ClassName = Marshal.StringToHGlobalUni("KeyMouseIsland");

    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private IntPtr _hwnd;
    private Action? _onStop;

    private int _width, _height;
    private bool _dragging;
    private POINT _grab;
    private RECT _origin;
    private bool _fired;

    /// <summary>窗口句柄（录制器用它忽略这个窗口上的鼠标事件）。</summary>
    internal IntPtr Handle => _hwnd;

    /// <summary>窗口是否已经建好（只有第一次 Start 会真的创建）。</summary>
    internal bool IsCreated => _hwnd != IntPtr.Zero;

    /// <summary>显示/隐藏已经建好的窗口。**只隐藏、不销毁**：第二次创建原生窗口会硬崩（实测）。</summary>
    internal void SetVisible(bool visible)
    {
        Trace("SetVisible(" + visible + ")");
        if (_hwnd == IntPtr.Zero) return;
        _fired = !visible;
        ShowWindow(_hwnd, visible ? 4 /*SW_SHOWNOACTIVATE*/ : 0 /*SW_HIDE*/);
        if (visible) UpdateWindow(_hwnd);
    }

    /// <summary>创建并显示，位置贴主屏工作区右侧、垂直居中。</summary>
    internal void Start(Action onStop)
    {
        _onStop = onStop;
        _current = this;

        _thread = new Thread(Run) { IsBackground = true, Name = "keymouse-island" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(3000);
    }

    private static readonly object LogGate = new();

    /// <summary>原生窗口没有托管栈可看：每一步写一行，崩溃时就知道停在哪。</summary>
    private static void Trace(string line)
    {
        try
        {
            string path = Scratch.PathFor("keymouse-island.log");
            lock (LogGate)
            {
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
        }
    }

    private void Run()
    {
        Trace("thread start, class=" + (_classRegistered ? "已注册" : "待注册"));
        EnsureClass();
        Trace("class ok");

        int dpi = GetDpiForSystem();
        double scale = dpi <= 0 ? 1.0 : dpi / 96.0;
        _width = (int)Math.Round(44 * scale);
        _height = _width;

        GetWorkArea(out RECT work);
        int x = work.Right - _width;              // 贴最右侧
        int y = work.Top + (work.Bottom - work.Top - _height) / 2;

        _hwnd = CreateWindowEx(
            WsExLayered | WsExToolWindow | WsExTopmost | WsExNoActivate,
            ClassName, "KeyMouse 录制中", WsPopup,
            x, y, _width, _height, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

        Trace("CreateWindowEx -> " + _hwnd.ToInt64().ToString("X"));
        if (_hwnd != IntPtr.Zero)
        {
            SetLayeredWindowAttributes(_hwnd, KeyColor, 0, LwaColorKey);
            // 建好但**先不显示**：只有录制真的开始了才由调用方 SetVisible(true)。
            // 之前这里直接显示，于是"录制没起来"时屏幕上只剩一个球，用户以为在录。
        }

        _ready.Set();

        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    /// <summary>
    /// 窗口类只注册一次，而且窗口过程必须是**静态**委托：注册过的类里存的是裸函数指针，
    /// 用实例方法的委托时，第一次录制结束、实例被回收之后，第二次创建窗口就跳进已释放内存——
    /// 进程直接消失，崩溃日志里一个字都没有（实测：录制一次后再点录制，编辑器退出）。
    /// </summary>
    private static readonly WndProc Proc = Dispatch;
    private static bool _classRegistered;

    private static void EnsureClass()
    {
        lock (typeof(NativeIsland))
        {
            if (_classRegistered) return;
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Proc,
                hInstance = GetModuleHandle(null),
                lpszClassName = ClassName,
                hCursor = LoadCursor(IntPtr.Zero, (IntPtr)32512),   // IDC_ARROW
            };
            RegisterClassEx(ref wc);
            _classRegistered = true;
        }
    }

    private static IntPtr Dispatch(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam) =>
        _current is { } island ? island.WindowProc(hwnd, msg, wParam, lParam) : DefWindowProc(hwnd, msg, wParam, lParam);

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WmPaint:
                Paint(hwnd);
                return IntPtr.Zero;

            case WmLButtonDown:
                // 自己处理拖动：交给系统的 HTCAPTION 拖动循环后，抬起事件会被它吃掉，点一下就停不了。
                GetCursorPos(out _grab);
                GetWindowRect(hwnd, out _origin);
                _dragging = true;
                SetCapture(hwnd);
                return IntPtr.Zero;

            case WmMouseMove:
                if (_dragging)
                {
                    GetCursorPos(out POINT now);
                    SetWindowPos(hwnd, IntPtr.Zero,
                        _origin.Left + (now.X - _grab.X), _origin.Top + (now.Y - _grab.Y),
                        0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
                }
                return IntPtr.Zero;

            case WmLButtonUp:
                Trace("island lbutton up");
                if (_dragging)
                {
                    _dragging = false;
                    ReleaseCapture();
                    // 位移超过 4px 算拖动，不算点击。
                    GetCursorPos(out POINT up);
                    if (Math.Abs(up.X - _grab.X) + Math.Abs(up.Y - _grab.Y) > 4) return IntPtr.Zero;
                }
                if (!_fired)
                {
                    _fired = true;
                    ShowWindow(hwnd, 0 /*SW_HIDE*/);
                    _onStop?.Invoke();
                }
                return IntPtr.Zero;

            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void Paint(IntPtr hwnd)
    {
        if (!GetClientRect(hwnd, out RECT client)) return;
        IntPtr dc = BeginPaint(hwnd, out PAINTSTRUCT ps);

        // 底色 = 透明色，然后画红圆 + 白色方块。
        IntPtr back = CreateSolidBrush(0x00FF00FF);
        FillRect(dc, ref client, back);
        DeleteObject(back);

        IntPtr red = CreateSolidBrush(0x00363BE2);   // COLORREF = BGR（= #E23B36）
        IntPtr oldBrush = SelectObject(dc, red);
        IntPtr oldPen = SelectObject(dc, GetStockObject(8 /*NULL_PEN*/));
        Ellipse(dc, 0, 0, client.Right, client.Bottom);
        SelectObject(dc, oldBrush);
        SelectObject(dc, oldPen);
        DeleteObject(red);

        int side = (int)Math.Round(client.Right * 0.32);
        int left = (client.Right - side) / 2;
        int top = (client.Bottom - side) / 2;
        IntPtr white = CreateSolidBrush(0x00FFFFFF);
        oldBrush = SelectObject(dc, white);
        Rectangle(dc, left, top, left + side, top + side);
        SelectObject(dc, oldBrush);
        DeleteObject(white);

        EndPaint(hwnd, ref ps);
    }

    public void Dispose()
    {
        Trace("dispose");
        _fired = true;
        if (_hwnd != IntPtr.Zero) PostMessage(_hwnd, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);

        // 等窗口线程真的退出再摘引用：窗口过程要靠 _current 路由，提前置空会让正在派发的消息乱走。
        try { _thread?.Join(2000); } catch (Exception) { }
        if (_current == this) _current = null;
        _hwnd = IntPtr.Zero;
        _thread = null;
    }

    // ------------------------------------------------------------------ interop

    private delegate IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam; public IntPtr lParam; public int time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT { public IntPtr hdc; public int fErase; public RECT rcPaint; public int fRestore; public int fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int exStyle, IntPtr className, string title, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hwnd, int min, int max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, IntPtr key, byte alpha, int flags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, int flags);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern int GetDpiForSystem();
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint param, ref RECT rect, uint winIni);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(int color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int index);
    [DllImport("gdi32.dll")] private static extern bool Ellipse(IntPtr dc, int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool Rectangle(IntPtr dc, int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr dc, ref RECT rect, IntPtr brush);

    private static void GetWorkArea(out RECT rect)
    {
        rect = default;
        SystemParametersInfo(SpiGetWorkArea, 0, ref rect, 0);
    }
}
