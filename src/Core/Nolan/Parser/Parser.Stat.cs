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
}
