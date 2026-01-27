using FrozenFrogFramework.NolanTech;

namespace FrozenFrogFramework.NolanApp
{
    static public class NolanApp
    {
        static int Main(string[] args)
        {
            if (args.Count() == 0)
            {
                PrintHelp(true, true);
            }
            else
            {
                switch(args[0].ToUpper())
                {
                    case "PARSE":
                        if (args.Count() > 1)
                        {
                            parse(args[1]);
                        }
                        else
                        {
                            PrintHelp(true, false);

                            string[] files = NolanApp.GetContentFiles("*.txt");

                            if (files.Count() > 0)
                            {
                                System.Console.WriteLine("Option:");

                                foreach (string file in files)
                                {
                                    System.Console.WriteLine($"  parse {Path.GetFileName(file)}");
                                }                                

                                System.Console.WriteLine();
                            }
                        }
                        break;

                    case "PLAY":
                        if (args.Count() > 2)
                        {
                            play(args[1], args[2].ToUpper());
                        }
                        else if (args.Count() > 1)
                        {
                            play(args[1]);
                        }
                        else
                        {
                            PrintHelp(false, true);

                            string[] files = NolanApp.GetContentFiles("*.json");

                            if (files.Count() > 0)
                            {
                                System.Console.WriteLine("Option:");

                                foreach (string file in files)
                                {
                                    System.Console.WriteLine($"  play {Path.GetFileName(file)}");
                                }                                

                                System.Console.WriteLine();
                            }
                        }
                        break;

                    default:
                        PrintHelp(true, true);
                        break;
                }
            }

            return 1;
        }

        static private void PrintHelp(bool displayParse, bool displayPlay)
        {
            System.Console.WriteLine("Description:");
            System.Console.WriteLine("  Narrative Oriented Language (NOLAN) Command Line Interpreter");
            System.Console.WriteLine();

            if (displayParse || displayPlay)
            {
                System.Console.WriteLine("Command:");
            }

            if (displayParse)
            {
                System.Console.WriteLine("  parse <filename>              Parse file into JSON format.");
            }

            if (displayPlay)
            {
                System.Console.WriteLine("  play <filename> [location]    Play script from the JSON file.");
            }

            if (displayParse || displayPlay)
            {
                System.Console.WriteLine();
            }
        }

        static public void parse(string filename)
        {
            string contentDirectory = NolanApp.GetContentDirectory(filename);
            string contentFullpath = Path.Combine(contentDirectory, filename);

            if (File.Exists(contentFullpath))
            {
                string[] script = File.ReadAllLines(contentFullpath);

                if (F3NolanScriptBuilder.Parse(script, out List<F3NolanScriptBuilder.Line> lines))
                {
                    F3NolanScriptBuilder builder = new F3NolanScriptBuilder();

                    builder.Build(in lines, out Dictionary<int, object> parts);

                    foreach (var key in builder.Keys)
                    {
                        string contentFilename = Path.ChangeExtension(filename, $"{key}.json");
                        string contentOutput = Path.Combine(contentDirectory, contentFilename);

                        File.WriteAllText(contentOutput, NolanJsonSerializer.SerializeNolanScript(builder[key]));

                        Console.WriteLine($"File: {contentOutput}");
                    }

                    writeGameplayTags(contentDirectory, contentFullpath, F3NolanGameTag.GameTags);
                }
            }
            else
            {
                Console.WriteLine($"Error (file not found): {contentFullpath}");
            }
        }

        static private void writeGameplayTags(string contentDirectory, string contentFilename, List<string> gameTags)
        {
            string filename = Path.GetFileNameWithoutExtension(contentFilename);

            gameTags.Sort(StringComparer.OrdinalIgnoreCase);

            string headerFilename = Path.Combine(contentDirectory, filename + "_GameTags.h");

            using (StreamWriter headerWriter = new StreamWriter(headerFilename))
            {
                string bodyFilename = Path.Combine(contentDirectory, filename + "_GameTags.cpp");

                using (StreamWriter bodyWriter = new StreamWriter(bodyFilename))
                {
                    headerWriter.WriteLine(@"#pragma once" + System.Environment.NewLine + System.Environment.NewLine + "#include \"NativeGameplayTags.h\"" + System.Environment.NewLine);
                    bodyWriter.WriteLine($"#include \"{filename}_GameTags.h\"" + System.Environment.NewLine);

                    foreach (string statTag in gameTags)
                    {
                        string tagName = string.Format($"TAG_{statTag.Replace(".", "_")}");

                        headerWriter.WriteLine($"UE_DECLARE_GAMEPLAY_TAG_EXTERN({tagName})");
                        bodyWriter.WriteLine($"UE_DEFINE_GAMEPLAY_TAG({tagName}, \"{statTag}\")");
                    }

                    Console.WriteLine($"File: {bodyFilename}");
                }

                Console.WriteLine($"File: {headerFilename}");
            }
        }

        static public void play(string filename, string? location = null)
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
                        Console.WriteLine($"[{index++}] {option.Key}");
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
                                        int textCount = script.TextBook.Lines[text].Length;
                                        string textKey = transient.ComputeSequence(text, textCount);

                                        string result = script.TextBook[textKey];

                                        if (result.Equals("<$GAMEOVER/>")) // TODO handle signal better
                                        {
                                            return;
                                        }

