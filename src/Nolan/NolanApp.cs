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

                if (F3NolanScriptBuilder.Parse(script, out List<F3NolanScriptBuilder.Line> lines))
                {
                    F3NolanScriptBuilder builder = new F3NolanScriptBuilder();

                    builder.Build(in lines, out Dictionary<int, object> parts);

                    foreach (var key in builder.Keys)
                    {
                        string outputFilename = Path.ChangeExtension(contentFilename, $"{key}.json");
                        string contentOutput = Path.Combine(currentDirectory, outputFilename);

                        File.WriteAllText(contentOutput, NolanJsonSerializer.SerializeNolanScript(builder[key]));

                        Console.WriteLine($"File: {contentOutput}");
                    }


                    writeGameplayTags( contentFilename, F3NolanGameTag.GameTags);
                }
            }
            else
            {
                Console.WriteLine($"Error (file not found): {contentFilename}");
            }
        }

        private void writeGameplayTags(string contentFilename, List<string> gameTags)
        {
            string filename = Path.GetFileNameWithoutExtension(contentFilename);
            string directory = Path.GetDirectoryName( Path.Combine(Directory.GetCurrentDirectory(), contentFilename) ) ?? Directory.GetCurrentDirectory();

            gameTags.Sort(StringComparer.OrdinalIgnoreCase);

            using (StreamWriter headerWriter = new StreamWriter(Path.Combine(directory, filename + "_GameTags.h")))
            {
                using (StreamWriter bodyWriter = new StreamWriter(Path.Combine(directory, filename + "_GameTags.cpp")))
                {
                    headerWriter.WriteLine(@"#pragma once" + System.Environment.NewLine + System.Environment.NewLine + "#include \"NativeGameplayTags.h\"" + System.Environment.NewLine);
                    bodyWriter.WriteLine($"#include \"{filename}_GameTags.h\"" + System.Environment.NewLine);

                    foreach (string statTag in gameTags)
                    {
                        string tagName = string.Format($"TAG_{statTag.Replace(".", "_")}");

                        headerWriter.WriteLine($"UE_DECLARE_GAMEPLAY_TAG_EXTERN({tagName})");
                        bodyWriter.WriteLine($"UE_DEFINE_GAMEPLAY_TAG({tagName}, \"{statTag}\")");
                    }
                }
            }
        }

        public void play(string filename, string? location = null)
        {
            F3NolanScriptData script;
            int index, inputIndex;
            string? input;

            if (load(filename, out script))
            {
                Console.Clear();
                string scene = string.IsNullOrEmpty(location) ? script.InitialStat.FirstOrDefault().Key : location;

                F3NolanStatData transient = script.InitialStat;
                Console.WriteLine(transient.ToString());

                Dictionary<string, KeyValuePair<string, F3NolanRuleMeta[]>> options = F3NolanScriptBuilder.Compute(scene, transient, script.RuleBook);

                while (options.Count() > 0)
                {
                    index = 0;

                    foreach (var option in options)
                    {
                        Console.WriteLine($"[{index++}] {option.Key.Substring(3)}");
                    }

                    Console.Write("Choose an option: ");
                    input = Console.ReadLine();

                    if (int.TryParse(input, out inputIndex) && inputIndex >= 0 && inputIndex < options.Count())
                    {
                        F3NolanRuleMeta[] optionMeta = options.ElementAt(inputIndex).Value.Value;

                        transient = transient.Apply(optionMeta, ref scene);
                        Console.WriteLine(transient.ToString());

                        string[] optionText = options.ElementAt(inputIndex).Value.Key.Split(',', StringSplitOptions.TrimEntries);

                        if (optionText.Count() == 1)
                        {
                            string currentRoute = optionText[0];

                            while (script.TextBook.Routes.TryGetValue(currentRoute, out var route))
                            {
                                foreach (var text in route.Text.Reverse())
                                {
                                    if (string.IsNullOrWhiteSpace(text) == false)
                                    {
                                        string result = text.EndsWith('%') ? script.TextBook[text.Substring(0, text.Length - 1)] : script.TextBook[text];

                                        if (result.Equals("<$EOF/>")) // TODO handle signal better
                                        {
                                            return;
                                        }

                                        Console.WriteLine(result);
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

                                        List<F3NolanRuleMeta> routeMeta = new List<F3NolanRuleMeta>();

                                        foreach (var payload in route.Flow.ElementAt(inputIndex).Payload)
                                        {
                                            routeMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, payload.Value, string.IsNullOrEmpty(payload.Location) ? scene : payload.Location));
                                            routeMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, payload.Value, string.IsNullOrEmpty(payload.Location) ? scene : payload.Location));
                                        }

                                        foreach (var payload in route.Flow.ElementAt(inputIndex).Gain)
                                        {
                                            routeMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, payload.Value, "DRAG"));
                                            routeMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, payload.Value, "DRAG"));
                                        }

                                        transient = transient.Apply(routeMeta.ToArray(), ref scene);
                                        Console.WriteLine(transient.ToString());
                                    }
                                    else
                                    {
                                        currentRoute = string.Empty;
                                    }

                                    optionText = Array.Empty<string>();
                                }
                                else
                                {
                                    currentRoute = route.Goto ?? string.Empty;

                                    optionText = new string[] { currentRoute };
                                }
                            }
                        }
                        
                        foreach (var text in optionText)
                        {
                            if (string.IsNullOrWhiteSpace(text) == false)
                            {
                                int lineIndex, lineEnds = text.LastIndexOf('_');
                                
                                if (lineEnds < 1)
                                {
                                    lineEnds = text.Length;
                                    lineIndex = 0;                                        
                                }
                                else
                                {
                                    lineIndex = int.Parse(text.Substring(lineEnds + 1));
                                }

                                string textKey = text.Substring(0, lineEnds);

                                if (script.TextBook.Ranges.TryGetValue(textKey, out var range))
                                {
                                    foreach(var rangeText in range)
                                    {
                                        if (lineIndex == rangeText.Start.Value)
                                        {
                                            for (int i = rangeText.Start.Value; i < rangeText.End.Value; i++)
                                            {
                                                string result = script.TextBook[$"{textKey}_{i}"];

                                                if (result.Equals("<$EOF/>")) // TODO handle signal better
                                                {
                                                    return;
                                                }

                                                Console.WriteLine(result);
                                            }

                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    string result = script.TextBook[text];

                                    if (result.Equals("<$EOF/>")) // TODO handle signal better
                                    {
                                        return;
                                    }

                                    Console.WriteLine(result);
                                }
                            }
                        }

                        options = F3NolanScriptBuilder.Compute(scene, transient, script.RuleBook);
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