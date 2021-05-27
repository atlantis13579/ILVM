namespace CRBGame
{
    class HotfixTest
    {
        int member0;

        public override string ToString()
        {
            return "member0:" + member0;
        }

        public virtual int GetMember0()
        {
            return member0;
        }
        public static int Add(int a, int b)
        {
            var tmp = new HotfixTest();
            tmp.member0 = 5;
            tmp.member0 = a;
            a = tmp.GetMember0();
            return a + b;
        }

        public static int Subtract(int a, int b)
        {
            return a - b;
        }

        public static int Multiply(int a, int b)
        {
            return a * b;
        }

        public static int Benchmark()
        {
            int s = 0;
            for (int i = 0; i <= 10000000; ++i)
            {
                int j = (i % 25163);
                j = (j * j) % 25163;
                j = (j * 113) % 25163;
                s = (s * 13 + j) % 25163;
            }
            return s;
        }
    }
}