                                        Console.WriteLine(result);
                                    }
                                }

                                if (route.Flow.Count() > 0)
                                {
                                    index = 0;

                                    Dictionary<string, KeyValuePair<string, F3NolanRuleMeta[]>> flowOptions = new Dictionary<string, KeyValuePair<string, F3NolanRuleMeta[]>>();

                                    foreach (var option in route.Flow)
                                    {
                                        List<F3NolanRuleMeta> flowMeta = new List<F3NolanRuleMeta>();

                                        if (ValidateContext(scene, option.Context, in transient, ref flowMeta) == false)
                                        {
                                            continue;
                                        }

                                        if (ValidateContext("DRAG", option.Cost, in transient, ref flowMeta) == false)
                                        {
                                            continue;
                                        }

                                        if (ValidatePayload(scene, option.Payload, in transient, ref flowMeta) == false)
                                        {
                                            continue;
                                        }

                                        if (ValidatePayload("DRAG", option.Gain, in transient, ref flowMeta) == false)
                                        {
                                            continue;
                                        }

                                        string optionString = script.TextBook[option.Choice];

                                        flowOptions.Add(optionString, new KeyValuePair<string, F3NolanRuleMeta[]>(option.Next, flowMeta.ToArray()));

                                        Console.WriteLine($"[{index++}] {optionString}");
                                    }

                                    Console.Write("Choose an option: ");
                                    input = Console.ReadLine();

                                    if (int.TryParse(input, out inputIndex) && inputIndex >= 0 && inputIndex < flowOptions.Count())
                                    {
                                        currentRoute = flowOptions.ElementAt(inputIndex).Value.Key;
                                        transient = transient.Apply(flowOptions.ElementAt(inputIndex).Value.Value, ref scene);

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

                                int textCount = script.TextBook.Lines[text].Length;
                                string textKey = text.Substring(0, lineEnds);

                                textKey = transient.ComputeSequence(textKey, textCount);

                                if (script.TextBook.Ranges.TryGetValue(textKey, out var range))
                                {
                                    foreach(var rangeText in range)
                                    {
                                        if (lineIndex == rangeText.Start.Value)
                                        {
                                            for (int i = rangeText.Start.Value; i < rangeText.End.Value; i++)
                                            {
                                                string result = script.TextBook[$"{textKey}_{i}"];

                                                if (result.Equals("<$GAMEOVER/>")) // TODO handle signal better
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

                                    if (result.Equals("<$GAMEOVER/>")) // TODO handle signal better
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
            string directory = NolanApp.GetContentDirectory(filename);
            string file = Path.Combine(directory, filename);

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

        static private bool ValidateContext(string scene, F3NolanGameTagSet tags, in F3NolanStatData stat, ref List<F3NolanRuleMeta> meta)
        {
            foreach (var tag in tags)
            {
                string tagLocation = scene.Equals("DRAG") ? scene : (string.IsNullOrEmpty(tag.Location) ? scene : tag.Location);

                switch (tag.TagOperation)
                {
                    case ENolanTagOperation.RemoveOrAppend:
                        if (stat.ContainsTag(tag.Value, tagLocation) == false)
                        {
                            return false;
                        }
                        else
                        {
                            meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, tagLocation));
                            break;
                        }

                    case ENolanTagOperation.FailedIfPresent:
                        if (stat.ContainsTag(tag.Value, tagLocation) == true)
                        {
                            return false;
                        }
                        else
                        {
                            break;
                        }

                    case ENolanTagOperation.SucceedIfPresent:
                        if (stat.ContainsTag(tag.Value, tagLocation) == false)
                        {
                            return false;
                        }
                        else
                        {
                            break;
                        }
                }
            }

            return true;
        }

        static private bool ValidatePayload(string scene, in F3NolanGameTagSet tags, in F3NolanStatData stat, ref List<F3NolanRuleMeta> meta)
        {
            foreach (var tag in tags)
            {
                string tagLocation = scene.Equals("DRAG") ? scene : (string.IsNullOrEmpty(tag.Location) ? scene : tag.Location);

                switch (tag.TagOperation)
                {
                    case ENolanTagOperation.RemoveOrAppend:
                        if (stat.ContainsTag(tag.Value, tagLocation))
                        {
                            return false;
                        }
                        else
                        {
                            meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, tag.Value, tagLocation));
                            break;
                        }

                    case ENolanTagOperation.FailedIfPresent:
                        if (stat.ContainsTag(tag.Value, tagLocation) == false)
                        {
                            return false;
                        }
                        else
                        {
                            meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, tagLocation));
                            break;
                        }
                }
            }

            return true;
        }

        static private string[] GetContentFiles(string extension)
        {
            string directory = Directory.GetCurrentDirectory();                
            string[] files = Directory.GetFiles(directory, extension);
            
            while (files.Count() == 0)
            {
                string contentDir = Path.Combine(directory, "content");

                if (Directory.Exists(contentDir))
                {
                    files = Directory.GetFiles(contentDir, extension);
                }

                if (files.Count() == 0)
                {
                    var parent = Directory.GetParent(directory);

                    if (parent is null)
                    {
                        // content directory not found into parent
                        return Array.Empty<string>(); 
                    }

                    directory = parent.FullName;
                }
            }

            return files;
        }

        static private string GetContentDirectory(string filename)
        {
            string directory = Directory.GetCurrentDirectory();                
            string file = Path.Combine(directory, filename);

            while (File.Exists(file) == false)
            {
                file = Path.Combine(directory, Path.Combine("content", filename));

                if (File.Exists(file))
                {
                    return Path.Combine(directory, "content");
                }
                else
                {
                    var parent = Directory.GetParent(directory);

                    if (parent is null)
                    {
                        // content directory not found into parent
                        return string.Empty; 
                    }

                    directory = parent.FullName;
                }
            }

            return directory;
        }
    }
}