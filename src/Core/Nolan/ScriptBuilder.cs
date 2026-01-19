using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace FrozenFrogFramework.NolanTech
{
    public enum ENolanScriptContext
    {
        /// <summary>
        /// Un bloc de texte (commence par '##') est en attente.
        /// </summary>
        Text,
        /// <summary>
        /// Un bloc de statistiques (commence par '==') est en attente.
        /// </summary>
        Stat,
        /// <summary>
        /// Un bloc de scène (commence par '>>') est attendu.
        /// </summary>
        Knot, // TODO rename to Tape
        /// <summary>
        /// Un bloc de règle (commence par '++') est en attente.
        /// </summary>
        Rule,
        /// <summary>
        /// Un bloc de route (commence par '--') est en attente.
        /// </summary>
        Route,
        /// <summary>
        /// Un bloc d'objectif (commence par '**') est en attente.
        /// </summary>
        Page,
        /// <summary>
        /// Un bloc d'objectif (commence par '??') est en attente.
        /// </summary>
        Goal
    }

    public class F3NolanScriptBuilder
    {
        public struct Line
        {
            public char Delimiter { get; set; }
            public string Content { get; set; }

            public Line(string line)
            {
                if (string.IsNullOrEmpty(line))
                {
                    Delimiter = '~'; // CONCAT
                    Content = string.Empty;
                    return;
                }

                foreach (char delimiter in new char[] { '=', '>', '+', '-', '#', '/', '*', '?' })
                {
                    if (line.StartsWith(new string(delimiter, 2)) || (delimiter == '=' && line.StartsWith("=>")))
                    {
                        Delimiter = delimiter; // STAT, TAPE, RULE, ROUTE, TEXT, COMMENT, GOAL, STEP
                        Content = line.Substring(2).Trim() + ' ';
                        return;
                    }
                }

                Delimiter = '~'; // CONCAT
                Content = line.Trim() + ' ';
            }

            public void Merge(Line nextLine)
            {
                if (nextLine.Delimiter == '~' && string.IsNullOrEmpty(nextLine.Content) == false)
                {
                    Content += nextLine.Content;
                }
            }
        }
        protected Dictionary<string, F3NolanScriptData> _tapes = new Dictionary<string, F3NolanScriptData>(StringComparer.OrdinalIgnoreCase);

        public void Build(in List<F3NolanScriptBuilder.Line> lines, out Dictionary<int, object> parts)
        {
            _tapeLabel = null;
            _tapes.Clear();

            parts = new Dictionary<int, object>();

            int routeStart = -1, routeEnd = -1;

            for (int i = 0; i < lines.Count; ++i)
            {
                if (lines[i].Delimiter == '-')
                {
                    if (routeStart == -1)
                    {
                        routeStart = i;
                    }
                    else
                    {
                        routeEnd = i;
                    }

                    continue; // process the whole route together
                }
                else
                {
                    BuildPart(lines, i, ref parts);
                }

                if (routeStart > -1 && routeEnd > routeStart)
                {
                    int routeLabel = -1;

                    for (int j = routeStart; j > 0; --j)
                    {
                        if (lines[j - 1].Delimiter == '+')
                        {
                            routeLabel = j - 1;
                            break;
                        }
                    }

                    BuildRoute(lines, routeLabel, routeStart, routeEnd, ref parts);

                    routeStart = routeEnd = -1;
                }
            }

            if (routeStart > -1 && routeEnd > routeStart)
            {
                int routeLabel = -1;

                for (int j = routeStart; j > 0; --j)
                {
                    if (lines[j - 1].Delimiter == '+')
                    {
                        routeLabel = j - 1;
                        break;
                    }
                }

                BuildRoute(lines, routeLabel, routeStart, routeEnd, ref parts);
            }

            BuildTape(lines, ref parts);
        }

        protected void BuildPart(in List<F3NolanScriptBuilder.Line> lines, int index, ref Dictionary<int, object> parts)
        {
            switch (lines[index].Delimiter)
            {
                case '=': // STAT
                    BuildTape(lines, ref parts);

                    string line = lines[index].Content.Trim();
                    int statIndex = line.IndexOf('[');

                    _tapeLabel = line.Substring(0, statIndex).Trim();

                    var stat = F3NolanDataStatParser.Parse($"== {line.Substring(statIndex)}");

                    parts.Add(index, stat.Success ? stat.Value : F3NolanStatData.Empty);
                    break;

                case '+': // RULE
                    var rule = F3NolanDataRuleParser.Parse($"++ {lines[index].Content}", _textbook.AppendLine);
                    if (rule.Success)
                    {
                        parts.Add(index, rule.Value);
                    }
                    break;

                case '#': // TEXT
                    var text = F3NolanDataTextParser.Parse($"## {lines[index].Content}", _textbook.AppendLine);
                    if (text.Success)
                    {
                        parts.Add(index, text.Value);
                    }
                    break;

                case '*': // GOAL
                    break;

                case '?': // STEP
                    break;

                case '/': // COMMENT
                    parts.Add(index, lines[index].Content.Trim());
                    break; 
            }
        }

        private string? _tapeLabel = null;
        private int _tapeStart = 0;

        protected void BuildTape(in List<F3NolanScriptBuilder.Line> lines, ref Dictionary<int, object> parts)
        {
            if (_tapeLabel != null)
            {
                F3NolanTextBook scriptText = new F3NolanTextBook(_textbook, _routeBank.ToDictionary(pair => pair.Key, pair => pair.Value));

                F3NolanStatData scriptStat = F3NolanStatData.Empty;
                if (lines[_tapeStart].Delimiter == '=' && parts[_tapeStart] is F3NolanStatData stat)
                {
                    scriptStat = stat;

                    var sequenceStat = _textbook.GetInitialSequence();

                    scriptStat.InitializeSequence(sequenceStat);
                }

                List<F3NolanRuleData> rulebook = new List<F3NolanRuleData>();

                for (int j = 0; j < lines.Count; ++j)
                {
                    if (lines[j].Delimiter == '+' && parts[j] is F3NolanRuleData rule)
                    {
                        rulebook.Add(rule);
                    }
                }

                F3NolanScriptData productStruct = new F3NolanScriptData(scriptStat, rulebook.ToArray(), scriptText);

                _tapes.Add(_tapeLabel, productStruct);

                _textbook.Clear();
                _routeBank.Clear();

                _tapeLabel = null;
            }
        }

        protected void BuildRoute(in List<F3NolanScriptBuilder.Line> lines, int routeLabel, int routeStart, int routeEnd, ref Dictionary<int, object> parts)
        {
            F3NolanRouteData.RouteKnot routeCache = new F3NolanRouteData.RouteKnot();

            if (parts.ContainsKey(routeLabel) && parts[routeLabel] is F3NolanRuleData rule)
            {
                if (rule.HasText)
                {
                    routeCache.Clear(rule.Text);               
                }
                else
                {
                    routeCache.Clear($"FAIL");
                }
            }
            else
            {
                routeCache.Clear($"FAIL");
            }

            List<F3NolanRouteData> results = new List<F3NolanRouteData>();

            for (int i = routeStart; i <= routeEnd; ++i)
            {
                var result = F3NolanDataRouteParser.Parse($"--{lines[i].Content}", ref routeCache, _textbook.AppendLine);

                if (result.Success)
                {
                    results.Add(result.Value);
                }
            }

            Dictionary<string, F3NolanRouteStruct> pendingDeadNodes = new Dictionary<string, F3NolanRouteStruct>();

            var initialKey = routeCache.GetShortName();

            F3NolanRouteStruct rootNode = new F3NolanRouteStruct(new string[] {initialKey});

            List<int> bankIndexes = FilterRoute(0, 0, results);

            if (_routeBank.ContainsKey(initialKey))
            {
                throw NolanException.ContextError($"Bank contains '{initialKey}' route already.", ENolanScriptContext.Route, ENolanScriptError.DuplicateKey);
            }

            if (bankIndexes.Count == 0 || results[0].Depth != 0)
            {
                throw NolanException.ContextError($"Route '{initialKey}' depth '{results[0].Depth}'.", ENolanScriptContext.Route, ENolanScriptError.OutOfRange);
            }

            foreach (int bankIndex in bankIndexes) // Add stitch struct into flow of the initial route struct
            {
                F3NolanRouteData flowCursor = results[bankIndex];
                F3NolanStitchStruct flowStitch = new F3NolanStitchStruct(flowCursor.ShortKey, flowCursor.Name);
                flowStitch.Context = flowCursor.ContextOrPayload;
                flowStitch.Cost = flowCursor.CostOrGain;

                if (BuildDeadEnds(bankIndex + 1, results, flowCursor, ref flowStitch, ref pendingDeadNodes))
                {
                    parts.Add(routeStart + bankIndex, pendingDeadNodes[flowCursor.Name]);
                }
                else // special use case with lonely answer line followed by dead-end
                {
                    F3NolanRouteData? flowEnds = FilterDeadEnds(bankIndex + 2, flowCursor.Depth, results);

                    if (flowEnds.HasValue)
                    {
                        flowStitch.Payload = flowEnds.Value.ContextOrPayload;
                        flowStitch.Gain = flowEnds.Value.CostOrGain;
                    }
                }

                rootNode.Flow.Add(flowStitch);
            }

            _routeBank.Add(initialKey, rootNode);

            for (int i = 1; i < results.Count; ++i)
            {
                if (results[i].IsChoiceLine)
                {
                    continue;
                }

                F3NolanRouteData routeCursor = results[i];
                if (routeCursor.Name.EndsWith("-0") == false)
                {
                    throw NolanException.ContextError($"Route '{initialKey}' name '{routeCursor.Name}' ending by '-0' required.", ENolanScriptContext.Route, ENolanScriptError.SyntaxError);
                }

                string routeName = routeCursor.Name.Substring(0, routeCursor.Name.Length - 2);
                List<string> routeKeys = new List<string>();
                routeKeys.AddRange(routeCursor.Keys);
                routeKeys.Add(routeName);
                F3NolanRouteStruct flowNode = new F3NolanRouteStruct(routeKeys.ToArray());

                bankIndexes = FilterRoute(i + 1, routeCursor.Depth + 1, results);

                if (bankIndexes.Count == 0)
                {
                    F3NolanRouteData? flowEnds = FilterDeadEnds(i + 1, routeCursor.Depth - 1, results);
                    if (flowEnds.HasValue)
                    {
                        flowNode.Goto = flowEnds.Value.Goto;
                    }
                }
                else
                {
                    foreach (int bankIndex in bankIndexes) // Add stitch struct into flow of each route struct (header lines)
                    {
                        F3NolanRouteData flowCursor = results[bankIndex];
                        F3NolanStitchStruct flowStitch = new F3NolanStitchStruct(flowCursor.ShortKey, flowCursor.Name);
                        flowStitch.Context = flowCursor.ContextOrPayload;
                        flowStitch.Cost = flowCursor.CostOrGain;

                        if (BuildDeadEnds(bankIndex + 1, results, flowCursor, ref flowStitch, ref pendingDeadNodes))
                        {
                            parts.Add(routeStart + bankIndex, pendingDeadNodes[flowCursor.Name]);
                        }
                        else // special use case with lonely answer line followed by dead-end
                        {
                            F3NolanRouteData? flowEnds = FilterDeadEnds(bankIndex + 2, flowCursor.Depth, results);

                            if (flowEnds.HasValue)
                            {
                                flowStitch.Payload = flowEnds.Value.ContextOrPayload;
                                flowStitch.Gain = flowEnds.Value.CostOrGain;
                            }
                        }

                        flowNode.Flow.Add(flowStitch);
                    }
                }

                _routeBank.Add(routeName, flowNode);

                parts.Add(routeStart + i - 1, rootNode);
            }

            foreach (var deadNode in pendingDeadNodes)
            {
                _routeBank.Add(deadNode.Key, deadNode.Value);
            }
        }

        static private bool BuildDeadEnds(int bankIndex, List<F3NolanRouteData> bankValues, F3NolanRouteData route, ref F3NolanStitchStruct stitch, ref Dictionary<string, F3NolanRouteStruct> pendingDeadEnds)
        {
            List<int> flowIndexes = FilterRoute(bankIndex, route.Depth + 1, bankValues);

            if (flowIndexes.Count == 0) // Add dead ends from stitch struct as route struct with payload and gain
            {
                List<string> deadKeys = new List<string>();

                F3NolanRouteData? flowEnds = FilterDeadEnds(bankIndex, route.Depth, bankValues);

                if (flowEnds.HasValue)
                {
                    deadKeys.AddRange(flowEnds.Value.Keys);

                    stitch.Payload = flowEnds.Value.ContextOrPayload;
                    stitch.Gain = flowEnds.Value.CostOrGain;
                }

                deadKeys.AddRange(route.Keys);

                F3NolanRouteStruct deadEnds = new F3NolanRouteStruct(deadKeys.ToArray());
                if (flowEnds.HasValue)
                {
                    deadEnds.Goto = flowEnds.Value.Goto;
                }

                pendingDeadEnds.Add(route.Name, deadEnds);
                return true;
            }

            return false;
        }

        private Dictionary<string, F3NolanRouteStruct> _routeBank = new Dictionary<string, F3NolanRouteStruct>();
        private F3NolanMutableTextBook _textbook = new F3NolanMutableTextBook();

        public string[] Keys
        {
            get { return _tapes.Keys.ToArray(); }
        }

        public F3NolanScriptData this[string name]
        {
            get
            {
                if (_tapes.ContainsKey(name) == false)
                {
                    throw NolanException.ContextError($"Tape '{name}' not found.", ENolanScriptContext.Knot, ENolanScriptError.KeyNotFound);
                }

                return _tapes[name];
            }
        }

        public string Last { get { return _tapes.Keys.Last(); } }

        static public bool Parse(string[] script, out List<F3NolanScriptBuilder.Line> lines)
        {
            lines = new List<F3NolanScriptBuilder.Line>();

            foreach (var line in script)
            {
                var nextLine = new F3NolanScriptBuilder.Line(line);

                if (nextLine.Delimiter != '~')
                {
                    lines.Add(nextLine);
                }
                else if (lines.Count > 0)
                {
                    var lastLine = lines.Last();
                    lines.RemoveAt(lines.Count - 1);

                    lastLine.Merge(nextLine);
                    lines.Add(lastLine);
                }
            }

            return lines.Count > 0;
        }

        static private List<int> FilterRoute(int inStart, int inDepth, List<F3NolanRouteData> inRoutes)
        {
            List<int> result = new List<int>();

            for (int i = inStart; i < inRoutes.Count; ++i)
            {
                F3NolanRouteData route = inRoutes[i];

                if (route.IsDeadEnds == false && route.Depth == inDepth)
                {
                    result.Add(i);
                }
                else if (route.Depth < inDepth)
                {
                    break;
                }
            }

            return result;
        }

        static private F3NolanRouteData? FilterDeadEnds(int index, int depth, List<F3NolanRouteData> routes)
        {
            if (index < routes.Count)
            {
                F3NolanRouteData route = routes[index];

                if (route.IsDeadEnds && route.Depth == depth)
                {
                    return route;
                }
            }

            return null;
        }

        public static Dictionary<string, KeyValuePair<string, F3NolanRuleMeta[]>> Compute(in string scene, in F3NolanStatData stat, in F3NolanRuleData[] rules)
        {
            List<F3NolanRuleData> rulebook = new List<F3NolanRuleData>();

            foreach (var rule in rules) // preprocessing sugar syntax like joker (*) and meter (.. ++ -- >= <=) for each rule
            {
                rule.ResolveSyntax(in stat, ref rulebook);
            }

            var result = new Dictionary<string, KeyValuePair<string, F3NolanRuleMeta[]>>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rulebook)
            {
                if (rule.Accept(scene, stat, out var meta))
                {
                    string name = rule.Description();

                    if (result.ContainsKey(name))
                    {
                        name = $"{name} duplicate";
                    }

                    result.Add(name, meta);
                }
            }

            return result;
        }

        public static string GetPrefixFromContext(ENolanScriptContext context)
        {
            switch (context)
            {
                case ENolanScriptContext.Rule:
                    return "++";
                case ENolanScriptContext.Route:
                    return "--";
                case ENolanScriptContext.Stat:
                    return "==";
                case ENolanScriptContext.Page:
                    return "**";
                case ENolanScriptContext.Goal:
                    return "??";
                default:
                    return "//";
            }
        }

        public static ENolanScriptContext GetContextFromPrefix(string prefix)
        {
            switch (prefix)
            {
                case "++":
                    return ENolanScriptContext.Rule;
                case "--":
                    return ENolanScriptContext.Route;
                case "==":
                    return ENolanScriptContext.Stat;
                case "**":
                    return ENolanScriptContext.Page;
                case "??":
                    return ENolanScriptContext.Goal;
                default:
                    return ENolanScriptContext.Text;
            }
        }
    }
}
