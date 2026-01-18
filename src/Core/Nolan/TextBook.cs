using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace FrozenFrogFramework.NolanTech
{
    public class F3NolanMutableTextBook
    {
        private Dictionary<string, List<string>> lines;
        private Dictionary<string, List<Range>> ranges;

        private List<string> loopKeys;
        private List<string> onceKeys;

        public F3NolanMutableTextBook()
        {
            lines = new Dictionary<string, List<string>>();
            ranges = new Dictionary<string, List<Range>>();

            loopKeys = new List<string>();
            onceKeys = new List<string>();
        }
        public KeyValuePair<string, F3NolanGameTagSet>[] GetInitialSequence()
        {
            var result = new List<KeyValuePair<string, F3NolanGameTagSet>>();

            if (loopKeys.Count() > 0)
            {
                result.Add(new KeyValuePair<string, F3NolanGameTagSet>("LOOP", new F3NolanGameTagSet(loopKeys)));
            }

            if (onceKeys.Count() > 0)
            {
                result.Add(new KeyValuePair<string, F3NolanGameTagSet>("ONCE", new F3NolanGameTagSet(onceKeys)));
            }

            return result.ToArray();
        }

        public Dictionary<string, string[]> Lines => lines.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        public Dictionary<string, Range[]> Ranges => ranges.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        public IEnumerable<KeyValuePair<string, string[]>> GetIterator()
        {
            List<string> Keys = lines.Keys.ToList();
            Keys.Sort();

            foreach (string key in Keys)
            {
                yield return new KeyValuePair<string, string[]>(key, lines[key].ToArray());
            }
        }
        public void Clear()
        {
            lines.Clear();
            ranges.Clear();

            loopKeys.Clear();
            onceKeys.Clear();
        }
        public string[] AppendLine(string name, string line)
        {
            bool bIsContainsName = lines.ContainsKey(name);

            if (line.StartsWith('(') && line.TrimEnd().EndsWith(">)"))
            {
                if (bIsContainsName)
                {
                    throw NolanException.ContextError($"Sequence '{name}' already exists.", ENolanScriptContext.Text, ENolanScriptError.DuplicateKey);
                }

                bool bIsLoop = line.StartsWith("(?<");

                if (bIsLoop == false && line.StartsWith("(!<") == false)
                {
                    throw NolanException.ContextError($"Sequence '{name}' delimiter '{line[1]}' is invalid.", ENolanScriptContext.Text);
                }

                foreach (string sequenceLine in line.Substring(3, line.Count() - 6).Split("><"))
                {
                    AppendLine(name, sequenceLine);
                }

                if (bIsLoop)
                {
                    loopKeys.Add($"{name}_0");
                }
                else
                {
                    onceKeys.Add($"{name}_0");
                }

                return new string[] { name };
            }

            List<string> trimLines = new List<string>();
            foreach (string splitLine in line.Split("</>"))
            {
                string signal, trimLine, spanLine = splitLine;
                int signalStart = splitLine.IndexOf("<$");
                int signalEnds = splitLine.IndexOf("/>");

                while (signalStart > -1 && signalStart < signalEnds)
                {
                    if (signalStart > 0)
                    {
                        trimLine = spanLine.Substring(0, signalStart).Trim();

                        if (trimLine.Equals(string.Empty) == false)
                        {
                            trimLines.Add(trimLine);
                        }
                    }

                    signal = spanLine.Substring(signalStart, signalEnds - signalStart + 2);

                    trimLines.Add(signal);

                    spanLine = spanLine.Substring(signalEnds + 2).Trim();

                    signalStart = spanLine.IndexOf("<$");
                    signalEnds = spanLine.IndexOf("/>");
                }

                trimLine = spanLine.Trim();

                if (string.IsNullOrEmpty(trimLine) == false)
                {
                    trimLines.Add(trimLine);
                }
            }

            if (trimLines.Count == 0)
            {
                throw NolanException.ContextError($"Text '{name}' is empty.", ENolanScriptContext.Text, ENolanScriptError.NullOrEmpty);
            }

            int rangeStart = bIsContainsName ? lines[name].Count : 0; // must be compute before adding new lines

            if (bIsContainsName)
            {
                foreach (string trimLine in trimLines)
                {
                    lines[name].Add(trimLine);
                }
            }
            else
            {
                lines.Add(name, trimLines);
            }

            // keep track of lines that must be displayed together

            if (trimLines.Count() > 1)
            {
                if (ranges.ContainsKey(name) == false)
                {
                    ranges.Add(name, new List<Range>());
                }

                ranges[name].Add(new Range(rangeStart, rangeStart + trimLines.Count()));
            }

            // compute text key(s) for the new line(s) of text added

            List<string> resultKeys = new List<string>();

            for (int i = rangeStart + trimLines.Count(); i > rangeStart; --i)
            {
                string resultLine = lines[name][i - 1];

                if (resultLine.StartsWith("<$") && resultLine.EndsWith("/>"))
                {
                    resultKeys.Add(resultLine);
                }
                else
                {
                    resultKeys.Add(i == 1 ? $"{name}" : $"{name}_{i - 1}");
                }
            }

            return resultKeys.ToArray();
        }
    }

    [JsonConverter(typeof(F3NolanTextBookConverter))]
    public class F3NolanTextBook
    {
        public static F3NolanTextBook Empty => new F3NolanTextBook(new Dictionary<string, string[]>(), new Dictionary<string, Range[]>(), new Dictionary<string, F3NolanRouteStruct>());
        private Dictionary<string, string[]> lines;
        public string this[string key] { get
        {
            bool bLoop = key.EndsWith('%');
            bool bSequence = key.EndsWith('#');

            if (bLoop || bSequence)
            {
                key = key.Substring(0, key.Count() - 1);
            }

            int lineEnds = key.LastIndexOf('_');
            if (lines.Count() == 1 || lineEnds < 1)
            {
                return lines[key][0];
            }

            int lineIndex = int.Parse(key.Substring(lineEnds + 1));
            if (lineIndex >= lines.Count())
            {
                throw NolanException.ContextError($"Text '{key}' out of range.", ENolanScriptContext.Text, ENolanScriptError.OutOfRange);
            }

            string lineKey = key.Substring(0, lineEnds);
            if (lines.ContainsKey(lineKey) == false)
            {
                throw NolanException.ContextError($"Text '{lineKey}' not found.", ENolanScriptContext.Text, ENolanScriptError.KeyNotFound);
            }

            return lines[lineKey][lineIndex];
        } }
        private Dictionary<string, Range[]> ranges;
        private Dictionary<string, F3NolanRouteStruct> routes;
        public F3NolanTextBook(F3NolanMutableTextBook book, Dictionary<string, F3NolanRouteStruct> stitch)
        {
            lines = book.Lines;

            ranges = book.Ranges;

            routes = stitch;
        }
        public F3NolanTextBook(Dictionary<string, string[]> line, Dictionary<string, Range[]> range, Dictionary<string, F3NolanRouteStruct> route)
        {
            lines = line;

            ranges = range;

            routes = route;
        }
        public Dictionary<string, string[]> Lines { get { return lines; } }

        public Dictionary<string, Range[]> Ranges { get { return ranges; } }
        public Dictionary<string, F3NolanRouteStruct> Routes { get { return routes; } }

        public IEnumerable<KeyValuePair<string, string[]>> GetLineIterator()
        {
            List<string> Keys = lines.Keys.ToList();
            Keys.Sort();

            foreach (string key in Keys)
            {
                yield return new KeyValuePair<string, string[]>(key, lines[key]);
            }
        }
        public IEnumerable<KeyValuePair<string, Range[]>> GetRangeIterator()
        {
            List<string> Keys = ranges.Keys.ToList();
            Keys.Sort();

            foreach (string key in Keys)
            {
                yield return new KeyValuePair<string, Range[]>(key, ranges[key]);
            }
        }
        public IEnumerable<KeyValuePair<string, F3NolanRouteStruct>> GetRouteIterator()
        {
            List<string> Keys = routes.Keys.ToList();
            Keys.Sort();

            foreach (string key in Keys)
            {
                yield return new KeyValuePair<string, F3NolanRouteStruct>(key, routes[key]);
            }
        }
    }
}
