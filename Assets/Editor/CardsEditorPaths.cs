using System.IO;

namespace Flippy.CardDuelMobile.EditorTools
{
    internal static class CardsEditorPaths
    {
        public const string Root = "Assets/CardDuelMobile";
        public const string Generated = Root + "/Generated";
        public const string Data = Generated + "/Data";
        public const string Cards = Data + "/Cards";
        public const string Abilities = Data + "/Abilities";
        public const string Effects = Data + "/Effects";
        public const string Selectors = Data + "/Selectors";
        public const string Decks = Data + "/Decks";
        public const string Rules = Data + "/Rules";
        public const string Visuals = Data + "/Visuals";
        public const string Config = Data + "/Config";
        public const string Prefabs = Generated + "/Prefabs";
        public const string Scenes = Generated + "/Scenes";
        public const string Materials = Generated + "/Materials";

        public static string[] AllFolders => new[]
        {
            Generated, Data, Cards, Abilities, Effects, Selectors, Decks, Rules, Visuals, Config, Prefabs, Scenes, Materials
        };
    }
}
