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
                        string outputFilename = Path.ChangeExtension(contentFilename, $"_{key}.json");
                        string contentOutput = Path.Combine(currentDirectory, outputFilename);

                        File.WriteAllText(contentOutput, NolanJsonSerializer.SerializeNolanScript(builder[key]));
                    }
                }
            }
            else
            {
                Console.WriteLine($"File not found: {contentFilename}");
            }
        }

        static private AppRunner<NolanApp> Runner = new AppRunner<NolanApp>();

        static int Main(string[] args)
        {
            return Runner.Run(args);
        }
    }
}