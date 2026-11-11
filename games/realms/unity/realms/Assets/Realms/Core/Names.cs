using System;

namespace Realms.Core
{
    /// <summary>Names shown in the game (Russian: the players are a father and his son).</summary>
    public static class Names
    {
        /// <summary>
        /// How the view shows a name: the rules keep the Russian name as the name's key, so a save
        /// opens in either language, and the view's language gives the word shown (the interface
        /// plan's 3.6). The view sets it; the rules alone show the key.
        /// </summary>
        public static Func<string, string> Shown = name => name;

        /// <summary>The start of a town's name when the names ran out: «Город 47».</summary>
        public const string NumberedTown = "Город ";

        public static string Numbered(int n) => NumberedTown + n;

        /// <summary>
        /// A kingdom's name by its colour, the field of its flag, in the tinctures' order
        /// (<see cref="Flags"/>): white, yellow, orange, red, violet, blue, sky, green, brown,
        /// black. The shades live in the view (FlagArt).
        /// </summary>
        public static readonly string[] Kingdoms =
        {
            "Серебряное княжество",
            "Солнечная держава",
            "Янтарный берег",
            "Алое королевство",
            "Фиалковые земли",
            "Лазурное царство",
            "Небесный удел",
            "Изумрудный край",
            "Каштановое графство",
            "Ночное королевство",
        };

        public static readonly string[] Cities =
        {
            "Златоград", "Белогорье", "Ясноград", "Светлояр", "Медовар", "Дубравинск",
            "Тихоречье", "Лисья Горка", "Камнеград", "Солнцедар", "Звездополь", "Ветроград",
            "Синеводск", "Хлебодар", "Ручейки", "Сосновка", "Луговое", "Высокий Холм",
            "Каменный Брод", "Яблоневка", "Озёрск", "Ромашково", "Мельничный", "Пряничный Двор",
            "Орлиное Гнездо", "Жемчужный", "Берёзовка", "Рыбачий", "Гусиный Луг", "Вишнёвый Сад",
            "Серебряный Ключ", "Кленовый Двор", "Медвежий Угол", "Ласточкино", "Бережок",
            "Тёплый Ручей", "Малиновка", "Лунный Причал", "Дальний Мыс", "Зелёный Дол",
            "Белые Камни", "Совиный Бор", "Пчелиный Двор", "Овсянка", "Радужный", "Новоград",
        };

        /// <summary>The kingdoms' names in English, one for each of <see cref="Kingdoms"/>.</summary>
        public static readonly string[] KingdomsEnglish =
        {
            "Silver Principality",
            "Sun Dominion",
            "Amber Coast",
            "Scarlet Kingdom",
            "Violet Lands",
            "Azure Realm",
            "Sky Domain",
            "Emerald March",
            "Chestnut County",
            "Night Kingdom",
        };

        /// <summary>The towns' names in English, one for each of <see cref="Cities"/>: of the same spirit, not spelled out.</summary>
        public static readonly string[] CitiesEnglish =
        {
            "Goldburg", "Whitehills", "Brightburg", "Lightmere", "Meadbrew", "Oakwood",
            "Stillriver", "Fox Hill", "Stoneburg", "Sungift", "Starfield", "Windburg",
            "Bluewater", "Breadgift", "Brooklets", "Pinewood", "Meadowby", "High Hill",
            "Stoneford", "Appleton", "Lakeby", "Daisyfield", "Millbrook", "Gingerbread Court",
            "Eagle's Nest", "Pearlhaven", "Birchwood", "Fisher's Rest", "Goose Meadow", "Cherry Orchard",
            "Silverspring", "Maple Court", "Bear's Corner", "Swallowby", "Bankside",
            "Warm Brook", "Raspberry Dell", "Moon Quay", "Far Cape", "Green Dale",
            "White Stones", "Owlwood", "Beeyard", "Oatfield", "Rainbow Vale", "Newburg",
        };

        /// <summary>The heroes' names in English, one for each of <see cref="HeroRules.Names"/>: their own, in Latin letters.</summary>
        public static readonly string[] HeroesEnglish =
        {
            "Ratibor", "Vseslava", "Dobrynya", "Milana", "Svetozar", "Yaroslava", "Miroslav", "Zabava",
            "Velimir", "Lyubava", "Bronislav", "Ognyana",
        };
    }
}
