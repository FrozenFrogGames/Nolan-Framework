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
        public F3NolanMutableTextBook()
        {
            lines = new Dictionary<string, List<string>>();
            ranges = new Dictionary<string, List<Range>>();
        }
        public string[] GetInitialKeys(string key)
        {
            return F3NolanTextBook.GetInitialKeys(
                key,
                lines.ToDictionary(
                    line => line.Key,
                    line => line.Value.ToArray()
                ),
                ranges.ToDictionary(
                    range => range.Key,
                    range=> range.Value.ToArray()
                ));
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
        }
        public string[] AppendLine(string name, string line)
        {
            bool bIsContainsName = lines.ContainsKey(name);

            if (line.StartsWith('(') && line.EndsWith(">)"))
            {
                if (bIsContainsName) // TODO need to add starting index in key notation in order to differ when sharing name
                {
                    throw NolanException.ContextError($"Sequence '{name}' already exists.", ENolanScriptContext.Text, ENolanScriptError.DuplicateKey);
                }

                bool bIsLoop = line.StartsWith("(?<");

                if (bIsLoop == false && line.StartsWith("(!<") == false)
                {
                    throw NolanException.ContextError($"Sequence '{name}' delimiter '{line[1]}' is invalid.", ENolanScriptContext.Text);
                }

                foreach (string sequenceLine in line.Substring(3, line.Count() - 5).Split("><"))
                {
                    AppendLine(name, sequenceLine);
                }

                return new string[] { $"{name}{(bIsLoop ? '%' : '#')}" }; // TODO support signal into text sequence syntax
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
        public static string[] GetInitialKeys(string key, Dictionary<string, string[]> lines, Dictionary<string, Range[]> ranges)
        {
            List<string> result = new List<string>();
            if (lines.ContainsKey(key))
            {
                int lineCount = lines[key].Count();

                if (ranges.ContainsKey(key))
                {
                    if (ranges[key].First().Start.Value == 0)
                    {
                        lineCount = ranges[key].First().End.Value;
                    }
                }

                for (int i = lineCount; i > 0; --i)
                {
                    string line = lines[key][i - 1];

                    if (line.StartsWith("<$") && line.EndsWith("/>"))
                    {
                        result.Add(line);
                    }
                    else
                    {
                        result.Add(i == 1 ? $"{key}" : $"{key}_{i - 1}");
                    }
                }
            }
            return result.ToArray();
        }
        public string[] GetInitialKeys(string key) { return F3NolanTextBook.GetInitialKeys(key, lines, ranges); }
        private Dictionary<string, string[]> lines;
        public void ComputeKeys(string key, F3NolanStatData stat, ref Dictionary<string, int> stack, ref List<string> results)
        {
            string resultKey;

            if (key.EndsWith('%')) // print looping sequence
            {
                resultKey = key.Substring(0, key.Count() - 1);

                // TODO support range at index to push subset of lines

                if (stack.TryGetValue(key, out var index))
                {
                    resultKey = $"{resultKey}_{index % lines.Count()}";
                    stack[key] = ++index;
                }
                else
                {
                    stack.Add(key, 1);
                }
            }
            else if (key.EndsWith('#')) // print sequence once
            {
                resultKey = key.Substring(0, key.Count() - 1);

                // TODO support range at index to push subset of lines

                if (stack.TryGetValue(key, out var index))
                {
                    resultKey = $"{resultKey}_{index++}";

                    int lastIndex = lines.Count() - 1; // repeat last line

                    stack[key] = index > lastIndex ? lastIndex : index;
                }
                else
                {
                    stack.Add(key, 1);
                }
            }
            else
            {
                int succeedIfPresent = key.IndexOf('?');
                int failedIfPresent = key.IndexOf('!');

                if (succeedIfPresent == -1 && failedIfPresent == -1)
                {
                    resultKey = key;
                }
                else
                {
                    string tagValue = succeedIfPresent > -1 ? key.Substring(succeedIfPresent + 1) : key.Substring(failedIfPresent + 1);

                    var tagLocation = stat.Locations.Where(loc => loc.Value.Any(t => t.Value == tagValue));
                    if ((succeedIfPresent > -1 && tagLocation is null) || (failedIfPresent > -1 && tagLocation is not null))
                    {
                        return; // condition failed
                    }

                    resultKey = succeedIfPresent > -1 ? key.Substring(0, succeedIfPresent) : key.Substring(0, failedIfPresent);
                }
            }

            results.AddRange(resultKey.Split(','));
        }
        public string this[string key] { get
        {
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
