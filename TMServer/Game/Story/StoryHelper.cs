using System;
using System.Collections.Generic;
using System.Text;

namespace TMServer.Game.Story
{
    static class StoryHelper
    {
        private static readonly Random Dice = new Random();
        public static string[] Get(Func<int, string[][]> stories, int len, int mafias) => stories(mafias)[Dice.Next(len)];

        public static string Number(int n)
        {
            string result = string.Empty;

            if (n >= 20)
                switch (n - n % 10)
                {
                    case 20:
                        result += " twenty";
                        break;
                    case 30:
                        result += " thirty";
                        break;
                    case 40:
                        result += " forty";
                        break;
                    case 50:
                        result += " fifty";
                        break;
                    case 60:
                        result += " sixty";
                        break;
                    case 70:
                        result += " seventy";
                        break;
                    case 80:
                        result += " eighty";
                        break;
                    case 90:
                        result += " ninety";
                        break;
                }
            if (n >= 10)
                switch (n)
                {
                    case 10:
                        result += " ten";
                        break;
                    case 11:
                        result += " eleven";
                        break;
                    case 12:
                        result += " twelve";
                        break;
                    case 13:
                        result += " thirteen";
                        break;
                    case 14:
                        result += " fourteen";
                        break;
                    case 15:
                        result += " fifteen";
                        break;
                    case 16:
                        result += " sixteen";
                        break;
                    case 17:
                        result += " seventeen";
                        break;
                    case 18:
                        result += " eighteen";
                        break;
                    case 19:
                        result += " nineteen";
                        break;
                }
            else if (n % 10 > 0)
                switch (n)
                {
                    case 1:
                        result += " one";
                        break;
                    case 2:
                        result += " two";
                        break;
                    case 3:
                        result += " three";
                        break;
                    case 4:
                        result += " four";
                        break;
                    case 5:
                        result += " five";
                        break;
                    case 6:
                        result += " six";
                        break;
                    case 7:
                        result += " seven";
                        break;
                    case 8:
                        result += " eight";
                        break;
                    case 9:
                        result += " nine";
                        break;
                }

            return result.Trim();
        }
        
        public static string Suffix(string word, string suffix, int n) => n > 1 ? word + suffix : word;
        public static string SuffixS(string word, int n) => Suffix(word, "s", n);
    }
}
