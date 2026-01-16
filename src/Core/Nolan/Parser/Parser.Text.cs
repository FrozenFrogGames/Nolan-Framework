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
}
