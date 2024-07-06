using Spectre.Console;

namespace TarkovDumper
{
    internal class Program
    {
#if DEBUG
        private const string DefaultAssemblyPath = @"Z:\Assembly-CSharp.dll";
        private const string DefaultDumpPath = @"C:\Users\microPower\Documents\GitHub\UnispectEx\Unispect\bin\Release\dump.txt";
#else
        private const string DefaultAssemblyPath = @"C:\Battlestate Games\Escape from Tarkov\EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll";
        private const string DefaultDumpPath = null;
#endif

        static void Main(string[] args)
        {
            StructGenerator structGenerator_gameData = new("GameData");
            StructGenerator structGenerator_classNames = new("ClassNames");
            StructGenerator structGenerator_offsets = new("Offsets");

            AnsiConsole.Profile.Width = 420;

            string assemblyPath;
            string dumpPath;
            if (args.Length == 2)
            {
                assemblyPath = args[0];
                dumpPath = args[1];
            }
            else
            {
                assemblyPath = AnsiConsole.Prompt(
                    new TextPrompt<string>("Assembly-CSharp.dll Path")
                        .PromptStyle("green")
                        .DefaultValue(DefaultAssemblyPath)
                        .ValidationErrorMessage("[red]That's not a valid file path[/]")
                        .Validate(path =>
                        {
                            if (Path.Exists(path))
                                return ValidationResult.Success();
                            else
                                return ValidationResult.Error("[red]You must enter a valid file path[/]");
                        })
                    );

                TextPrompt<string> dumpPathPrompt = new TextPrompt<string>("UnispectEx Dump Path")
                        .PromptStyle("green")
                        .ValidationErrorMessage("[red]That's not a valid file path[/]")
                        .Validate(path =>
                        {
                            if (Path.Exists(path))
                                return ValidationResult.Success();
                            else
                                return ValidationResult.Error("[red]You must enter a valid file path[/]");
                        });
                if (DefaultDumpPath != null)
                    dumpPathPrompt = dumpPathPrompt.DefaultValue(DefaultDumpPath);

                dumpPath = AnsiConsole.Prompt(dumpPathPrompt);
            }

            AnsiConsole.Status().Start("Starting...", ctx => {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("green"));

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Processing entries...");

                Processor processor = null;

                try
                {
                    processor = new(assemblyPath, dumpPath);

                    processor.ProcessGameData(ctx, structGenerator_gameData);
                    processor.ProcessClassNames(ctx, structGenerator_classNames);
                    processor.ProcessOffsets(ctx, structGenerator_offsets);

                    AnsiConsole.Clear();

                    List<StructGenerator> sgList = new()
                    {
                        structGenerator_gameData,
                        structGenerator_classNames,
                        structGenerator_offsets
                    };
                    AnsiConsole.WriteLine(StructGenerator.GenerateNamespace("SDK", sgList));
                    AnsiConsole.WriteLine(StructGenerator.GenerateReports(sgList));
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteLine();

                    if (processor != null)
                        AnsiConsole.MarkupLine($"[bold yellow]Exception thrown while processing step -> {processor.LastStepName}[/]");

                    AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                    if (ex.StackTrace != null)
                    {
                        AnsiConsole.MarkupLine("[bold yellow]==========================Begin Stack Trace==========================[/]");
                        AnsiConsole.WriteLine(ex.StackTrace);
                        AnsiConsole.MarkupLine("[bold yellow]===========================End Stack Trace===========================[/]");
                    }
                }
            });

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Press the space bar to exit...");
            Console.ReadKey();
        }
    }
}
