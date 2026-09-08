using System;
using System.Reflection;

namespace BBSB.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class TestAttribute : Attribute { }

    internal static class Program
    {
        private static int Main()
        {
            int passed = 0, failed = 0;
            var tests = new RunSessionTests();
            foreach (var method in typeof(RunSessionTests).GetMethods())
            {
                if (method.GetCustomAttribute<TestAttribute>() == null) continue;
                try { method.Invoke(tests, null); passed++; Console.WriteLine("PASS " + method.Name); }
                catch (Exception error)
                { failed++; Console.WriteLine("FAIL " + method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
