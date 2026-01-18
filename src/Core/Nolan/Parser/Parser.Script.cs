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
            Token(LetterOrDigit.Or(OneOf('.', '_', '-', '+', '*')).AtLeastOnceString())
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
}
