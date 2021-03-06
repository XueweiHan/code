﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    //////////////////////////////////////////////////////////////////
    static void WriteLine(string format, params object[] arg)
    {
        foreach (var tab in indent)
        {
            System.Console.Write(tab);
            text += tab;
        }
        if (arg.Length == 0)
        {
            System.Console.WriteLine(format);
            text += format + Environment.NewLine;
        }
        else
        {
            System.Console.WriteLine(format, arg);
            text += string.Format(format, arg) + Environment.NewLine;
        }
    }
    static System.Collections.Generic.Stack<string> indent = new System.Collections.Generic.Stack<string>();
    static void PushIndent(string tab)
    {
        indent.Push(tab);
    }
    static void PopIndent()
    {
        indent.Pop();
    }
    static string text = string.Empty;
    ////////////////////////////////////////////////////////////////////

    static void Main(string[] args)
    {
        Program p = new Program();
        p.Run();

        System.IO.File.WriteAllText(@"result.cs", text);
    }

    void Run()
    {
        var enlistmentRoot = Environment.GetEnvironmentVariable("INETROOT") ?? @"e:\src\coreux_working";
        var intermediateOutputPath = Environment.GetEnvironmentVariable("IntermediateOutputPath") ?? @"objd\amd64";

        var configFilePath = Path.Combine(enlistmentRoot, @"private\frontend\Answers\services\Widget\Src\KnowledgeConfig.ini");
        configFilePath = @"..\..\libs\KnowledgeConfig.ini"; // hard-code for testing
        var configLines = File.ReadAllLines(configFilePath);

        var schemasFilePath = Path.Combine(enlistmentRoot, @"private\frontend\ApiSchemas\Interfaces", intermediateOutputPath, @"Microsoft.Search.Frontend.ApiSchemas.Interfaces.dll");
        schemasFilePath = @"..\..\libs\Microsoft.Search.Frontend.ApiSchemas.Interfaces.dll"; // hard-code for testing
        var assembly = Assembly.ReflectionOnlyLoadFrom(schemasFilePath);
        InitBaseToDerivedsDictionary(assembly);

        var allAutoGenMethodsKeyInConfigFile = configLines.Where(l => l.StartsWith("ApiResponseAutoGenMethod_") && l.Contains("="))
                                                          .Select(l => l.Substring(0, l.IndexOf("=")).Trim())
                                                          .ToList();

        PushIndent(Tab + Tab);
        foreach (var configKey in allAutoGenMethodsKeyInConfigFile)
        {
            var configValue = GetConfigValue(configKey, configLines);
            var configArray = configValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (configArray.Length > 1)
            {
                // use regex to read the method signature.
                var match = Regex.Match(configArray[0].Trim(), @"^([A-Za-z0-9_.]+)\s*\.\s*([A-Za-z0-9_]+)\s*\(\s*([A-Za-z0-9_.]+)\s*\)$");
                if (match.Success)
                {
                    // get method name, input type, output type, and json paths
                    var methodName = match.Groups[2].Value;
                    var inputTypeName = match.Groups[1].Value;
                    var outputTypeName = match.Groups[3].Value;
                    var jsonPaths = configArray.Skip(1);

                    // find input/output types from api schema assembly
                    var outputType = assembly.GetTypes().FirstOrDefault(t => t.FullName.EndsWith(outputTypeName, StringComparison.Ordinal));
                    var inputType = assembly.GetTypes().FirstOrDefault(t => t.FullName.EndsWith(inputTypeName, StringComparison.Ordinal));

                    if (inputType != null && outputType != null)
                    {
                        // generate method
                        GenerateMethod(methodName, inputType, outputType, jsonPaths, configKey);
                    }
                    else
                    {
                        WriteLine("#error Wrong type name: {0} in config key: {1}", inputType == null ? inputTypeName : outputTypeName, configKey);
                    }
                }
                else
                {
                    WriteLine("#error Unrecognize method signature: {0} in config key: {1}", configArray[0], configKey);
                }
            }

            if (configKey != allAutoGenMethodsKeyInConfigFile.Last())
            {
                WriteLine("");
            }
        }
        PopIndent();
    }



    string Tab = "    ";
    Dictionary<Type, HashSet<Type>> BaseToDeriveds = new Dictionary<Type, HashSet<Type>>();

    private void GenerateMethod(string methodName, Type inputType, Type outputType, IEnumerable<string> jsonPaths, string configKey)
    {
        var pathRoot = new PathItem() { PropertyTypeFullName = inputType.FullName };
        var result = new Stack<PathItem>();

        foreach (var path in jsonPaths)
        {
            var pathArray = path.Trim().Split('.');

            // DFS to find the C# property path from the json path.
            int pathCount = Search(inputType, pathArray, 0, result, pathRoot, configKey);
            if (pathCount == 0)
            {
                WriteLine("#error Json path is wrong: {0} in config key: {1}", path, configKey);
            }
            else if (pathCount > 1)
            {
                WriteLine("// #warning There are more than one C# path found in the API schema sharing the same json path: {0} in conifg key: {1}", path, configKey);
            }
        }

        // variable name postfix dictionary, make sure we don't generate a duplicate local variable name.
        var variableNamePostfixes = new Dictionary<string, int>();

        var paramterName = ToVariableName(inputType.Name, variableNamePostfixes);
        WriteLine("static public void {0}(this {1} {2}, Func<{3}, {3}> func)", methodName, inputType.FullName, paramterName, outputType.FullName);
        WriteLine("{");
        PushIndent(Tab);
        WriteLine("if ({0} == null)", paramterName);
        WriteLine("{");
        WriteLine("    return;");
        WriteLine("}");
        WriteLine("");

        foreach (var item in pathRoot.NextItems.Values)
        {
            GeneratePath(paramterName, item, variableNamePostfixes);

            if (item != pathRoot.NextItems.Values.Last())
            {
                WriteLine("");
            }
        }

        PopIndent();
        WriteLine("}");
    }

    private void GeneratePath(string objectName, PathItem currentItem, Dictionary<string, int> variableNamePostfixes)
    {
        var propertyName = currentItem.PropertyName;
        var typeFullName = currentItem.PropertyTypeFullName;
        var indentCount = 0;
        var variableName = ToVariableName(propertyName ?? typeFullName, variableNamePostfixes);
        var hasChildren = currentItem.NextItems.Values.Any();

        if (propertyName == null)
        {
            WriteLine("var {0} = {1} as {2};", variableName, objectName, typeFullName);
            WriteLine("if ({0} != null)", variableName);
            WriteLine("{");
            PushIndent(Tab);
            indentCount = 1;
        }
        else if (currentItem.IsList)
        {
            WriteLine("if ({0}.{1} != null)", objectName, propertyName);
            WriteLine("{");
            PushIndent(Tab);
            indentCount = 1;

            if (currentItem.JsonPath != null)
            {
                WriteLine("// {0}", currentItem.JsonPath);
                WriteLine("{0}.{1} = {0}.{1}", objectName, propertyName);
                WriteLine("    .Select({0} => func({0}))", variableName);
                WriteLine("    .OfType<{0}>()", typeFullName);
                WriteLine("    .ToList();");

                if (hasChildren) WriteLine("");
            }

            if (hasChildren)
            {
                WriteLine("foreach (var {0} in {1}.{2}.OfType<{3}>())", variableName, objectName, propertyName, typeFullName);
                WriteLine("{");
                PushIndent(Tab);
                indentCount = 2;
            }
        }
        else
        {
            if (currentItem.JsonPath != null)
            {
                WriteLine("// {0}", currentItem.JsonPath);
                WriteLine("{0}.{1} = func({0}.{1}) as {2};", objectName, propertyName, typeFullName);

                if (hasChildren) WriteLine("");
            }

            if (hasChildren)
            {
                WriteLine("var {0} = {1}.{2} as {3};", variableName, objectName, propertyName, typeFullName);
                WriteLine("if ({0} != null)", variableName);
                WriteLine("{");
                PushIndent(Tab);
                indentCount = 1;
            }
        }

        foreach (var item in currentItem.NextItems.Values)
        {
            GeneratePath(variableName, item, variableNamePostfixes);

            if (item != currentItem.NextItems.Values.Last())
            {
                WriteLine("");
            }
        }

        for (var i = 0; i < indentCount; ++i)
        {
            PopIndent();
            WriteLine("}");
        }
    }

    class PathItem
    {
        public string PropertyName { get; set; }
        public string PropertyTypeFullName { get; set; }
        public bool IsList { get; set; }

        public string JsonPath { get; set; }

        public Dictionary<string, PathItem> NextItems
        {
            get
            {
                if (nextItems == null)
                {
                    nextItems = new Dictionary<string, PathItem>();
                }
                return nextItems;
            }
        }

        private Dictionary<string, PathItem> nextItems;
    }

    private void InitBaseToDerivedsDictionary(Assembly assembly)
    {
        BaseToDeriveds = new Dictionary<Type, HashSet<Type>>();

        // enumerate all interface types in schema assembly
        foreach (var type in assembly.GetTypes().Where(t => t.IsInterface))
        {
            // get all base Interfaces of this Interface type
            foreach (var baseType in type.GetInterfaces())
            {
                // add base type and derived type in to dictionary.
                HashSet<Type> deriveds;
                if (!BaseToDeriveds.TryGetValue(baseType, out deriveds))
                {
                    deriveds = new HashSet<Type>();
                    BaseToDeriveds.Add(baseType, deriveds);
                }

                deriveds.Add(type);
            }
        }
    }

    private void MergePath(Stack<PathItem> result, string[] path, PathItem pathRoot, string configKey)
    {
        var current = pathRoot;

        foreach (var item in result.Reverse())
        {
            var key = item.PropertyName + '+' + item.PropertyTypeFullName;

            PathItem next;
            if (!current.NextItems.TryGetValue(key, out next))
            {
                current.NextItems.Add(key, item);
                next = item;
            }

            current = next;
        }

        if (current.JsonPath == null)
        {
            current.JsonPath = string.Join(".", path);
        }
        else
        {
            WriteLine("#warning Duplicated json path: {0} in config key: {1}", string.Join(".", path), configKey);
        }
    }

    private int Search(Type type, string[] path, int level, Stack<PathItem> result, PathItem pathRoot, string configKey)
    {
        if (level == path.Length)
        {
            MergePath(result, path, pathRoot, configKey);
            return 1;
        }

        var propertyName = GetPropertyNameAndType(path[level]).Item1;

        int foundCount = 0;
        // find the property with the same name from all public properties of current type
        var property = GetPublicProperties(type).FirstOrDefault(p => string.Equals(ToCamelCase(p.Name), propertyName, StringComparison.Ordinal));
        if (property != null)
        {
            var propertyUnderlineType = GetPropertyUnderlineType(property.PropertyType);
            result.Push(new PathItem() { PropertyName = property.Name, PropertyTypeFullName = propertyUnderlineType.FullName, IsList = property.PropertyType.IsGenericType });
            foundCount += Search(propertyUnderlineType, path, level + 1, result, pathRoot, configKey);
            result.Pop();
        }

        // we have to search all derived types properties, because some of the properties may defined on the derived types.
        HashSet<Type> deriveds;
        if (BaseToDeriveds.TryGetValue(type, out deriveds))
        {
            foreach (var derived in deriveds)
            {
                // find the property with the same name from the derived type
                property = derived.GetProperties().FirstOrDefault(p => string.Equals(ToCamelCase(p.Name), propertyName, StringComparison.Ordinal));
                if (property != null)
                {
                    var match = level == 0;
                    if (level > 0)
                    {
                        var typeName = GetPropertyNameAndType(path[level - 1]).Item2;
                        match |= typeName == null || derived.FullName.EndsWith(typeName, StringComparison.Ordinal);
                    }

                    if (match)
                    {
                        var propertyUnderlineType = GetPropertyUnderlineType(property.PropertyType);
                        result.Push(new PathItem() { PropertyTypeFullName = derived.FullName });
                        result.Push(new PathItem() { PropertyName = property.Name, PropertyTypeFullName = propertyUnderlineType.FullName, IsList = property.PropertyType.IsGenericType });
                        foundCount += Search(propertyUnderlineType, path, level + 1, result, pathRoot, configKey);
                        result.Pop();
                        result.Pop();
                    }
                }
            }
        }

        return foundCount;
    }

    private Type GetPropertyUnderlineType(Type propertyType)
    {
        // assume api schema bond only contains 1 parameter generic types, such as Nullable<T> and IList<T>, then get the type T.
        return propertyType.IsGenericType ? propertyType.GetGenericArguments()[0] : propertyType;
    }

    static private Tuple<string, string> GetPropertyNameAndType(string propertyName)
    {
        var match = Regex.Match(propertyName, @"^([A-Za-z0-9_]+)\(([A-Za-z0-9_.]+)\)$");
        if (match.Success)
        {
            return new Tuple<string, string>(match.Groups[1].Value, match.Groups[2].Value);
        }

        return new Tuple<string, string>(propertyName, null);
    }

    static private IEnumerable<PropertyInfo> GetPublicProperties(Type type)
    {
        return type.IsInterface
            ? (new[] { type }).Concat(type.GetInterfaces()).SelectMany(i => i.GetProperties())
            : type.GetProperties();
    }

    static private string ToCamelCase(string word)
    {
        return word.Substring(0, 1).ToLowerInvariant() + word.Substring(1);
    }

    static private string ToVariableName(string name, Dictionary<String, int> variableNamePostfixes)
    {
        name = name.Split('.').Last();
        name = ToCamelCase(Regex.Replace(name, @"^I|\d|_", string.Empty));

        if (variableNamePostfixes.ContainsKey(name))
        {
            return name + (++variableNamePostfixes[name]).ToString();
        }
        else
        {
            variableNamePostfixes[name] = 0;
            return name;
        }
    }

    static private string GetConfigValue(string key, string[] configLines)
    {
        for (int i = 0; i < configLines.Length; ++i)
        {
            var values = configLines[i].Split('=');
            if (values.Length == 2 && string.Equals(values[0].Trim(), key, StringComparison.Ordinal))
            {
                var value = values[1].Trim();

                var strBuilder = new StringBuilder();
                while (value.EndsWith("_") && ++i < configLines.Length)
                {
                    strBuilder.Append(value.Substring(0, value.Length - 1));
                    value = configLines[i].Trim();
                }

                strBuilder.Append(value);

                return strBuilder.ToString();
            }
        }

        return null;
    }

}
