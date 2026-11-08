using System;
using System.Linq;
using System.Reflection;
using CatGame.EditTests;
using NUnit.Framework;

internal static class RunNUnitMethods
{
    private static int Main()
    {
        var tests = typeof(IdleMathTests)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => Attribute.IsDefined(method, typeof(TestAttribute)))
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();
        var passed = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Invoke(new IdleMathTests(), null);
                Console.WriteLine("NUNIT-MONO-" + test.Name + ": PASS");
                passed++;
            }
            catch (TargetInvocationException exception)
            {
                var cause = exception.InnerException ?? exception;
                Console.WriteLine("NUNIT-MONO-" + test.Name + ": FAIL: " + cause);
            }
            catch (Exception exception)
            {
                Console.WriteLine("NUNIT-MONO-" + test.Name + ": FAIL: " + exception);
            }
        }

        Console.WriteLine("NUNIT-MONO: " + passed + "/" + tests.Length + " NUnit assertion methods passed; Unity Test Runner not used");
        return tests.Length > 0 && passed == tests.Length ? 0 : 1;
    }
}
