using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using static Wox.Infrastructure.StringMatcher;

namespace Wox.Infrastructure
{
    public static class StringMatcher
    {
        public static MatchOption DefaultMatchOption = new MatchOption();

        public static int UserSettingSearchPrecision { get; set; }

        public static bool ShouldUsePinyin { get; set; }

        [Obsolete("This method is obsolete and should not be used. Please use the static function StringMatcher.FuzzySearch")]
        public static int Score(string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                return FuzzySearch(target, source, DefaultMatchOption).Score;
            }
            else
            {
                return 0;
            }
        }

        [Obsolete("This method is obsolete and should not be used. Please use the static function StringMatcher.FuzzySearch")]
        public static bool IsMatch(string source, string target)
        {
            return FuzzySearch(target, source, DefaultMatchOption).Score > 0;
        }

        public static MatchResult FuzzySearch(string query, string stringToCompare)
        {
            return FuzzySearch(query, stringToCompare, DefaultMatchOption);
        }

        /// <summary>
        /// refer to https://github.com/mattyork/fuzzy
        /// </summary>
        public static MatchResult FuzzySearch(string query, string stringToCompare, MatchOption opt)
        {
            if (string.IsNullOrEmpty(stringToCompare) || string.IsNullOrEmpty(query)) return new MatchResult { Success = false };
            
            query = query.Trim();

            var fullStringToCompareWithoutCase = opt.IgnoreCase ? stringToCompare.ToLower() : stringToCompare;

            var queryWithoutCase = opt.IgnoreCase ? query.ToLower() : query;

            int currentQueryToCompareIndex = 0;
            var queryToCompareSeparated = queryWithoutCase.Split(' ');
            var currentQueryToCompare = queryToCompareSeparated[currentQueryToCompareIndex];

            var patternIndex = 0;
            var firstMatchIndex = -1;
            var firstMatchIndexInWord = -1;
            var lastMatchIndex = 0;
            bool allMatched = false;
            bool isFullWordMatched = false;
            bool allWordsFullyMatched = true;

            var indexList = new List<int>();

            for (var index = 0; index < fullStringToCompareWithoutCase.Length; index++)
            {
                var ch = stringToCompare[index];
                if (fullStringToCompareWithoutCase[index] == currentQueryToCompare[patternIndex])
                {
                    if (firstMatchIndex < 0)
                    { // first matched char will become the start of the compared string
                        firstMatchIndex = index;
                    }

                    if (patternIndex == 0)
                    { // first letter of current word
                        isFullWordMatched = true;
                        firstMatchIndexInWord = index;
                    }
                    else if (!isFullWordMatched)
                    { // we want to verify that there is not a better match if this is not a full word
                        // in order to do so we need to verify all previous chars are part of the pattern
                        int startIndexToVerify = index - patternIndex;
                        bool allMatch = true;
                        for (int indexToCheck = 0; indexToCheck < patternIndex; indexToCheck++)
                        {   
                            if (fullStringToCompareWithoutCase[startIndexToVerify + indexToCheck] !=
                                currentQueryToCompare[indexToCheck])
                            {
                                allMatch = false;
                            }
                        }

                        if (allMatch)
                        { // update to this as a full word
                            isFullWordMatched = true;
                            if (currentQueryToCompareIndex == 0)
                            { // first word so we need to update start index
                                firstMatchIndex = startIndexToVerify;
                            }

                            indexList.RemoveAll(x => x >= firstMatchIndexInWord);
                            for (int indexToCheck = 0; indexToCheck < patternIndex; indexToCheck++)
                            { // update the index list
                                indexList.Add(startIndexToVerify + indexToCheck);
                            }
                        }
                    }

                    lastMatchIndex = index + 1;
                    indexList.Add(index);

                    // increase the pattern matched index and check if everything was matched
                    if (++patternIndex == currentQueryToCompare.Length)
                    {
                        if (++currentQueryToCompareIndex >= queryToCompareSeparated.Length)
                        { // moved over all the words
                            allMatched = true;
                            break;
                        }

                        // otherwise move to the next word
                        currentQueryToCompare = queryToCompareSeparated[currentQueryToCompareIndex];
                        patternIndex = 0;
                        if (!isFullWordMatched)
                        { // if any of the words was not fully matched all are not fully matched
                            allWordsFullyMatched = false;
                        }
                    }
                }
                else
                {
                    isFullWordMatched = false;
                }
            }


            // return rendered string if we have a match for every char or all substring without whitespaces matched
            if (allMatched)
            {
                // check if all query string was contained in string to compare
                bool containedFully = lastMatchIndex - firstMatchIndex == queryWithoutCase.Length;
                var score = CalculateSearchScore(query, stringToCompare, firstMatchIndex, lastMatchIndex - firstMatchIndex, containedFully, allWordsFullyMatched);
                var pinyinScore = ScoreForPinyin(stringToCompare, query);

                var result = new MatchResult
                {
                    Success = true,
                    MatchData = indexList,
                    RawScore = Math.Max(score, pinyinScore)
                };

                return result;
            }

            return new MatchResult { Success = false };
        }

