namespace WinBounce;

public static class PhysicsUtils
{
    public static double Lerp(double start, double end, double percentage)
    {
        return start + (end - start) * percentage;
    }
}