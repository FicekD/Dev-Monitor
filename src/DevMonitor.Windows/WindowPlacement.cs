using System.Runtime.InteropServices;

namespace DevMonitor.Windows;

public static class WindowPlacement
{
    [StructLayout(LayoutKind.Sequential)]
    struct Point
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Placement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);

    public static (int X, int Y, int Width, int Height)? NormalBounds(IntPtr window)
    {
        var placement = new Placement { Length = Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(window, ref placement))
            return null;
        var bounds = placement.NormalPosition;
        return (bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
    }
}
