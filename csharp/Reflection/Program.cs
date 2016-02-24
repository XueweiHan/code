using Microsoft.Search.ModelSerialization.Models.Api;
using Microsoft.Search.ModelSerialization.Models.Api.Common;
using Microsoft.Search.ModelSerialization.Models.Api.Entities;
using Microsoft.Search.ModelSerialization.Models.Api.Events;
using Microsoft.Search.ModelSerialization.Models.Api.Movies;
using Microsoft.Search.ModelSerialization.Models.Api.Music;
using Microsoft.Search.ModelSerialization.Models.Api.People;
using Microsoft.Search.ModelSerialization.Models.Api.Places;
using Microsoft.Search.ModelSerialization.Models.Api.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Reflection
{
    class Program
    {
        static void Main(string[] args)
        {
            var program = new Program();
            program.Run();
            System.IO.File.WriteAllText("generated.cs", program.text);

            X.Test.Test1();
        }






        Dictionary<Type, List<Type>> baseToDeriveds = new Dictionary<Type, List<Type>>();

        void Run()
        {
            //ISearchResponse_4
            var allInterfaces = typeof(ISearchResponse_4).Assembly.GetTypes().Where(t => t.IsInterface).ToList();

            // build base to deriveds dictionary
            foreach (var type in allInterfaces)
            {
                foreach (var baseInterface in allInterfaces.Where(t => t.IsAssignableFrom(type) && type != t))
                {
                    List<Type> types;
                    if (!baseToDeriveds.TryGetValue(baseInterface, out types))
                    {
                        types = new List<Type>();
                        baseToDeriveds.Add(baseInterface, types);
                    }
                    types.Add(type);
                }
            }

            //GenerateFile(typeof(IThing_4), typeof(IThing_4));
            GenerateFile(typeof(IEntityAnswer_4), typeof(IRestaurant_4));
            //GenerateFile(typeof(IA), typeof(IF));
        }

        private void GenerateFile(Type sourceType, Type targetType)
        {
            WriteLine("using System.Collections.Generic;");
            WriteLine("using {0};", targetType.Namespace);
            WriteLine("static partial class Test");
            WriteLine("{");
            PushIndent("    ");

            Search(sourceType, targetType, new Dictionary<Type, bool>());

            PopIndent();
            WriteLine("}");
        }

        private bool Search(Type type, Type targetType, Dictionary<Type, bool> searchedTypes)
        {
            //var debugTypeName = type.Name;

            bool found;
            if (searchedTypes.TryGetValue(type, out found))
            {
                return found;
            }
            searchedTypes.Add(type, type == targetType);

            // search properties
            var pathProperties = new HashSet<PropertyInfo>();

            foreach (var property in type.GetProperties())
            {
                var propertyType = property.PropertyType;
                if (propertyType.IsGenericType)
                {
                    // assume bond only generate 2 generic types Nullable<T> and IList<T>, get the type T.
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                // assume bond only generate interfaces, we only care about 
                if (propertyType.IsInterface && Search(propertyType, targetType, searchedTypes))
                {
                    pathProperties.Add(property);
                }
            }

            // search derived types
            var pathDerivedTypes = new HashSet<Type>();

            List<Type> deriveds;
            if (this.baseToDeriveds.TryGetValue(type, out deriveds))
            {
                foreach (var derivedType in deriveds)
                {
                    //var debugDerivedTypeName = derivedType.Name;

                    bool isPath = Search(derivedType, targetType, searchedTypes);
                    if (isPath)
                    {
                        pathDerivedTypes.Add(derivedType);
                    }
                }
            }

            return GenerateMethodForType(type, targetType, searchedTypes, pathProperties, pathDerivedTypes);
        }

        private bool GenerateMethodForType(Type type, Type targetType, Dictionary<Type, bool> searchTypes, HashSet<PropertyInfo> pathProperties, HashSet<Type> pathDerivedTypes)
        {
            if (pathProperties.Count == 0 && pathDerivedTypes.Count == 0 && type != targetType) return false;

            searchTypes[type] = true;
            var tab = "    ";
            WriteLine("public static IEnumerable<{0}> GetAll_{0}(this {1} obj, bool tryBaseInterface = true)", targetType.Name, type.FullName);
            WriteLine("{");
            PushIndent(tab);
            WriteLine("if (obj == null) yield break;");
            if (type == targetType)
            {
                WriteLine("yield return obj;");
            }

            // generate property recursive method
            foreach (var property in pathProperties)
            {
                var propertyType = property.PropertyType;
                if (propertyType.IsGenericType)
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                    if (propertyType.IsInterface)   // IList<T>
                    {
                        WriteLine("if (obj.{0} != null)", property.Name);
                        WriteLine("{");
                        WriteLine("    foreach (var element in obj.{0}) // {1}<{2}>", property.Name, property.PropertyType.Name, propertyType.Name);
                        WriteLine("    {");
                        WriteLine("        foreach (var item in GetAll_{0}(element))", targetType.Name);
                        WriteLine("        {");
                        WriteLine("            yield return item;");
                        WriteLine("        }");
                        WriteLine("    }");
                        WriteLine("}");
                    }
                    // no need to generate code for Nullable<T>
                }
                else
                {
                    WriteLine("foreach (var item in GetAll_{0}(obj.{1})) // {2}", targetType.Name, property.Name, propertyType.Name);
                    WriteLine("{");
                    WriteLine("    yield return item;");
                    WriteLine("}");
                }
            }

            // generate derived type recursive method call
            if (pathDerivedTypes.Count > 0)
            {
                WriteLine("if (!tryBaseInterface) yield break;");
                foreach (var derivedTypes in pathDerivedTypes)
                {
                    WriteLine("foreach (var item in GetAll_{0}(obj as {1}, false))", targetType.Name, derivedTypes.FullName);
                    WriteLine("{");
                    WriteLine("    yield return item;");
                    WriteLine("}");
                }
            }

            PopIndent();
            WriteLine("}");

            return true;
        }




        void WriteLine(string format, params object[] arg)
        {
            foreach (var tab in this.indent)
            {
                Console.Write(tab);
                text += tab;
            }
            if (arg.Length == 0)
            {
                Console.WriteLine(format);
                text += format + "\r\n";
            }
            else
            {
                Console.WriteLine(format, arg);
                text += string.Format(format, arg) + "\r\n";
            }
        }
        List<string> indent = new List<string>();
        void PushIndent(string tab)
        {
            this.indent.Add(tab);
        }
        void PopIndent()
        {
            this.indent.RemoveAt(this.indent.Count - 1);
        }
        string text = string.Empty;
    }
}
