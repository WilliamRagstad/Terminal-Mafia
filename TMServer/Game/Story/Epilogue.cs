using System;
using System.Collections.Generic;
using System.Text;

namespace TMServer.Game.Story
{
    public static class Epilogue
    {
        private static string[][] StoriesMafia(int mafias) => new[]
        {
            new[]
            {
                "Once upon a time, there was a test.",
                "It went along just like this."
            },
            new[]
            {
                "It was a dark and stormy night, and the members of the Jones family were gathered together on a camping trip.",
                "That night as everyone gathered around to roast hot dogs and tell ghost stories,",
                "the clouds gathered and lighting flashed ahead. Spirits were high around the campfire,",
                "though, but no one knew that someone in the party had evil intentions...",
                "",
                "So everyone finished eating their hot dogs and returned to their tents to sleep."
            }
        };
        private static string[][] StoriesVillagers(int mafias) => new[]
        {
            new[]
            {
                "Once upon a time, there was a test.",
                "It went along just like this."
            },
            new[]
            {
                "It was a dark and stormy night, and the members of the Jones family were gathered together on a camping trip.",
                "That night as everyone gathered around to roast hot dogs and tell ghost stories,",
                "the clouds gathered and lighting flashed ahead. Spirits were high around the campfire,",
                "though, but no one knew that someone in the party had evil intentions...",
                "",
                "So everyone finished eating their hot dogs and returned to their tents to sleep."
            }
        };

        public static string[] Get(GameState state, int mafias) => state == GameState.MafiaWon ?
            StoryHelper.Get(StoriesMafia, 2, mafias) :
            StoryHelper.Get(StoriesVillagers, 2, mafias);
    }
}
