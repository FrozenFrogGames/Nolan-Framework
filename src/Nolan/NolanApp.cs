using FrozenFrogFramework.NolanTech;
using CommandDotNet;

namespace FrozenFrogFramework.NolanApp
{
    public class NolanApp
    {
        public void parse(string filename)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string contentFullpath = Path.Combine(currentDirectory, filename);

            string contentFilename = File.Exists(contentFullpath) ? filename : Path.Combine("content", filename);
            if (File.Exists(contentFullpath) == false)
            {
                contentFullpath = Path.Combine(currentDirectory, contentFilename);
            }

            if (File.Exists(contentFullpath))
            {
                string[] script = File.ReadAllLines(contentFullpath);

                F3NolanScriptBuilder builder = new F3NolanScriptBuilder();

                if (F3NolanScriptBuilder.Parse(script, out List<F3NolanScriptBuilder.Line> lines))
                {
                    builder.Build(in lines, out Dictionary<int, object> parts);

                    foreach (var key in builder.Keys)
                    {
                        string outputFilename = Path.ChangeExtension(contentFilename, $"{key}.json");
                        string contentOutput = Path.Combine(currentDirectory, outputFilename);

                        File.WriteAllText(contentOutput, NolanJsonSerializer.SerializeNolanScript(builder[key]));

                        Console.WriteLine($"File: {contentOutput}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Error (file not found): {contentFilename}");
            }
        }

        public void play(string filename)
        {
            F3NolanScriptData script;

            if (load(filename, out script))
            {
                F3NolanStatData transient = script.InitialStat;

                Console.WriteLine(transient.ToString());

                string scene = string.Empty;
                int index, inputIndex;
                string? input;

                Dictionary<string, KeyValuePair<string, F3NolanRuleMeta[]>> options = F3NolanScriptBuilder.Compute(transient, script.RuleBook);

                while (options.Count() > 0)
                {
                    index = 0;

                    foreach (var option in options)
                    {
                        Console.WriteLine($"[{index++}] {option.Key}");
                    }

                    Console.Write("Choose an option: ");
                    input = Console.ReadLine();

                    if (int.TryParse(input, out inputIndex) && inputIndex >= 0 && inputIndex < options.Count())
                    {
                        F3NolanRuleMeta[] optionMeta = options.ElementAt(inputIndex).Value.Value;

                        transient = transient.Apply(optionMeta, script.RuleBook, ref scene);

                        Console.WriteLine(transient.ToString());

                        string[] optionText = options.ElementAt(inputIndex).Value.Key.Split(',', StringSplitOptions.TrimEntries);

                        bool bHadRoute = false;

                        if (optionText.Count() == 1)
                        {
                            string currentRoute = optionText[0];

                            while (script.TextBook.Routes.TryGetValue(currentRoute, out var route))
                            {
                                foreach (var text in route.Text)
                                {
                                    if (string.IsNullOrWhiteSpace(text) == false)
                                    {
                                        if (text.EndsWith('%'))
                                        {
                                            Console.WriteLine(script.TextBook[text.Substring(0, text.Length - 1)]);
                                        }
                                        else
                                        {
                                            Console.WriteLine(script.TextBook[text]);
                                        }
                                    }
                                }

                                if (route.Flow.Count() > 0)
                                {
                                    index = 0;

                                    foreach (var option in route.Flow) // TODO filter cost and context from transient stat
                                    {
                                        Console.WriteLine($"[{index++}] {script.TextBook[option.Choice]}");
                                    }

                                    Console.Write("Choose an option: ");
                                    input = Console.ReadLine();

                                    if (int.TryParse(input, out inputIndex) && inputIndex >= 0 && inputIndex < route.Flow.Count())
                                    {
                                        currentRoute = route.Flow.ElementAt(inputIndex).Next;

                                        // TODO apply gain and payload to transient stat

                                        Console.WriteLine(transient.ToString());
                                    }
                                    else
                                    {
                                        currentRoute = string.Empty;
                                    }
                                }
                                else
                                {
                                    currentRoute = route.Goto ?? string.Empty;
                                }

                                bHadRoute = true;
                            }
                        }
                        
                        if (bHadRoute == false)
                        {
                            foreach (var text in optionText)
                            {
                                if (string.IsNullOrWhiteSpace(text) == false)
                                {
                                    Console.WriteLine(script.TextBook[text]);
                                }
                            }
                        }

                        options = F3NolanScriptBuilder.Compute(transient, script.RuleBook);
                    }
                    else
                    {
                        break;
                    }
                }               
            }
        }

        static private bool load(string filename, out F3NolanScriptData script)
        {
            string file = Path.Combine(Directory.GetCurrentDirectory(), filename);

            if (File.Exists(file))
            {
                try
                {
                    script = NolanJsonSerializer.DeserializeNolanScript(string.Join('\n', File.ReadAllLines(file)));

                    return true;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error ({ex.Message}): '{file}'");
                }
            }
            else
            {
                System.Console.WriteLine($"Error (file not found): '{file}'");
            }

            script = F3NolanScriptData.Empty;
            return false;
        }

        static private AppRunner<NolanApp> Runner = new AppRunner<NolanApp>();

        static int Main(string[] args)
        {
            return Runner.Run(args);
        }
    }
}