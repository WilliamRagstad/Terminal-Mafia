using System;
using System.Collections.Generic;
using System.Text;

namespace TMServer.Game.Story.Night
{
    public static class Mafia
    {
        private static string[][] Stories(int mafias) => new[]
        {
            new[]
            {
                "But late that night,",
                $"{StoryHelper.Number(mafias)} of the {StoryHelper.SuffixS("mafia", mafias)} woke up with a dark and evil plan."
            }
        };

        public static string[] Get(int mafias) => StoryHelper.Get(Stories, 1, mafias);
    }
}
