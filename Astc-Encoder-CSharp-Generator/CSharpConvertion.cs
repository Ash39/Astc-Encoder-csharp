using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Astc_Encoder_CSharp_Generator
{
    internal static class CSharpConvertion
    {
        static Dictionary<string,string> declaredBuffers = new Dictionary<string,string>();
        static Dictionary<string,string> functionPointers = new Dictionary<string,string>();
        static Dictionary<string,string> internalTypes = new Dictionary<string,string>();
        static List<string> buffers = new List<string>();


        internal class CSFile
        {
            public CSFile(string name, string code)
            {
                Name = name;
                Code = code;
            }

            public string Name { get; internal set; }
            public string Code { get; internal set; }
        }

        public static void CreateBindings(TaskLoggingHelper Log,AstcSourceDownload sourceDownload, string fileName, string nameSpace, string path, string[] internaltypes)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(sourceDownload.Xml))) 
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Header));

                Header header = (Header)serializer.Deserialize(stream);

                List<CSFile> files = new List<CSFile>();
                StringBuilder mainFileCode =  new StringBuilder();
                StringBuilder mainUnmanagedCode =  new StringBuilder();

                List<Comment> usedComments = new List<Comment>();

                string mainClassName = ToTitleCase(Path.GetFileNameWithoutExtension(fileName));
                string mainUnmanagedClassName = ToTitleCase(Path.GetFileNameWithoutExtension(fileName)) + "Unmanaged";

                mainFileCode.AppendLine($"public partial class {mainClassName} {{");
                mainUnmanagedCode.AppendLine($"internal partial class {mainUnmanagedClassName} {{");

                mainUnmanagedCode.AppendLine($"private const string nativeLibName = \"{Path.GetFileNameWithoutExtension(fileName)}\";");

                foreach (var item in header.Items.Where(obj => obj is FunctionPointer).Select(func => (FunctionPointer)func))
                {
                    FunctionPointerGeneration(item);
                }

                foreach (var item in header.Items)
                {
                    switch (item) 
                    {
                        case Struct strct:
                            files.Add(StructGeneration(strct, internaltypes));
                            break;
                        case Enum enm:
                            files.Add(EnumGeneration(enm, internaltypes));
                            break;
                        case StaticField field:
                            mainFileCode.AppendLine(StaticFieldGeneration(field));
                            break;
                        case Method method:
                            mainUnmanagedCode.AppendLine(MethodGeneration(method));
                            break;
                            }
                    }

                foreach (var buffer in buffers)
                {
                    mainFileCode.AppendLine(buffer);
                }

                mainFileCode.AppendLine("}");
                mainUnmanagedCode.AppendLine("}");

                files.Add(new CSFile($"{mainClassName}.cs", mainFileCode.ToString()));
                files.Add(new CSFile($"{mainUnmanagedClassName}.cs", mainUnmanagedCode.ToString()));

                foreach (var file in files)
                {
                    StringBuilder fileUsings = new StringBuilder();
                    fileUsings.AppendLine("using System;");
                    fileUsings.AppendLine("using System.Runtime.InteropServices;");
                    fileUsings.AppendLine($"using static {nameSpace}.Astcenc;");
                    fileUsings.AppendLine();
                    fileUsings.AppendLine($"namespace {nameSpace};");
                    fileUsings.AppendLine("#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member;");
                    fileUsings.AppendLine(file.Code);
                    fileUsings.AppendLine("#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member;");
                    file.Code = fileUsings.ToString();

                    string folderPath = Path.Combine(path, "Generated");

                    Directory.CreateDirectory(folderPath);

                    string filePath = Path.Combine(folderPath, file.Name);

                    Log.LogMessage($"Generating cs file at {filePath}");

                    File.WriteAllText(filePath, FormatCSharp(file.Code));
                }
            }
        }

        private static string MethodGeneration(Method method)
        {
            StringBuilder stringBuilder = new StringBuilder();

            string pointerTypeName(Field p) => string.IsNullOrEmpty(p.Suffix) ? ToCSharpType(p.Type, p.Prefix) : "IntPtr";

            stringBuilder.AppendLine($"[LibraryImport(nativeLibName, EntryPoint = \"{method.Name}\")]");
            stringBuilder.AppendLine($"internal unsafe static partial {ToCSharpType(method.Return, method.ReturnTypePrefix)} {ToTitleCase(method.Name)}({string.Join(", ", method.Param.Select(p => $"{ pointerTypeName(p)} {ToCamelCase(p.Name)}"))});");

            return stringBuilder.ToString();
        }

        private static string StaticFieldGeneration(StaticField field)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"public static readonly {ToCSharpType(field.Type, field.Modifier)} {ToTitleCase(field.Name)} = {ToTitleCase(field.Initializer)};");

            return stringBuilder.ToString();
        }

        private static void FunctionPointerGeneration(FunctionPointer func)
        {
            if (functionPointers.ContainsKey(func.Name))
            {
                return;
            }
            functionPointers.Add(func.Name, $"delegate* unmanaged<{string.Join(", ", func.Params.Select(p => $"{ToCSharpType(p.Type, p.Prefix)}"))}, {ToCSharpType(func.Return, func.ReturnTypePrefix)}>");
        }

        private static CSFile EnumGeneration(Enum enm, string[] internaltypes)
        {
            StringBuilder stringBuilder = new StringBuilder();

            string enumName = ToTitleCase(enm.Name);

            bool isInternal;

            if (isInternal = internaltypes.Contains(enumName))
            {
                enumName += "Internal";
                if (!internalTypes.ContainsKey(ToTitleCase(enm.Name)))
                    internalTypes.Add(ToTitleCase(enm.Name), enumName);
            }

            string accesser = isInternal ? "internal" : "public";

            stringBuilder.Append($"{accesser} enum {enumName}");

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("{");

            foreach (Constant constant in enm.Constants)
            {
                stringBuilder.AppendLine(ToTitleCase(constant.Name) + (string.IsNullOrEmpty(constant.Value) ? string.Empty : string.Concat(" = ", constant.Value)) + ",");
            }
            stringBuilder.AppendLine("}");

            return new CSFile($"{enumName}.cs", stringBuilder.ToString());
        }

        private static CSFile StructGeneration(Struct strct, string[] internaltypes)
        {
            StringBuilder stringBuilder = new StringBuilder();

            string sructName = ToTitleCase(strct.Name);

            bool isInternal;

            if (isInternal = internaltypes.Contains(sructName))
            {
                sructName += "Internal";
                if(!internalTypes.ContainsKey(ToTitleCase(strct.Name)))
                    internalTypes.Add(ToTitleCase(strct.Name), sructName);
            }

            string accesser = isInternal ? "internal" : "public";

            stringBuilder.AppendLine("[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]");
            stringBuilder.Append($"{accesser} unsafe struct {sructName}");

            if (strct.Fields.Count > 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("{");

                foreach (Field field in strct.Fields)
                {
                    if (string.IsNullOrEmpty(field.Suffix))
                        stringBuilder.AppendLine($"public {ToCSharpType(field.Type, field.Prefix)} {ToCamelCase(field.Name)};");
                    else
                        stringBuilder.AppendLine($"public {string.Format(ConvertToInLineBuffer(ToCSharpType(field.Type, field.Prefix), field.Suffix), ToCamelCase(field.Name))};");
                }
                stringBuilder.AppendLine("}");
            }
            else
                stringBuilder.Append(";");

            return new CSFile($"{sructName}.cs", stringBuilder.ToString());
        }

        private static string ConvertToInLineBuffer(string type, string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                return type;
            }

            if (declaredBuffers.TryGetValue(type + suffix, out string bufferName))
            {
                return bufferName;
            }

            List<string> arrayLengths = new List<string>();

            for (int i = 0; i < suffix.Length; i++)
            {
                char character = suffix[i];
                if (char.IsDigit(character))
                {
                    string numberString = character.ToString();

                    while(char.IsDigit(suffix[++i]))
                    {
                        numberString += suffix[i].ToString();
                    }
                    arrayLengths.Add(numberString);
                }
            }

            StringBuilder stringBuilder = new StringBuilder();

            bufferName = $"Buffer{ToTitleCase(type)}{string.Join("_", arrayLengths)}";

            string sizeString = arrayLengths.Select(a => int.Parse(a)).Aggregate(1, (acc, val) => acc * val).ToString();


            if (arrayLengths.Count > 1)
            {
                stringBuilder.AppendLine($"public static ref {type} {bufferName}Get({type}[] source,{string.Join(", ", GetIndexers(arrayLengths).Select( c => $"int {(char)(c + (int)'i')}"))})");

                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine($"Span<{type}> bufferSpan = source;");
                stringBuilder.AppendLine($"return ref bufferSpan[{GetIndexerOperation()}];");
                stringBuilder.AppendLine("}");

                char GetIndexerName(string str) => (char)(arrayLengths.IndexOf(str) + (int)'i');

                string GetIndexerOperation()
                {
                    string operation = string.Empty;

                    for (int i = 0; i < arrayLengths.Count; i++)
                    {
                        string arrayLength = arrayLengths[i];

                        if (i != 0)
                        {
                            operation += "+";
                        }

                        if (i == arrayLengths.Count - 1)
                        {
                            operation += $"{(char)(i + (int)'i')}";
                            break;
                        }

                        operation += $"({(char)(i + (int)'i')} * {string.Join(" * ", arrayLengths.Skip(i + 1))})";
                    }

                    return operation;
                }

                List<int> GetIndexers(List<string> array) 
                {
                    var indexes = new List<int>();

                    for (int i = 0; i < array.Count; i++)
                    {
                        indexes.Add(i);
                    }

                    return indexes;
                }

                stringBuilder.AppendLine($"public static void {bufferName}Set({type} {string.Join(string.Empty, arrayLengths.Select(c => "[]"))} source, {type}[] desination)");
                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine($"Span<{type}> bufferSpan = desination;");

                stringBuilder.AppendLine("int index = 0;");

                for (int i = 0; i < arrayLengths.Count - 1; i++)
                {
                    char indexerName = (char)(i + (int)'i');
                    stringBuilder.AppendLine($"for (int {indexerName} = 0; {indexerName} < {arrayLengths[i]}; {indexerName}++)");
                    stringBuilder.AppendLine("{");
                }

                string lastArraySize = arrayLengths[arrayLengths.Count - 1];

                stringBuilder.AppendLine($"source{string.Join(string.Empty, arrayLengths.Take(arrayLengths.Count - 1).Select(c => $"[{GetIndexerName(c)}]"))}.AsSpan().CopyTo(bufferSpan.Slice(index, {lastArraySize}));");

                stringBuilder.AppendLine($"index+={lastArraySize};");

                for (int i = 0; i < arrayLengths.Count - 1; i++)
                {
                    stringBuilder.AppendLine("}");
                }

                stringBuilder.AppendLine("}");
            }

            buffers.Add(stringBuilder.ToString());

            string fixedBuffer = $"fixed {type} {{0}}[{sizeString}]";

            declaredBuffers.Add(type + suffix, fixedBuffer);

            return fixedBuffer;
        }

        private static string ToCamelCase(string text) 
        {
            TextInfo txtInfo = new CultureInfo("en-US", false).TextInfo;
            string titleCaseString = txtInfo.ToTitleCase(text).Replace("_", string.Empty);

            return char.ToLowerInvariant(titleCaseString[0]) + titleCaseString.Substring(1);
        }

        private static string ToTitleCase(string text)
        {
            TextInfo txtInfo = new CultureInfo("en-US", false).TextInfo;
            return txtInfo.ToTitleCase(text.ToLowerInvariant()).Replace("_", string.Empty);
        }

        private static string ToCSharpType(string baseType, string specifiers)
        {
            if (string.IsNullOrEmpty(specifiers))
            {
                specifiers = string.Empty;
            }

            int pointerDepth = 0;
            for (int i = baseType.Length - 1; i >= 0 && baseType[i] == '*'; i--)
                pointerDepth++;

            if (pointerDepth > 0)
                baseType = baseType.Substring(0, baseType.Length - pointerDepth).Trim();

            bool isUnsigned = specifiers.Contains("unsigned");
            bool isSigned = specifiers.Contains("signed") && !isUnsigned;

            bool isShort = specifiers.Contains("short");
            bool isLongLong = specifiers.Contains("long long");
            bool isLong = specifiers.Contains("long") && !isLongLong;

            string resolved;

            switch (baseType)
            {
                case "char":
                    resolved = isUnsigned ? "byte" :
                               isSigned ? "sbyte" : "byte";
                    break;

                case "int":
                    if (isShort)
                        resolved = isUnsigned ? "ushort" : "short";
                    else if (isLongLong || isLong)
                        resolved = isUnsigned ? "ulong" : "long";
                    else
                        resolved = isUnsigned ? "uint" : "int";
                    break;

                case "float":
                    resolved = "float";
                    break;

                case "double":
                    resolved = "double";
                    break;

                case "bool":
                case "_Bool":
                    resolved = "bool";
                    break;

                case "void":
                    resolved = "void";
                    break;

                case "wchar_t":
                    resolved = "char";
                    break;

                case "uint8_t":
                    resolved = "byte";
                    break;
                case "uint16_t":
                    resolved = "ushort";
                    break;
                case "uint32_t":
                    resolved = "uint";
                    break;
                case "uint64_t":
                    resolved = "ulong";
                    break;
                case "int8_t":
                    resolved = "sbyte";
                    break;
                case "int16_t":
                    resolved = "short";
                    break;
                case "int32_t":
                    resolved = "int";
                    break;
                case "int64_t":
                    resolved = "long";
                    break;

                case "size_t":
                    resolved = "UIntPtr";
                    break;

                default:
                    if (isLong || isLongLong || isShort || isSigned || isUnsigned)
                        throw new NotSupportedException(
                            "Unsupported C type: " + specifiers + " " + baseType);

                    if (functionPointers.TryGetValue(baseType, out string funcPointer))
                    {
                        resolved = funcPointer;
                        break;
                    }
                    resolved = ToTitleCase(baseType);

                    if (internalTypes.TryGetValue(resolved, out string internalType))
                    {
                        resolved = internalType;
                        break;
                    }

                    break;
            }

            if (pointerDepth > 0)
            {
                if (resolved == "void")
                    return "void" + new string('*', pointerDepth);

                return resolved + new string('*', pointerDepth);
            }

            return resolved;
        }

        public static string FormatCSharp(string code)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var workspace = new AdhocWorkspace();
            var formattedRoot = Formatter.Format(root, workspace);

            return formattedRoot.ToFullString();
        }
    }
}