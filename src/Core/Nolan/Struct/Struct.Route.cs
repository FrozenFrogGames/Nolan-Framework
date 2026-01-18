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

                GotoName = string.IsNullOrWhiteSpace(resultLine) ? string.Empty : resultLine.TrimEnd();
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
}
