using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>
    /// Names shown in the game (Russian: the players are a father and his son). They are neutral,
    /// of no one people: kingdoms by their colour, towns by what is around them, heroes by short
    /// names that read the same in both languages. The players asked for these instead of the
    /// Slavic ones on 2026-11-12.
    /// </summary>
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
            "Серебряное герцогство",
            "Солнечная долина",
            "Янтарный берег",
            "Алое королевство",
            "Фиалковые земли",
            "Лазурная гавань",
            "Небесное нагорье",
            "Изумрудный край",
            "Каштановое графство",
            "Ночное королевство",
        };

        public static readonly string[] Cities =
        {
            "Золотой Колодец", "Белые Утёсы", "Ясная Вершина", "Светлое Озеро", "Медовый Луг", "Дубовая Роща",
            "Тихая Река", "Лисья Горка", "Каменные Ворота", "Солнечная Поляна", "Звёздный Мост", "Ветреный Холм",
            "Синий Залив", "Пшеничные Холмы", "Три Ручья", "Сосновый Склон", "Широкий Луг", "Высокий Холм",
            "Каменный Брод", "Яблоневый Сад", "Озёрная Пристань", "Ромашковое Поле", "Старая Мельница", "Пряничный Двор",
            "Орлиное Гнездо", "Жемчужная Бухта", "Берёзовая Роща", "Рыбачий Мыс", "Гусиный Луг", "Вишнёвый Сад",
            "Серебряный Ключ", "Кленовый Двор", "Медвежий Угол", "Ласточкин Утёс", "Песчаный Берег",
            "Тёплый Ручей", "Малиновая Поляна", "Лунный Причал", "Дальний Мыс", "Зелёный Дол",
            "Белые Камни", "Совиный Бор", "Пчелиный Двор", "Овсяное Поле", "Радужный Водопад", "Новая Башня",
        };

        /// <summary>The kingdoms' names in English, one for each of <see cref="Kingdoms"/>.</summary>
        public static readonly string[] KingdomsEnglish =
        {
            "Silver Duchy",
            "Sun Vale",
            "Amber Coast",
            "Scarlet Kingdom",
            "Violet Lands",
            "Azure Haven",
            "Sky Highlands",
            "Emerald March",
            "Chestnut County",
            "Night Kingdom",
        };

        /// <summary>The towns' names in English, one for each of <see cref="Cities"/>: what they say, not spelled out.</summary>
        public static readonly string[] CitiesEnglish =
        {
            "Goldwell", "White Cliffs", "Bright Peak", "Clear Lake", "Honey Meadow", "Oak Grove",
            "Quiet River", "Fox Hill", "Stone Gate", "Sunny Glade", "Star Bridge", "Windy Hill",
            "Blue Bay", "Wheat Hills", "Three Brooks", "Pine Slope", "Broad Meadow", "High Hill",
            "Stoneford", "Apple Orchard", "Lake Landing", "Daisy Field", "Old Mill", "Gingerbread Court",
            "Eagle's Nest", "Pearl Cove", "Birch Grove", "Fisher's Point", "Goose Meadow", "Cherry Orchard",
            "Silverspring", "Maple Court", "Bear's Corner", "Swallow Cliff", "Sandy Shore",
            "Warm Brook", "Raspberry Glade", "Moon Quay", "Far Cape", "Green Dale",
            "White Stones", "Owlwood", "Beeyard", "Oat Field", "Rainbow Falls", "New Tower",
        };

        /// <summary>The heroes' names in English, one for each of <see cref="HeroRules.Names"/>: the same names in Latin letters.</summary>
        public static readonly string[] HeroesEnglish =
        {
            "Aren", "Lina", "Theo", "Mira", "Kai", "Nora", "Felix", "Ida", "Leo", "Ella", "Finn", "Iris",
        };

        /// <summary>
        /// The Slavic names the game gave before 2026-11-12 and the names that took their place, so a
        /// save written before then opens with the new ones (a name the players typed stays as typed).
        /// </summary>
        static readonly Dictionary<string, string> Former = new Dictionary<string, string>
        {
            ["Серебряное княжество"] = "Серебряное герцогство",
            ["Солнечная держава"] = "Солнечная долина",
            ["Лазурное царство"] = "Лазурная гавань",
            ["Небесный удел"] = "Небесное нагорье",

            ["Златоград"] = "Золотой Колодец", ["Белогорье"] = "Белые Утёсы", ["Ясноград"] = "Ясная Вершина",
            ["Светлояр"] = "Светлое Озеро", ["Медовар"] = "Медовый Луг", ["Дубравинск"] = "Дубовая Роща",
            ["Тихоречье"] = "Тихая Река", ["Камнеград"] = "Каменные Ворота", ["Солнцедар"] = "Солнечная Поляна",
            ["Звездополь"] = "Звёздный Мост", ["Ветроград"] = "Ветреный Холм", ["Синеводск"] = "Синий Залив",
            ["Хлебодар"] = "Пшеничные Холмы", ["Ручейки"] = "Три Ручья", ["Сосновка"] = "Сосновый Склон",
            ["Луговое"] = "Широкий Луг", ["Яблоневка"] = "Яблоневый Сад", ["Озёрск"] = "Озёрная Пристань",
            ["Ромашково"] = "Ромашковое Поле", ["Мельничный"] = "Старая Мельница", ["Жемчужный"] = "Жемчужная Бухта",
            ["Берёзовка"] = "Берёзовая Роща", ["Рыбачий"] = "Рыбачий Мыс", ["Ласточкино"] = "Ласточкин Утёс",
            ["Бережок"] = "Песчаный Берег", ["Малиновка"] = "Малиновая Поляна", ["Овсянка"] = "Овсяное Поле",
            ["Радужный"] = "Радужный Водопад", ["Новоград"] = "Новая Башня",

            ["Ратибор"] = "Арен", ["Всеслава"] = "Лина", ["Добрыня"] = "Тео", ["Милана"] = "Мира",
            ["Светозар"] = "Кай", ["Ярослава"] = "Нора", ["Мирослав"] = "Феликс", ["Забава"] = "Ида",
            ["Велимир"] = "Лео", ["Любава"] = "Элла", ["Бронислав"] = "Финн", ["Огняна"] = "Ирис",
        };

        /// <summary>The name a save's <paramref name="name"/> has now: the new one for a former name, else itself.</summary>
        public static string Current(string name) =>
            name != null && Former.TryGetValue(name, out string now) ? now : name;

        /// <summary>The former names and the ones that took their place, for the tests.</summary>
        public static IReadOnlyDictionary<string, string> Renamed => Former;
    }
}
