using System;
using System.Collections.Generic;
using System.Linq;
using Mephi.Cybernetics.Mace.AbstractMachines.Cam;
using Mephi.Cybernetics.Mace.Core;
using Mephi.Cybernetics.Race.Core.Host.Definitions; // Используйте этот namespace для ITerm
using Environment = Mephi.Cybernetics.Mace.AbstractMachines.Cam.Environment;
using System.Text.Json;

namespace PiServer.Services
{
    public class MachineBundle
    {
        public IAbstractMachine? Machine;
        public ILanguageCompiler? Compiler;
        public ILanguageParser? Parser;

        public ITerm Parse(string code) => Parser!.Parse(code);
        public IMachineCode Compile(ITerm term) => Compiler!.Compile(term);
        public IMachineState EvaluateOneStep(IMachineCode code) => Machine!.EvaluateCode(code, true);
        public IMachineState EvaluateOneStep(IMachineState machineState) => Machine!.EvaluateState(machineState);
    }

    public static class MachineSelector
    {
        private static IDictionary<string, MachineBundle?> _machines = new Dictionary<string, MachineBundle?>
        {
            {
                "CAM", new MachineBundle
                {
                    Machine = new Machine.CAM(),
                    Compiler = new Compiler.Compiler(Environment.CoreVersion.V2, false),
                    Parser = new Parser.CamParser(Environment.CoreVersion.V2),
                }
            },
            {
                "Beta Reductor", null
            }
        };

        public static MachineBundle? GetMachineByName(string machineName) => _machines[machineName];
    }

    public static class LambdaEvaluator
    {
public static object EvaluateLambda(string lambdaExpression)
{
    var machineBundle = MachineSelector.GetMachineByName("CAM");
    if (machineBundle == null)
    {
        throw new InvalidOperationException("CAM machine bundle is not available");
    }

    var parsed = machineBundle.Parse(lambdaExpression);
    var compiled = machineBundle.Compile(parsed);
    var resultState = machineBundle.Machine!.EvaluateCode(compiled, false);

    // Получаем результат как в оригинальном коде
    return GetResultFromMachineState(resultState, machineBundle.Machine);
}

private static object GetResultFromMachineState(IMachineState machineState, IAbstractMachine machine)
{
    // Пробуем основные регистры, где обычно хранится результат
    string[] resultRegisters = { "ACC", "RESULT", "R0", "value", "output" };
    
    foreach (var registerName in resultRegisters)
    {
        try
        {
            // Используем тот же метод, что и в оригинальном коде
            string value = machineState.GetRegisterStringValue(registerName);
            if (!string.IsNullOrEmpty(value) && value != "null" && value != "0")
            {
                return value;
            }
        }
        catch
        {
            // Регистр может не существовать, продолжаем поиск
        }
    }
    
    // Если не нашли в конкретных регистрах, проверяем все доступные регистры
    foreach (var register in machine.Registers)
    {
        try
        {
            string value = machineState.GetRegisterStringValue(register.Name);
            if (!string.IsNullOrEmpty(value) && value != "null" && value != "0")
            {
                return value;
            }
        }
        catch
        {
            // Пропускаем недоступные регистры
        }
    }
    
    // Если ничего не нашли, возвращаем строковое представление состояния
    return machineState.ToString();
}



        // Вспомогательные методы для работы с регистрами
        private static IEnumerable<string> GetRegisterNames(IMachineState machineState)
        {
            // Попробуйте разные методы доступа к регистрам
            try
            {
                // Если есть метод GetRegisterNames
                return machineState.GetType().GetMethod("GetRegisterNames")?.Invoke(machineState, null) as IEnumerable<string> 
                    ?? new[] { "result", "output", "value", "r0", "r1" };
            }
            catch
            {
                return new[] { "result", "output", "value", "r0", "r1" };
            }
        }

        private static object GetRegisterValue(IMachineState machineState, string registerName)
        {
            try
            {
                // Если есть метод GetRegister
                return machineState.GetType().GetMethod("GetRegister")?.Invoke(machineState, new object[] { registerName })
                    ?? machineState.GetType().GetProperty(registerName)?.GetValue(machineState);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsEmptyValue(object value)
        {
            if (value == null) return true;
            if (value is string str && string.IsNullOrEmpty(str)) return true;
            if (value is Array array && array.Length == 0) return true;
            if (value is IEnumerable<object> enumerable && !enumerable.Any()) return true;
            return false;
        }

        public static string EvaluateLambdaToString(string lambdaExpression)
        {
            var result = EvaluateLambda(lambdaExpression);
            return result?.ToString() ?? string.Empty;
        }

        public static T EvaluateLambda<T>(string lambdaExpression)
        {
            var result = EvaluateLambda(lambdaExpression);
            try
            {
                return (T)Convert.ChangeType(result, typeof(T));
            }
            catch (InvalidCastException)
            {
                throw new InvalidOperationException($"Cannot convert result of type {result?.GetType().Name} to {typeof(T).Name}");
            }
        }
    }
}