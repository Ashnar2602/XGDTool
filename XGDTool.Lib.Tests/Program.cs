using System.Reflection;
using Xunit;

var assembly = Assembly.GetExecutingAssembly();
int passed = 0;
int failed = 0;

foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract))
{
    object? instance = null;

    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
    {
        if (method.GetCustomAttribute<TheoryAttribute>() is not null)
        {
            instance ??= Activator.CreateInstance(type);

            foreach (var memberData in method.GetCustomAttributes<MemberDataAttribute>())
            {
                var memberMethod = type.GetMethod(
                    (string)memberData.MemberName,
                    BindingFlags.Public | BindingFlags.Static)!;

                var cases = (IEnumerable<object?[]>)memberMethod.Invoke(null, null)!;
                foreach (var testCase in cases)
                    RunCase(instance, method, testCase);
            }
        }
        else if (method.GetCustomAttribute<FactAttribute>() is not null)
        {
            instance ??= Activator.CreateInstance(type);
            RunCase(instance, method, []);
        }
    }
}

Console.WriteLine();
Console.WriteLine($"Passed: {passed}, Failed: {failed}");
return failed == 0 ? 0 : 1;

void RunCase(object? instance, MethodInfo method, object?[] testArgs)
{
    var label = $"{method.DeclaringType!.Name}.{method.Name}({string.Join(", ", testArgs.Select(a => a switch
    {
        Type t => t.Name,
        null => "null",
        _ => a.ToString()
    }))})";

    try
    {
        method.Invoke(instance, testArgs);
        Console.WriteLine($"PASS  {label}");
        passed++;
    }
    catch (TargetInvocationException ex)
    {
        Console.WriteLine($"FAIL  {label}");
        Console.WriteLine($"      {ex.InnerException?.Message ?? ex.Message}");
        failed++;
    }
}
