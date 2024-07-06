using dnlib.DotNet;
using Kokuban;
using System.Text;

namespace TarkovDumper
{
    public sealed class StructGenerator(string name)
    {
        private const string namespaceTemplate = "namespace ${name}";
        private const string structTemplate = "public readonly struct ${name}";
        private const string stringTemplate = "public const string ${name} = @\"${value}\";";
        private const string offsetTemplate = "public const uint ${name} = ${value}; // ${comment}";
        private const string offsetChainTemplate = "public static readonly uint[] ${name} = new uint[] { ${values} }; // ${comment}";

        private readonly string _name = name;
        private readonly List<string> _inlineEntries = new();
        private readonly List<StructGenerator> _nestedStructs = new();

        public int ClassesProcessed { get; private set; } = 0;
        public int ClassesErrored { get; private set; } = 0;

        public int MethodsProcessed { get; private set; } = 0;
        public int MethodsErrored { get; private set; } = 0;

        public int OffsetsProcessed { get; private set; } = 0;
        public int OffsetsErrored { get; private set; } = 0;

        public void AddClassName(TypeDef typeDef, string variable, string entity, bool outputFullName = false)
        {
            if (typeDef != null)
            {
                AddString(variable, typeDef.Humanize(outputFullName));

                ClassesProcessed++;
            }
            else
            {
                AddError($"[ERROR] Unable to find: \"{variable}\" using entity: \"{entity}\"!");

                ClassesErrored++;
            }
        }

        public void AddClassName(string className, string variable, string entity)
        {
            if (className != null)
            {
                AddString(variable, className);

                ClassesProcessed++;
            }
            else
            {
                AddError($"[ERROR] Unable to find: \"{variable}\" using entity: \"{entity}\"!");

                ClassesErrored++;
            }
        }

        public void AddMethodName(MethodDef methodDef, string variable, string entity)
        {
            if (methodDef != null)
            {
                AddString(variable, methodDef.Humanize());

                MethodsProcessed++;
            }
            else
            {
                AddError($"[ERROR] Unable to find: \"{variable}\" using entity: \"{entity}\"!");

                MethodsErrored++;
            }
        }

        public void AddMethodName(string methodName, string variable, string entity)
        {
            if (methodName != null)
            {
                AddString(variable, methodName);

                MethodsProcessed++;
            }
            else
            {
                AddError($"[ERROR] Unable to find: \"{variable}\" using entity: \"{entity}\"!");

                MethodsErrored++;
            }
        }

        public void AddString(string name, string value, bool colorize = true)
        {
            string output = HydrateStringTemplate(name, value);

            if (colorize)
                output = ColorizeStringTemplate(output);

            _inlineEntries.Add(output);
        }

        public void AddOffset(string name, DumpParser.Result<DumpParser.OffsetData> offsetData, bool colorize = true)
        {
            if (offsetData.Success)
            {
                if (name.Contains("k__BackingField", StringComparison.OrdinalIgnoreCase))
                    name = name.TrimStart('<').Replace(">k__BackingField", "");

                string output = HydrateOffsetTemplate(name, offsetData.Value.ToString(), offsetData.Value.TypeName);

                if (colorize)
                    output = ColorizeOffsetTemplate(output);

                _inlineEntries.Add(output);

                OffsetsProcessed++;
            }
            else
            {
                AddError($"[ERROR] Unable to find offset: \"{name}\"!");

                OffsetsErrored++;
            }
        }

        public void AddOffsetChain(string name, List<DumpParser.Result<DumpParser.OffsetData>> offsetData, bool colorize = true)
        {
            if (offsetData.Any(x => x.Success == false))
            {
                AddError($"[ERROR] Unable to find offset: \"{name}\"!");
                OffsetsErrored++;
                return;
            }

            List<string> offsets = new();
            List<string> typeNames = new();
            foreach (var offset in offsetData)
            {
                offsets.Add(offset.Value.ToString());
                typeNames.Add(offset.Value.TypeName);
            }

            if (name.Contains("k__BackingField", StringComparison.OrdinalIgnoreCase))
                name = name.TrimStart('<').Replace(">k__BackingField", "");

            string output = HydrateOffsetChainTemplate(name, offsets, typeNames);

            if (colorize)
                output = ColorizeOffsetTemplate(output);

            _inlineEntries.Add(output);

            OffsetsProcessed++;
        }

        public void AddEmptyLine()
        {
            _inlineEntries.Add("");
        }

        public void AddStruct(StructGenerator nestedStruct)
        {
            _nestedStructs.Add(nestedStruct);

            ClassesProcessed += nestedStruct.ClassesProcessed;
            ClassesErrored += nestedStruct.ClassesErrored;

            MethodsProcessed += nestedStruct.MethodsProcessed;
            MethodsErrored += nestedStruct.MethodsErrored;

            OffsetsProcessed += nestedStruct.OffsetsProcessed;
            OffsetsErrored += nestedStruct.OffsetsErrored;
        }

        public void AddError(string message)
        {
            _inlineEntries.Add(Chalk.Red + ("// " + message));
        }

