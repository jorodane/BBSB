using System;
using System.Reflection;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BBSB.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class TestAttribute : Attribute { }

    internal static class Program
    {
        private static int Main(string[] filters)
        {
            int passed = 0, failed = 0;
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            foreach (var method in type.GetMethods())
            {
                if (method.GetCustomAttribute<TestAttribute>() == null) continue;
                if (filters.Length > 0 && !Array.Exists(filters, filter =>
                    (type.Name + "." + method.Name).Contains(filter, StringComparison.OrdinalIgnoreCase))) continue;
                if (Environment.GetEnvironmentVariable("BBSB_CHECK_TRACE") == "1") Console.WriteLine("RUN " + type.Name + "." + method.Name);
                try { method.Invoke(Activator.CreateInstance(type), null); passed++; Console.WriteLine("PASS " + type.Name + "." + method.Name); }
                catch (Exception error)
                { failed++; Console.WriteLine("FAIL " + method.Name + ": " + error.GetBaseException().Message); }
            }
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Assets", "BBSB"))) root = root.Parent;
            if (root == null) { failed++; Console.WriteLine("FAIL Unity syntax scan: repository root not found."); }
            else
            {
                int files = 0, errors = 0;
                var options = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: new[] { "UNITY_EDITOR", "ENABLE_INPUT_SYSTEM" });
                foreach (var path in Directory.EnumerateFiles(Path.Combine(root.FullName, "Assets", "BBSB"), "*.cs", SearchOption.AllDirectories))
                {
                    files++;
                    foreach (var diagnostic in CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path).GetDiagnostics())
                        if (diagnostic.Severity == DiagnosticSeverity.Error) { errors++; Console.WriteLine(diagnostic); }
                }
                if (errors > 0 || files == 0) failed++; else passed++;
                Console.WriteLine("Unity source syntax: " + files + " files, " + errors + " errors (not a Unity API compilation).");
            }
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
