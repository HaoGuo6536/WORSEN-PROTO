// Standalone evidence harness adapted from PLAN-010/verification/ManagedTestRunner.cs.
// Executes explicitly reviewed managed fixtures; it never starts or contacts Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public static class ManagedPureRunner
{
    private static readonly string[] Fixtures = {
        "Worsen.Tests.Level.LevelGraphUtilityTests",
        "Worsen.Tests.Level.LevelControllerTests",
        "Worsen.Tests.Player.PlayerMoverPresenterTests",
        "Worsen.Tests.Run.RunSessionControllerTests",
        "Worsen.Tests.SceneFlow.SceneFlowControllerTests"
    };

    public static int Main()
    {
        string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) => {
            string path = Path.Combine(root, new AssemblyName(eventArgs.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Assembly tests = Assembly.LoadFrom(Path.Combine(root, "Worsen.Tests.dll"));
        int passed = 0, failed = 0, environmentFailures = 0, excludedEngineCases = 0;
        foreach (string name in Fixtures)
        {
            Type type = tests.GetType(name, true);
            var setup = type.GetMethods().Where(m => Has(m, "SetUpAttribute")).ToArray();
            var cleanup = type.GetMethods().Where(m => Has(m, "TearDownAttribute")).ToArray();
            foreach (var method in type.GetMethods())
            {
                if (Has(method, "TestCaseSourceAttribute") || Has(method, "OneTimeSetUpAttribute") || Has(method, "OneTimeTearDownAttribute"))
                    throw new InvalidOperationException("Unsupported fixture lifecycle/case source: " + name + "." + method.Name);
                var cases = method.GetCustomAttributes(false).Where(a => a.GetType().Name == "TestCaseAttribute").ToArray();
                bool single = Has(method, "TestAttribute");
                if (!single && cases.Length == 0) continue;
                if (name == "Worsen.Tests.Run.RunSessionControllerTests" && method.Name == "ManagerKeepsFactoryRandomSourceAcrossReadinessAndResetsAtNextAssembly")
                {
                    Console.WriteLine("NOT_RUN_ENGINE " + name + "." + method.Name + " requires GameObject and Manager lifecycle in Unity.");
                    excludedEngineCases++;
                    continue;
                }
                if (Has(method, "IgnoreAttribute") || Has(method, "ExplicitAttribute"))
                    throw new InvalidOperationException("Selected case is not admitted: " + name + "." + method.Name);
                foreach (var data in cases.Length > 0 ? cases : new object[] { null })
                {
                    object[] parameters = data == null ? Array.Empty<object>() : (object[])data.GetType().GetProperty("Arguments").GetValue(data);
                    string label = name + "." + method.Name + "(" + string.Join(",", parameters.Select(p => p == null ? "null" : p.ToString())) + ")";
                    var errors = new List<Exception>();
                    object instance = null;
                    try
                    {
                        instance = Activator.CreateInstance(type);
                        foreach (var before in setup) before.Invoke(instance, null);
                        method.Invoke(instance, parameters);
                    }
                    catch (Exception error) { errors.Add(Unwrap(error)); }
                    finally
                    {
                        if (instance != null)
                            foreach (var after in cleanup)
                                try { after.Invoke(instance, null); }
                                catch (Exception error) { errors.Add(Unwrap(error)); }
                    }
                    if (errors.Count == 0) { passed++; Console.WriteLine("PASS " + label); }
                    else
                    {
                        bool environment = errors.Any(IsEnvironmentFailure);
                        if (environment) environmentFailures++; else failed++;
                        Console.WriteLine((environment ? "ENVIRONMENT_FAILURE " : "FAIL ") + label);
                        foreach (Exception error in errors) Console.WriteLine(error.GetType().FullName + ": " + error.Message);
                    }
                }
            }
        }
        Console.WriteLine("MANAGED_RESULT passed=" + passed + " failed=" + failed + " environment_failures=" + environmentFailures + " excluded_engine_cases=" + excludedEngineCases);
        Console.WriteLine("Standalone managed fixture execution only; Unity Editor/Test Framework and engine-dependent fixtures were not invoked.");
        return failed == 0 && environmentFailures == 0 && passed > 0 ? 0 : 1;
    }

    private static bool Has(MethodInfo method, string name) => method.GetCustomAttributes(false).Any(a => a.GetType().Name == name);
    private static Exception Unwrap(Exception error) => error is TargetInvocationException && error.InnerException != null ? Unwrap(error.InnerException) : error;
    private static bool IsEnvironmentFailure(Exception error) => error is TypeLoadException || error is FileNotFoundException ||
        error is DllNotFoundException || error is EntryPointNotFoundException || error is System.Security.SecurityException ||
        error is BadImageFormatException || (error.InnerException != null && IsEnvironmentFailure(error.InnerException));
}
