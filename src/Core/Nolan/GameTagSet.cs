using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Text.Json.Serialization;

namespace FrozenFrogFramework.NolanTech
{
    /// <summary>
    /// Représente un ensemble de F3NolanGameTags.
    /// </summary>
    [JsonConverter(typeof(F3NolanGameTagSetConverter))] // Référence du convertisseur Json
    public class F3NolanGameTagSet : IEnumerable<F3NolanGameTag>, IEquatable<F3NolanGameTagSet>
    {
        public static F3NolanGameTagSet Empty => new F3NolanGameTagSet(Enumerable.Empty<string>());
        private readonly List<F3NolanGameTag> _tags;

        /// <summary>
        /// Obtient le nombre de tags dans l'ensemble.
        /// </summary>
        public int Count => _tags.Count;

        /// <summary>
        /// Obtient le tag à l'index spécifié.
        /// </summary>
        /// <param name="index">L'index du tag.</param>
        /// <returns>Le F3NolanGameTag à l'index spécifié.</returns>
        public F3NolanGameTag this[int index] => _tags[index];
        public F3NolanGameTag[] Tags => _tags.ToArray();

        /// <summary>
        /// Constructeur pour F3NolanGameTagSet.
        /// </summary>
        /// <param name="tags">Une collection de tags à inclure dans l'ensemble.</param>
        public F3NolanGameTagSet(IEnumerable<F3NolanGameTag> tags)
        {
            _tags = tags.OrderBy(t => t.RawValue).ToList(); // Trie les tags pour une comparaison cohérente
        }

        /// <summary>
        /// Constructeur pour F3NolanGameTagSet à partir d'une liste de chaînes de caractères.
        /// </summary>
        /// <param name="tagStrings">Une collection de chaînes de caractères de tags.</param>
        public F3NolanGameTagSet(IEnumerable<string> tagStrings)
        {
            _tags = tagStrings.Select(s => new F3NolanGameTag(s)).OrderBy(t => t.RawValue).ToList();
        }

        /// <summary>
        /// Vérifie si l'ensemble contient un tag spécifique.
        /// </summary>
        /// <param name="tag">Le tag à vérifier.</param>
        /// <returns>Vrai si le tag est présent, faux sinon.</returns>
        public bool Contains(F3NolanGameTag tag)
        {
            return _tags.Contains(tag);
        }

        /// <summary>
        /// Vérifie si l'ensemble contient un tag avec la valeur brute spécifiée.
        /// </summary>
        /// <param name="rawValue">La valeur brute du tag à vérifier.</param>
        /// <returns>Vrai si un tag avec la valeur brute est présent, faux sinon.</returns>
        public bool Contains(string rawValue)
        {
            return _tags.Any(t => t.RawValue == rawValue);
        }
        public List<string> ToList()
        {
            return _tags.Select(t => t.ToString()).OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Checks if the tag set is empty.
        /// </summary>
        public bool Any()
        {
            return _tags.Any();
        }

        public IEnumerator<F3NolanGameTag> GetEnumerator()
        {
            return _tags.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Retourne une chaîne de caractères des tags de l'ensemble séparés par des points-virgules, triés alphabétiquement.
        /// </summary>
        /// <returns>Une représentation textuelle de l'ensemble de tags.</returns>
        public override string ToString()
        {
            return string.Join(";", ToList());
        }

        public override bool Equals(object? obj)
        {
            return obj is F3NolanGameTagSet set && Equals(set);
        }

        public bool Equals(F3NolanGameTagSet? other)
        {
            if (other is null || Count != other.Count) return false;

            var sortedThis = ToList();
            var sortedOther = other.ToList();

            for (int i = 0; i < Count; i++)
            {
                if (sortedThis[i] != sortedOther[i]) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                // Utilise une liste triée de chaînes pour un calcul de code de hachage cohérent
                foreach (var tagString in ToList())
                {
                    hash = hash * 23 + tagString.GetHashCode();
                }
                return hash;
            }
        }

        public bool ContainsSugarSyntax()
        {
            return _tags.Any(t => t.ContainsSugarSyntax());
        }

        public IEnumerable<F3NolanGameTagSet> ResolveSyntax(F3NolanGameTagSet tags)
        {
            foreach (var tag in _tags)
            {
                if (tag.ContainsSugarSyntax())
                {
                    foreach (F3NolanGameTag resolvedTag in tag.ResolveSyntax(tags))
                    {
                        F3NolanGameTagSet resultSet = ReplaceTag(tag, resolvedTag, _tags);

                        if (resultSet.ContainsSugarSyntax()) // recursive call for each tag that contains sugar syntax
                        {
                            foreach (var resolvedSet in resultSet.ResolveSyntax(tags))
                            {
                                yield return resolvedSet;
                            }
                        }
                        else
                        {
                            yield return resultSet;
                        }
                    }

                    break; // each other tags with sugar syntax will be resolved in recursive call
                }
            }
        }

        public static F3NolanGameTagSet ReplaceTag(F3NolanGameTag oldTag, F3NolanGameTag newTag, List<F3NolanGameTag> tagSet)
        {
            List<F3NolanGameTag> result = tagSet.Where(t => t.Equals(oldTag) == false).ToList();
            result.Add(newTag);
            return new F3NolanGameTagSet(result);
        }

        public static F3NolanGameTagSet ConcatSet(F3NolanGameTag match, F3NolanGameTagSet context, F3NolanGameTagSet cost)
        {
            List<F3NolanGameTag> tags = new List<F3NolanGameTag>() { match };
            tags.AddRange(context);
            tags.AddRange(cost);
            return new F3NolanGameTagSet(tags);
        }
    }
}
