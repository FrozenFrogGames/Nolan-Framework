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
    public struct F3NolanTextData
    {
        private Dictionary<string, List<string>> textKeys = new Dictionary<string, List<string>>();

        public F3NolanTextData(in string name, in string line, Func<string, string, string[]> add)
        {
            textKeys.Add(name, new List<string>());

            textKeys[name].AddRange(add(name, line));
        }

        public F3NolanTextData(in List<string> names, in List<string> lines, Func<string, string, string[]> add)
        {
            foreach (string name in names)
            {
                textKeys.Add(name, new List<string>());
            }

            int nameIndex;

            for (int i = 0; i < lines.Count; i++)
            {
                nameIndex = i % names.Count;

                textKeys[names[nameIndex]].AddRange(add(names[nameIndex], lines[i]));
            }
        }

        public string Name { get { return textKeys.Keys.First() ?? 
                throw NolanException.ContextError($"Name is null.", ENolanScriptContext.Text, ENolanScriptError.NullOrEmpty); }  }
        public string[] Value { get { return textKeys[Name].ToArray(); } }
        public override string ToString()
        {
            List<string> results = new List<string>();

            foreach (var result in textKeys.Values)
            {
                results.Add(string.Join(", ", result));
            }

            return string.Join(", ", results);
        }
        // Text Bank Interface
        public string[] Keys
        {
            get { return textKeys.Keys.ToArray(); }
        }
        public string[] this[string name]
        {
            get
            {
                if (textKeys.ContainsKey(name) == false)
                {
                    throw NolanException.ContextError($"Text '{name}' not found.", ENolanScriptContext.Text, ENolanScriptError.KeyNotFound);
                }

                return textKeys[name].ToArray();
            }
        }
        // Text Syntax Helper
        static public string ParseTextFormat(in string textLine)
        {
            string result = textLine.Replace("<>", "\\n");

            int emphasisLast = 0;
            int emphasisIndex = textLine.IndexOf("<*", emphasisLast);

            while (emphasisIndex > -1)
            {
                emphasisLast = result.IndexOf("/>") + 2;

                string signal = result.Substring(emphasisIndex, emphasisLast - emphasisIndex);
                result = result.Replace(signal, signal.Replace("<*", "\\n<Emphasis>").Replace("/>", "</>"));

                emphasisIndex = textLine.IndexOf("<*", emphasisLast);
            }

            return result;
        }
    }
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

    /// <summary>
    /// Represents a choice in the narrative, which can be a standard route or a dead end.
    /// It contains the text associated with the choice and its indentation depth.
    /// </summary>
    public struct F3NolanRouteData : IEquatable<F3NolanRouteData>
    {
        public struct RouteKnot
        {
            public RouteKnot()
            {
                depthCount = new int[9];
                results = new List<F3NolanRouteData>();

                Clear();
            }
            public void Clear(string? debug = null)
            {
                routeName = debug;
                Depth = 0;

                for (int i = 0; i < 9; ++i)
                {
                    depthCount[i] = 0;
                }

                results.Clear();
            }
            public bool IsValid { get { return string.IsNullOrEmpty(routeName) == false; } }
            public string GetShortName()
            {
                return routeName ?? throw NolanException.ContextError("Name is null.", ENolanScriptContext.Route, ENolanScriptError.NullOrEmpty);
            }
            public string GetLongName(int depth)
            {
                string outName = routeName ?? throw NolanException.ContextError("Name is null.", ENolanScriptContext.Route, ENolanScriptError.NullOrEmpty);

                for (int i = 0; i <= depth / 2; ++i)
                {
                    outName += "-" + depthCount[i];
                }

                if (depth % 2 == 1)
                {
                    outName += "-0";
                }

                return outName;
            }
            private string? routeName;
            public int Depth;
            public int[] depthCount;
            public List<F3NolanRouteData> results;
        }
        public bool IsChoiceLine { get { return Depth % 2 == 0; } }
        public bool IsDeadEnds { get { return bIsDeadEnd; } }
        public string ShortKey { get { return text.Key + "S"; } }
        public string Name { get { return text.Key; } }
        public int Depth { get; }
        public F3NolanGameTagSet ContextOrPayload { get; private set; }
        public F3NolanGameTagSet CostOrGain { get; private set; }
        public string Goto { get { return GotoName ?? 
                throw NolanException.ContextError("Goto is null.", ENolanScriptContext.Route, ENolanScriptError.NullOrEmpty); } }
        private bool bIsDeadEnd;
        private string? GotoName;
        private KeyValuePair<string, List<string>> text;
        public string[] Keys
        {
            get { return text.Value.ToArray(); }
        }
        public override string ToString()
        {
            return string.Join(", ", text.Value);
        }

        public F3NolanRouteData(int depth, string line, ref F3NolanRouteData.RouteKnot route, Func<string, string, string[]> add)
        {
            bIsDeadEnd = depth < 0;

            Depth = bIsDeadEnd ? -3 - depth : depth;
            if (IsDeadEnds)
            {
                if (route.Depth < Depth)
                {
                    throw NolanException.ContextError("Route indentation error.", ENolanScriptContext.Route);
                }

                route.Depth = Depth;
            }
            else
            {
                if (route.Depth == depth)
                {
                    if (route.Depth % 2 == 0)
                    {
                        route.depthCount[route.Depth / 2] += 1;
                    }
                }
                else if (route.Depth > Depth)
                {
                    route.Depth = Depth;

                    if (route.Depth % 2 == 0)
                    {
                        route.depthCount[route.Depth / 2] += 1;
                    }
                }
                else
                {
                    route.Depth = route.Depth + 1;

                    if (route.Depth != Depth)
                    {
                        throw NolanException.ContextError("Route indentation error.", ENolanScriptContext.Route);
                    }

                    if (route.Depth % 2 == 0)
                    {
                        route.depthCount[route.Depth / 2] = 1;
                    }
                }
            }

            text = new KeyValuePair<string, List<string>>(route.GetLongName(Depth), new List<string>());

            ContextOrPayload = F3NolanGameTagSet.Empty;
            CostOrGain = F3NolanGameTagSet.Empty;

            string resultLine = line.TrimStart();

            if (IsDeadEnds)
            {
                ContextOrPayload = ExtractFromPayloadOrGainSyntax(true, ref resultLine);

                CostOrGain = ExtractFromPayloadOrGainSyntax(false, ref resultLine);

                GotoName = string.IsNullOrEmpty(resultLine) ? "EOF" : resultLine.TrimEnd();
            }
            else
            {
                GotoName = null;

                if (IsChoiceLine)
                {
                    ContextOrPayload = ExtractFromPayloadOrGainSyntax(true, ref resultLine);

                    CostOrGain = ExtractFromPayloadOrGainSyntax(false, ref resultLine);

                    int prefixEnds = resultLine.IndexOf('[');
                    int shortEnds = resultLine.IndexOf(']');

                    if (shortEnds > prefixEnds && prefixEnds > -1)
                    {
                        string text = resultLine.Substring(0, prefixEnds) + resultLine.Substring(prefixEnds + 1, shortEnds - prefixEnds - 1);
                        string[] unusedKey = add(ShortKey, text); // add short option to textbook without keeping the key value
                        resultLine = resultLine.Substring(0, prefixEnds) + resultLine.Substring(shortEnds + 1);
                    }
                    else
                    {
                        throw NolanException.ContextError("Route choice error.", ENolanScriptContext.Route);
                    }
                }

                text.Value.AddRange(add(Name, resultLine));
            }
        }

        static private F3NolanGameTagSet ExtractFromPayloadOrGainSyntax(bool isPayload, ref string result)
        {
            int suffixEnds = result.IndexOf(isPayload ? ')' : '}');
            if (result.StartsWith(isPayload ? '(' : '{') && suffixEnds > -1)
            {
                var gameTags = F3NolanDataRuleParser.ParseGameTagSet(result.Substring(0, suffixEnds + 1), isPayload);
                if (gameTags.Success)
                {
                    result = result.Substring(suffixEnds + 1).TrimStart();
                    return gameTags.Value;
                }
            }

            return F3NolanGameTagSet.Empty;
        }

        public override bool Equals(object? obj)
        {
            return obj is F3NolanRouteData data && Equals(data);
        }
        public bool Equals(F3NolanRouteData other)
        {
            return text.Key == other.text.Key &&
                   text.Value == other.text.Value &&
                   bIsDeadEnd == other.bIsDeadEnd &&
                   Depth == other.Depth;
        }
        public static bool operator ==(F3NolanRouteData left, F3NolanRouteData right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(F3NolanRouteData left, F3NolanRouteData right)
        {
            return !left.Equals(right);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + text.Key.GetHashCode();
                hash = hash * 23 + text.Value.GetHashCode();
                hash = hash * 23 + bIsDeadEnd.GetHashCode();
                hash = hash * 23 + Depth.GetHashCode();
                return hash;
            }
        }
    }

    [JsonConverter(typeof(F3NolanStitchStructConverter))]
    public struct F3NolanStitchStruct
    {
        public F3NolanStitchStruct(string key, string next)
        {
            Choice = key;
            Next = next;

            Context = F3NolanGameTagSet.Empty;
            Cost = F3NolanGameTagSet.Empty;
            Payload = F3NolanGameTagSet.Empty;
            Gain = F3NolanGameTagSet.Empty;
        }

        public string Choice;
        public F3NolanGameTagSet Context;
        public F3NolanGameTagSet Cost;
        public F3NolanGameTagSet Payload;
        public F3NolanGameTagSet Gain;
        public string Next;

        public bool Accept(in F3NolanStatData stat, out KeyValuePair<string, F3NolanRuleMeta[]> meta)
        {
            var metaList = new List<F3NolanRuleMeta>();

            var dragTags = new HashSet<string>(stat.Locations.Where(loc => loc.Key == "DRAG").SelectMany(loc => loc.Value).Select(t => t.Value));

            foreach (var tag in Cost)
            {
                switch (tag.TagOperation)
                {
                    case ENolanTagOperation.RemoveOrAppend:
                        if (dragTags.Contains(tag.Value) == false)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                            return false; // Cost tag must be present in DRAG
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, "DRAG"));
                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, "DRAG"));
                        break;

                    case ENolanTagOperation.SucceedIfPresent:
                        if (dragTags.Contains(tag.Value) == false)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                            return false; // Cost tag must be present in DRAG
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, "DRAG"));
                        break;

                    case ENolanTagOperation.FailedIfPresent:
                        if (dragTags.Contains(tag.Value))
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                            return false; // Cost tag must not be present in DRAG
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, "DRAG"));
                        break;
                }
            }

            foreach (var tag in Gain)
            {
                if (dragTags.Contains(tag.Value) == false && tag.TagOperation == ENolanTagOperation.RemoveOrAppend)
                {
                    metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, "DRAG"));
                    metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, tag.Value, "DRAG"));
                }
                else
                {
                    meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                    return false; // the gain tag is already present or invalid syntax (gain tag must be RemoveOrAppend)
                }
            }

            var statLocations = stat.Locations.Where(loc => loc.Key != "DRAG");

            foreach (var tag in Context)
            {
                string? locationName = null;

                foreach (var location in statLocations)
                {
                    if (location.Value.Any(t => t.Value == tag.Value))
                    {
                        if (locationName == null)
                        {
                            locationName = location.Key;
                        }
                        else
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must be present only once
                        }
                    }

                    if (tag.TagOperation == ENolanTagOperation.FailedIfPresent)
                    {
                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, location.Key));
                    }
                }

                switch (tag.TagOperation)
                {
                    case ENolanTagOperation.RemoveOrAppend:
                        if (locationName == null)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must be present in one location
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName));
                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, locationName ?? string.Empty));
                        break;

                    case ENolanTagOperation.SucceedIfPresent:
                        if (locationName == null)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must be present in one location
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName ?? string.Empty));
                        break;

                    case ENolanTagOperation.FailedIfPresent:
                        if (locationName != null)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must not be present in any location
                        }
                        break;
                }
            }

            foreach (var tag in Payload)
            {
                if (tag.TagOperation != ENolanTagOperation.RemoveOrAppend || string.IsNullOrEmpty(tag.Location)|| tag.Location.Equals("DRAG", StringComparison.OrdinalIgnoreCase))
                {
                    meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                    return false; // Payload tag invalid syntax (must be RemoveOrAppend, have a location that's not DRAG)
                }

                foreach (var location in statLocations)
                {
                    if (location.Value.Any(t => t.Value == tag.Value))
                    {
                        meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                        return false; // Payload tag must not be present in any location
                    }

                    metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, location.Key));
                }

                metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, tag.Value, tag.Location));
            }

            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(Next, metaList.ToArray());
            return true;
        }
    }

    [JsonConverter(typeof(F3NolanRouteStructConverter))]
    public struct F3NolanRouteStruct
    {
        public F3NolanRouteStruct(string[] text)
        {
            Text = text;
            Flow = new List<F3NolanStitchStruct>();
            // Context and Cost to evaluate as choice condition
            Goto = null; 
        }

        public string[] Text;
        public List<F3NolanStitchStruct> Flow;
        public string? Goto;

        public bool Accept(in F3NolanStatData stat, out KeyValuePair<string, F3NolanRuleMeta[]> meta)
        {
            var metaList = new List<F3NolanRuleMeta>();

            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
            return false;
        }
    }
}