        private static int CalculateSearchScore(string query, string stringToCompare, int firstIndex, int matchLen,
            bool isFullyContained, bool allWordsFullyMatched)
        {
            // A match found near the beginning of a string is scored more than a match found near the end
            // A match is scored more if the characters in the patterns are closer to each other, 
            // while the score is lower if they are more spread out
            var score = 100 * (query.Length + 1) / ((1 + firstIndex) + (matchLen + 1));

            // A match with less characters assigning more weights
            if (stringToCompare.Length - query.Length < 5)
            {
                score += 20;
            }
            else if (stringToCompare.Length - query.Length < 10)
            {
                score += 10;
            }

            if (isFullyContained)
            {
                score += 20; // honestly I'm not sure what would be a good number here or should it factor the size of the pattern
            }

            if (allWordsFullyMatched)
            {
                score += 20;
            }

            return score;
        }

        public enum SearchPrecisionScore
        {
            Regular = 50,
            Low = 20,
            None = 0
        }

        public static int ScoreForPinyin(string source, string target)
        {
            if (!ShouldUsePinyin)
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                if (Alphabet.ContainsChinese(source))
                {
                    var combination = Alphabet.PinyinComination(source);
                    var pinyinScore = combination
                        .Select(pinyin => FuzzySearch(target, string.Join("", pinyin)).Score)
                        .Max();
                    var acronymScore = combination.Select(Alphabet.Acronym)
                        .Select(pinyin => FuzzySearch(target, pinyin).Score)
                        .Max();
                    var score = Math.Max(pinyinScore, acronymScore);
                    return score;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }
    }

    public class MatchResult
    {
        public bool Success { get; set; }

        /// <summary>
        /// The final score of the match result with all search precision filters applied.
        /// </summary>
        public int Score { get; private set; }

        /// <summary>
        /// The raw calculated search score without any search precision filtering applied.
        /// </summary>
        private int _rawScore;

        public int RawScore
        {
            get { return _rawScore; }
            set
            {
                _rawScore = value;
                Score = ApplySearchPrecisionFilter(_rawScore);
            }
        }

        /// <summary>
        /// Matched data to highlight.
        /// </summary>
        public List<int> MatchData { get; set; }

        public bool IsSearchPrecisionScoreMet()
        {
            return IsSearchPrecisionScoreMet(Score);
        }

        private bool IsSearchPrecisionScoreMet(int score)
        {
            return score >= UserSettingSearchPrecision;
        }

        private int ApplySearchPrecisionFilter(int score)
        {
            return IsSearchPrecisionScoreMet(score) ? score : 0;
        }
    }

    public class MatchOption
    {
        /// <summary>
        /// prefix of match char, use for hightlight
        /// </summary>
        [Obsolete("this is never used")]
        public string Prefix { get; set; } = "";

        /// <summary>
        /// suffix of match char, use for hightlight
        /// </summary>
        [Obsolete("this is never used")]
        public string Suffix { get; set; } = "";

        public bool IgnoreCase { get; set; } = true;
    }
}