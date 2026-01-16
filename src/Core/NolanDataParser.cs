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
    /// <summary>
    /// A static utility class containing common Pidgin parser elements for the Nolan syntax.
    /// All shared and primitive parsers have been consolidated here to avoid duplication.
    /// </summary>
    public static class F3NolanDataScriptParser
    {
        // Helper to create a token parser that also consumes trailing whitespace.
        public static Parser<char, T> Token<T>(Parser<char, T> p) => Try(p).Before(SkipWhitespaces);
        public static Parser<char, char> Token(char value) => Token(Char(value));
        public static Parser<char, string> Token(string value) => Token(String(value));

        // --- Common Delimiter Parsers ---
        public static readonly Parser<char, char> LParenthesis = Token('(');
        public static readonly Parser<char, char> RParenthesis = Token(')');
        public static readonly Parser<char, char> LBrace = Token('{');
        public static readonly Parser<char, char> RBrace = Token('}');
        public static readonly Parser<char, char> LBracket = Token('[');
        public static readonly Parser<char, char> RBracket = Token(']');
        public static readonly Parser<char, char> Semicolon = Token(';');
        public static readonly Parser<char, char> Colon = Token(',');
        public static readonly Parser<char, char> Pipe = Token('|');
        public static readonly Parser<char, char> PipeSeq = Token('&');

        // --- Common Base Content Parsers ---

        // Parser for content that can be an identifier (alphanumeric, '.', '_', '+', '^', '@').
        public static readonly Parser<char, string> IdentifierContentParser =
            Token(LetterOrDigit.Or(OneOf('.', '_', '+', '*')).AtLeastOnceString())
            .Where(s => !string.IsNullOrWhiteSpace(s));

        // Parser for the optional <LOCATION> suffix.
        public static readonly Parser<char, string> LocationSuffixParser =
            from lt in Token('<')
            from locContent in IdentifierContentParser
            from gt in Token('>')
            select locContent;

        // Parser for a GameTag, which can have an optional '!' or '?' prefix and an optional location suffix.
        public static readonly Parser<char, F3NolanGameTag> GameTagParser =
            from prefix in OneOf('!', '?').Optional()
            from tagName in IdentifierContentParser
            from locationSuffix in Try(LocationSuffixParser).Optional() // Use Try() for optional suffix to allow backtracking.
            select new F3NolanGameTag(
                (prefix.HasValue ? prefix.Value.ToString() : "") +
                tagName +
                (locationSuffix.HasValue ? $"<{locationSuffix.Value}>" : "")
            );

        // Parser for a generic identifier string.
        public static readonly Parser<char, string> IdentifierParser = Token(LetterOrDigit.ManyString());

        // Parser for free-form sentence content.
        public static readonly Parser<char, string> SentenceContentParser = Token(OneOf(LetterOrDigit, Punctuation, Whitespace, Char('<'), Char('>'), Char('@'), Char('*'), Char('?'), Char('$'), Char('/')).ManyString());

        // A generic method to parse semicolon-separated tags into a F3NolanGameTagSet.
        // SUGGESTION: To make this cleaner, consider adding a static F3NolanGameTagSet.Empty property.
        public static Parser<char, F3NolanGameTagSet> SemicolonSeparatedTags(Parser<char, F3NolanGameTag> tagParser) =>
            tagParser.SeparatedAndOptionallyTerminated(Semicolon)
                     .Select(tags => new F3NolanGameTagSet(tags.ToList()));
        public static Parser<char, List<T>> TextLooping<T>(Parser<char, T> p) => p.Separated(Pipe).Select(x => x.ToList());
        public static Parser<char, List<T>> TextSequence<T>(Parser<char, T> p) => p.Separated(PipeSeq).Select(x => x.ToList());

        /// <summary>
        /// Vérifie si la ligne donnée commence par le délimiteur de règle (">>").
        /// </summary>
        public static bool IsDelimiter(string line) => line.StartsWith(">>");
    }

    /// <summary>
    /// A static class containing the simplified Pidgin parser for NOLAN rules.
    /// This class now exclusively uses the building blocks from F3NolanDataParser.
    /// </summary>
    public static class F3NolanDataRuleParser
    {
        // --- Intermediate Records for Readability ---
        // Represents the cost of a rule, which includes a set of tags and whether it's from a drag or drop action.
        private record ParsedRuleCost(F3NolanGameTagSet Cost, bool IsFromDrag);
        // Represents the optional text block associated with a rule.
        private record ParsedTextComponent(string Name, List<string> Lines);
        // A container for all components of a parsed rule, replacing the large Tuple.
        private record ParsedRuleComponent(
            F3NolanGameTagSet Context,
            F3NolanGameTag Match,
            F3NolanGameTagSet Payload,
            ParsedRuleCost CostInfo,
            F3NolanGameTagSet Gain,
            ParsedTextComponent? Text
        );

        // --- Parsers for Rule Components ---
        private static readonly Parser<char, F3NolanGameTagSet> SemicolonSeparatedGameTags = F3NolanDataScriptParser.SemicolonSeparatedTags(F3NolanDataScriptParser.GameTagParser);
        private static readonly Parser<char, F3NolanGameTagSet> PayloadParser = LParenthesis.Then(SemicolonSeparatedGameTags).Before(RParenthesis);
        private static readonly Parser<char, F3NolanGameTagSet> GainParser = LBrace.Then(SemicolonSeparatedGameTags).Before(RBrace);
        private static readonly Parser<char, F3NolanGameTag> MatchParser = LBracket.Then(F3NolanDataScriptParser.GameTagParser).Before(Token("]->"));

        // --- Cost Parsers ---
        private static readonly Parser<char, ParsedRuleCost> FromDragCostParser =
            LBrace.Then(SemicolonSeparatedGameTags).Before(Token("}-")).Select(cost => new ParsedRuleCost(cost, true));

        private static readonly Parser<char, ParsedRuleCost> FromDropCostParser =
            Token("-{").Then(SemicolonSeparatedGameTags).Before(RBrace).Select(cost => new ParsedRuleCost(cost, false));

        private static readonly Parser<char, ParsedRuleCost> EmptyCostParser =
            Token("-").Then(Return(new ParsedRuleCost(new F3NolanGameTagSet(new List<string>()), true)));

        private static readonly Parser<char, ParsedRuleCost> CostParser = FromDragCostParser.Or(FromDropCostParser).Or(EmptyCostParser);

        // --- Text Parser ---
        private static readonly Parser<char, ParsedTextComponent> EmbeddedTextParser =
            from name in F3NolanDataScriptParser.IdentifierParser
            from text in TextLooping(F3NolanDataScriptParser.SentenceContentParser)
            select new ParsedTextComponent(name, text);

        private static readonly Parser<char, ParsedTextComponent> TextParser = Token('#').Then(EmbeddedTextParser);

        // --- Main Rule Parser ---
        // This parser now assembles a clean ParsedRuleComponents record instead of a complex tuple.
        private static readonly Parser<char, ParsedRuleComponent> RuleParser =
            from delimiter in Token("++")
            from context in PayloadParser.Optional()
            from costInfo in CostParser
            from match in MatchParser
            from payload in PayloadParser.Optional()
            from gain in GainParser.Optional()
            from text in TextParser.Optional()
            select new ParsedRuleComponent(
                context.GetValueOrDefault(F3NolanGameTagSet.Empty),
                match,
                payload.GetValueOrDefault(F3NolanGameTagSet.Empty),
                costInfo,
                gain.GetValueOrDefault(F3NolanGameTagSet.Empty),
                text.HasValue ? text.Value : null
            );

        public static Result<char, F3NolanGameTagSet> ParseGameTagSet(string input, bool isPayload)
        {
            return isPayload ? PayloadParser.Parse(input) : GainParser.Parse(input);
        }

        /// <summary>
        /// Parses the input string into an F3NolanRuleData object.
        /// The logic is now much cleaner due to the use of the ParsedRuleComponents record.
        /// </summary>
        public static Result<char, F3NolanRuleData> Parse(string input, Func<string, string, string[]> add)
        {
            Result<char, ParsedRuleComponent> result = RuleParser.Parse(input);

            if (result.Success)
            {
                F3NolanTextData? textData = null;
                var resultValues = result.Value;

                if (resultValues.Text is not null)
                {
                    textData = new F3NolanTextData(new List<string> { resultValues.Text.Name }, resultValues.Text.Lines, add);
                }

                var resultStruct = new F3NolanRuleData(
                    resultValues.Match,
                    resultValues.Context,
                    resultValues.CostInfo.Cost,
                    resultValues.Payload,
                    resultValues.Gain,
                    resultValues.CostInfo.IsFromDrag,
                    textData
                );

                return new Result<char, F3NolanRuleData>(resultStruct);
            }

            return new Result<char, F3NolanRuleData>(result.Error ?? throw NolanException.ContextError("Internal Error", ENolanScriptContext.Rule));
        }

        /// <summary>
        /// Vérifie si la ligne donnée commence par le délimiteur de règle ("++").
        /// </summary>
        public static bool IsDelimiter(string line) => line.StartsWith("++");
    }

    /// <summary>
    /// Une classe statique contenant les parseurs Pidgin pour les états de statistique et de fin de partie Nolan.
    /// </summary>
    public static class F3NolanDataStatParser
    {
        private static readonly Parser<char, F3NolanGameTagSet> SemicolonSeparatedGameTags = F3NolanDataScriptParser.SemicolonSeparatedTags(F3NolanDataScriptParser.GameTagParser);

        /// <summary>
        /// Parseur pour un F3NolanLocation: name(tag1;tag2;...)
        /// </summary>
        private static readonly Parser<char, KeyValuePair<string, F3NolanGameTagSet>> LocationParser =
            from name in F3NolanDataScriptParser.LBracket.Then(F3NolanDataScriptParser.IdentifierContentParser).Before(F3NolanDataScriptParser.Colon)
            from tags in SemicolonSeparatedGameTags.Before(F3NolanDataScriptParser.RBracket)
            select new KeyValuePair<string, F3NolanGameTagSet>(name, tags);

        /// <summary>
        /// Parseur fusionné pour tout F3NolanStat (soit normal "==" soit fin de partie "=>").
        /// Il détermine le drapeau IsGameOver basé sur le préfixe correspondant.
        /// </summary>
        private static readonly Parser<char, F3NolanStatData> StatParser =
            // Tente d'abord de parser "=>", indiquant IsGameOver = true
            Try(Token("=>").Then(Return(true)))
            // Si "=>" échoue, tente de parser "==", indiquant IsGameOver = false
            .Or(Token("==").Then(Return(false)))
            // Puis parse les emplacements qui suivent
            .Then(isGameOver => LocationParser.Many().Select(locations => new F3NolanStatData(locations.ToArray())));

        /// <summary>
        /// Méthode publique pour analyser une chaîne d'entrée en un F3NolanStat.
        /// </summary>
        public static Result<char, F3NolanStatData> Parse(string input) => StatParser.Parse(input);

        /// <summary>
        /// Vérifie si la ligne donnée commence par un délimiteur de statistique ("==" ou "=>").
        /// </summary>
        public static bool IsDelimiter(string line) => line.StartsWith("==") || line.StartsWith("=>");
    }

    /// <summary>
    /// A static class for parsing Nolan text blocks ("##").
    /// </summary>
    public static class F3NolanDataTextParser
    {
        // Intermediate record for parsed text content, replacing KeyValuePair.
        private record ParsedTextContent(string Name, bool bLoop, List<string> Lines);
        private record ParsedBankContent(List<string> Names, List<string> Lines);

        // Parser for a single text entry: name|sentence1|sentence2
        private static readonly Parser<char, ParsedTextContent> TextLoopParser =
            from name in F3NolanDataScriptParser.IdentifierParser
            from sentences in TextLooping(F3NolanDataScriptParser.SentenceContentParser)
            select new ParsedTextContent(name, true, sentences);

        private static readonly Parser<char, ParsedTextContent> TextSequenceParser =
            from name in F3NolanDataScriptParser.IdentifierParser
            from sentences in TextSequence(F3NolanDataScriptParser.SentenceContentParser)
            select new ParsedTextContent(name, false, sentences);

        // Parser for a text bank: (name1|name2)|sentence1|sentence2
        private static readonly Parser<char, ParsedBankContent> TextBankParser =
            from names in F3NolanDataScriptParser.LParenthesis.Then(TextLooping(F3NolanDataScriptParser.IdentifierParser)).Before(F3NolanDataScriptParser.RParenthesis)
            from sentences in TextLooping(F3NolanDataScriptParser.SentenceContentParser)
            select new ParsedBankContent(names, sentences);

        // Main parser that handles either a single text or a text bank, prefixed with "##"
        private static readonly Parser<char, ParsedTextContent> NolanTextParser =
            Try(Token("##").Then(TextLoopParser.Or(TextSequenceParser)));

        public static Result<char, F3NolanTextData> Parse(string input, Func<string, string, string[]> add)
        {
            Result<char, ParsedTextContent> result = NolanTextParser.Parse(input); // TODO support ParsedBankContent

            if (result.Success)
            {
                bool bIsSingleLine = result.Value.Lines.Count() == 1;

                var resultLines = bIsSingleLine ? result.Value.Lines.First() : $"({(result.Value.bLoop ? "?" : "!")}<{string.Join("><", result.Value.Lines)}>)";
                var resultStruct = new F3NolanTextData(result.Value.Name, resultLines, add);

                return new Result<char, F3NolanTextData>(resultStruct);
            }

            return new Result<char, F3NolanTextData>(result.Error ?? throw NolanException.ContextError("Internal Error", ENolanScriptContext.Text));
        }

        public static bool IsDelimiter(string line) => line.StartsWith("##");
    }
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
