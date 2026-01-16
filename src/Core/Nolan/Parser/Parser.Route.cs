using Pidgin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

using static FrozenFrogFramework.NolanTech.F3NolanDataScriptParser;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace FrozenFrogFramework.NolanTech
{
    public static class F3NolanDataRouteParser
    {
        /// <summary>
        /// Parser for the depth, represented by one or more hyphens.
        /// </summary>
        private static readonly Parser<char, int> _depthParser =
            Char('-').AtLeastOnceString().Select(s => s.Length);


        private static readonly Parser<char, IEnumerable<string>> _sentencesParser =
            F3NolanDataScriptParser.SentenceContentParser.Separated(F3NolanDataScriptParser.Pipe);

        /// <summary>
        /// Parser for a dead-end route, e.g., "--> text|more text"
        /// </summary>
        private static readonly Parser<char, (int Depth, IEnumerable<string> Lines)> _deadEndRouteParser =
            from depth in _depthParser
            from _ in Token('>')
            from sentences in _sentencesParser
            select (-1 - depth, sentences);

        /// <summary>
        /// Parser for a standard route, e.g., "-- text|more text".
        /// It must not be a dead-end route.
        /// </summary>
        private static readonly Parser<char, (int Depth, IEnumerable<string> Lines)> _standardRouteParser =
            from depth in _depthParser
            from _ in Lookahead(AnyCharExcept('>')) // Ensure it's not a dead-end marker right after the hyphens
            from sentences in _sentencesParser
            select (depth - 2, sentences);

        /// <summary>
        /// The main parser that combines the route types. It tries to parse a dead-end route first,
        /// then falls back to a standard route.
        /// </summary>
        private static readonly Parser<char, (int Depth, IEnumerable<string> Lines)> _routeParser =
            Try(_deadEndRouteParser).Or(_standardRouteParser);


        /// <summary>
        /// Parses a line of text representing a route.
        /// A route starts with one or more hyphens for depth. If followed by '>', it's a dead end.
        /// Example: "-- choice 1" or "--> dead end".
        /// </summary>
        /// <param name="input">The string to parse.</param>
        /// <param name="get">Function to retrieve lines from the textbook.</param>
        /// <param name="add">Function to add a line to the textbook.</param>
        /// <returns>A result containing the parsed F3NolanRouteData or an error.</returns>
        public static Result<char, F3NolanRouteData> Parse(string input, ref F3NolanRouteData.RouteKnot route, Func<string, string, string[]> add)
        {
            var result = _routeParser.Parse(input);
            if (result.Success)
            {
                bool bIsSingleLine = result.Value.Lines.Count() == 1;

                string resultLine = bIsSingleLine ? result.Value.Lines.First() : $"(?<{string.Join("><", result.Value.Lines)}>)";
                var resultStruct = new F3NolanRouteData(result.Value.Depth, resultLine, ref route, add);

                return new Result<char, F3NolanRouteData>(resultStruct);
            }

            return new Result<char, F3NolanRouteData>(result.Error ?? throw NolanException.ContextError("Internal Error", ENolanScriptContext.Route));
        }
        
        public static bool IsDelimiter(string line) => line.StartsWith("--") || line.StartsWith("->");
    }
}
