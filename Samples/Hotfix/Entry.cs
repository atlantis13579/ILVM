using System;

namespace Game.Hotfix
{
    public static class Entry
    {
        public static int Add(int a, int b) { return a + b; }
        public static int Version()
        {
#if HOTFIX_V2
            return 2;
#else
            return 1;
#endif
        }
        public static float Scale(float value, float factor) { return value * factor; }
        public static long Add64(long a, long b) { return a + b; }
        public static double Divide(double a, double b) { return a / b; }
        public static string Greet(string name) { return "Hello " + name; }
        public static bool IsNull(object value) { return value == null; }
        public static int Sum(int[] values)
        {
            int sum = 0;
            foreach (int value in values) sum += value;
            return sum;
        }
        public static int Factorial(int value) { return value < 2 ? 1 : value * Factorial(value - 1); }
        public static int Overload(int value) { return value + 1; }
        public static string Overload(string value) { return value + "!"; }
        public static void Throw() { throw new InvalidOperationException("Expected hotfix failure."); }
        public static Func<int, int> MakeClosure(int value) { return x => x + value; }
    }
}