        public string ToString(bool colorize = true)
        {
            var sb = new StringBuilder();

            if (colorize)
                sb.AppendLine(ColorizeStructTemplate(InsertInTemplate(structTemplate, "name", _name)));
            else
                sb.AppendLine(InsertInTemplate(structTemplate, "name", _name));

            sb.AppendLine("{");

            if (_inlineEntries.Count == 0 && _nestedStructs.Count == 0)
                AddError("No data!");

            bool linesAppended = false;

            foreach (var constString in _inlineEntries)
            {
                sb.AppendLine($"\t{constString}");
                linesAppended = true;
            }

            for (int i = 0; i < _nestedStructs.Count; i++)
            {
                var nestedStruct = _nestedStructs[i];

                if (i > 0 || (i == 0 && linesAppended))
                    sb.AppendLine("");

                string nestedString = nestedStruct.ToString(colorize).Replace("\n", "\n\t");
                sb.AppendLine($"\t{nestedString.TrimEnd()}");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        public string GenerateReport()
        {
            var sb = new StringBuilder();

            static string ProcessReportItem(string title, int processed, int errored)
            {
                var sb = new StringBuilder();

                bool any = processed > 0 || errored > 0;
                if (!any)
                    return null;

                sb.AppendLine(Chalk.Blue + $"{title}:");

                if (processed > 0)
                    sb.AppendLine(Chalk.Green + $"\t- Processed: {processed}");

                if (errored > 0)
                    sb.AppendLine(Chalk.Red + $"\t- Errored: {errored}");

                return sb.ToString();
            }

            string classes = ProcessReportItem("Classes", ClassesProcessed, ClassesErrored);
            string methods = ProcessReportItem("Methods", MethodsProcessed, MethodsErrored);
            string offsets = ProcessReportItem("Offsets", OffsetsProcessed, OffsetsErrored);

            if (classes == null && methods == null && offsets == null)
                return null;

            sb.AppendLine(Chalk.White + $"{_name} Generation Report");
            if (classes != null)
                sb.AppendLine(classes);
            if (methods != null)
                sb.AppendLine(methods);
            if (offsets != null)
                sb.AppendLine(offsets);

            return sb.ToString();
        }

        public static string GenerateNamespace(string name, List<StructGenerator> structs, bool colorize = true)
        {
            var sb = new StringBuilder();

            if (colorize)
                sb.AppendLine(ColorizeNamespaceTemplate(InsertInTemplate(namespaceTemplate, "name", name)));
            else
                sb.AppendLine(InsertInTemplate(namespaceTemplate, "name", name));

            sb.AppendLine("{");

            for (int i = 0; i < structs.Count; i++)
            {
                var sg = structs[i];

                string nestedStruct = sg.ToString(colorize).Replace("\n\t", "\n\t\t");
                nestedStruct = nestedStruct.Insert(nestedStruct.IndexOf('{'), "\t");
                nestedStruct = nestedStruct.Insert(nestedStruct.LastIndexOf('}'), "\t");
                sb.AppendLine($"\t{nestedStruct.TrimEnd()}");

                if (i + 1 < structs.Count)
                    sb.AppendLine("");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        public static string GenerateReports(List<StructGenerator> structs)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < structs.Count; i++)
            {
                var sg = structs[i];

                string report = sg.GenerateReport();
                if (report == null)
                    continue;

                sb.AppendLine(report);

                if (i + 1 < structs.Count)
                    sb.AppendLine("");
            }

            return sb.ToString();
        }

        private static string InsertInTemplate(string template, string templateItem, string value)
        {
            return template.Replace("${" + templateItem + "}", value);
        }

        private static string HydrateStringTemplate(string name, string value)
        {
            string template = stringTemplate;

            template = InsertInTemplate(template, "name", name);
            template = InsertInTemplate(template, "value", value);

            return template;
        }

        private static string HydrateOffsetTemplate(string name, string offset, string type)
        {
            string template = offsetTemplate;

            template = InsertInTemplate(template, "name", name);
            template = InsertInTemplate(template, "value", offset);
            template = InsertInTemplate(template, "comment", type);

            return template;
        }

        private static string HydrateOffsetChainTemplate(string name, List<string> offsets, List<string> types)
        {
            string template = offsetChainTemplate;

            template = InsertInTemplate(template, "name", name);
            template = InsertInTemplate(template, "values", offsets.JoinAsString());
            template = InsertInTemplate(template, "comment", types.JoinAsString());

            return template;
        }

        private static string ColorizeStringTemplate(string template)
        {
            template = Chalk.Blue + template;

            var varNameCandidates = template.Split('=')[0].Split(' ');
            string varName = varNameCandidates[^2];

            template = template.Replace(varName + ' ', Chalk.White + varName + ' ');

            template = template.Split('=')[0] + '=' + Chalk.Green + template.Split('=')[1].Split(';')[0] + ';';

            return template;
        }

        private static string ColorizeOffsetTemplate(string template)
        {
            template = Chalk.Blue + template;

            var varNameCandidates = template.Split('=')[0].Split(' ');
            string varName = varNameCandidates[^2];

            template = template.Replace(varName + ' ', Chalk.White + varName + ' ');

            template = template.Split('=')[0] + '=' + Chalk.Green + template.Split('=')[1].Split(';')[0] + ';' + Chalk.Yellow + template.Split(";")[1];

            return template;
        }

        private static string ColorizeStructTemplate(string template)
        {
            template = Chalk.Blue + template;

            var varNameCandidates = template.Split('=')[0].Split(' ');
            string varName = varNameCandidates[^1];

            template = template.Replace(varName, Chalk.Green + varName);

            return template;
        }

        private static string ColorizeNamespaceTemplate(string template)
        {
            template = Chalk.Blue + template;

            var varNameCandidates = template.Split('=')[0].Split(' ');
            string varName = varNameCandidates[^1];

            template = template.Replace(varName, Chalk.White + varName);

            return template;
        }
    }
}
