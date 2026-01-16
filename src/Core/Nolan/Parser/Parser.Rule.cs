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
}
