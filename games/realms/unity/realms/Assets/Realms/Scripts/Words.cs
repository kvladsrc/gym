using Realms.Core;
using static Realms.Lang;

namespace Realms
{
    /// <summary>Russian words for the interface.</summary>
    public static class Words
    {
        /// <summary>The kind of land or water, with "у реки" where a river runs along the cell.</summary>
        public static string Biome(Cell cell)
        {
            if (cell.IsWater && cell.Lake)
                return T("Озеро", "Lake");
            string biome = BiomeName(cell.Biome);
            return cell.IsLand && cell.Rivers != 0 ? biome + T(" у реки", " by a river") : biome;
        }

        static string BiomeName(Core.Biome biome)
        {
            switch (biome)
            {
                case Core.Biome.Ocean: return T("Открытое море", "Open sea");
                case Core.Biome.Shallows: return T("Мелководье", "Shallows");
                case Core.Biome.Beach: return T("Песчаный берег", "Sandy shore");
                case Core.Biome.Meadow: return T("Луг", "Meadow");
                case Core.Biome.Plains: return T("Степь", "Steppe");
                case Core.Biome.Forest: return T("Лес", "Forest");
                case Core.Biome.Hills: return T("Холмы", "Hills");
                case Core.Biome.Mountains: return T("Горы", "Mountains");
                default: return "";
            }
        }

        public static string Resource(Resource resource)
        {
            switch (resource)
            {
                case Core.Resource.Wheat: return T("Пшеница", "Wheat");
                case Core.Resource.Wood: return T("Древесина", "Timber");
                case Core.Resource.Stone: return T("Камень", "Stone");
                case Core.Resource.Gold: return T("Золото", "Gold");
                case Core.Resource.Fish: return T("Рыба", "Fish");
                case Core.Resource.Sheep: return T("Овцы", "Sheep");
                default: return "";
            }
        }

        /// <summary>The icon of a resource in the art library.</summary>
        public static string ResourceIcon(Resource resource)
        {
            switch (resource)
            {
                case Core.Resource.Wheat: return "wheat";
                case Core.Resource.Wood: return "logs";
                case Core.Resource.Stone: return "stone";
                case Core.Resource.Gold: return "ingot";
                case Core.Resource.Fish: return "fish";
                case Core.Resource.Sheep: return "sheep";
                default: return null;
            }
        }

        /// <summary>The title of a monster group: its kind, or its den for a lair.</summary>
        public static string Monster(Monster monster, bool lair = false)
        {
            if (lair)
            {
                switch (monster)
                {
                    case Core.Monster.Troll: return T("Логово тролля", "Troll's lair");
                    case Core.Monster.Wolves: return T("Волчье логово", "Wolves' den");
                    case Core.Monster.Boar: return T("Логово кабанов", "Boars' lair");
                }
            }
            switch (monster)
            {
                case Core.Monster.Troll: return T("Тролли", "Trolls");
                case Core.Monster.Wolves: return T("Волки", "Wolves");
                case Core.Monster.Boar: return T("Кабаны", "Boars");
                case Core.Monster.Goblins: return T("Гоблины", "Goblins");
                case Core.Monster.Bandits: return T("Разбойники", "Bandits");
                case Core.Monster.Skeletons: return T("Скелеты", "Skeletons");
                case Core.Monster.Spiders: return T("Пауки", "Spiders");
                default: return "";
            }
        }

        /// <summary>What is special about the monsters, for the info card.</summary>
        public static string MonsterHint(Monster monster)
        {
            switch (monster)
            {
                case Core.Monster.Troll: return T("Огромный и живучий: каждый раунд залечивает раны.", "Huge and tough: heals its wounds every round.");
                case Core.Monster.Wolves: return T("Быстрые: кусают, и никто не успевает дать сдачи.", "Fast: they bite, and no one has time to strike back.");
                case Core.Monster.Boar: return T("Злые кабаны с острыми клыками.", "Angry boars with sharp tusks.");
                case Core.Monster.Goblins: return T("Мелкие и слабые, но их много.", "Small and weak, but there are many of them.");
                case Core.Monster.Bandits: return T("Стреляют из луков издалека.", "They shoot their bows from afar.");
                case Core.Monster.Skeletons: return T("Стрелы почти не берут кости: лучники бьют вполсилы.", "Arrows barely hurt bones: archers strike at half strength.");
                case Core.Monster.Spiders: return T("Кого укусят, тот запутается в паутине и не сойдёт с места.", "Whoever they bite gets caught in a web and can't move.");
                default: return "";
            }
        }

        /// <summary>"12 копейщиков": a count with the unit's word.</summary>
        public static string Count(Unit unit, int count)
        {
            switch (unit)
            {
                case Core.Unit.Spearmen: return T($"{count} {Plural(count, "копейщик", "копейщика", "копейщиков")}", $"{count} {En(count, "spearman", "spearmen")}");
                case Core.Unit.Archers: return T($"{count} {Plural(count, "лучник", "лучника", "лучников")}", $"{count} {En(count, "archer", "archers")}");
                case Core.Unit.Riders: return T($"{count} {Plural(count, "всадник", "всадника", "всадников")}", $"{count} {En(count, "rider", "riders")}");
                case Core.Unit.Goblins: return T($"{count} {Plural(count, "гоблин", "гоблина", "гоблинов")}", $"{count} {En(count, "goblin", "goblins")}");
                case Core.Unit.Boars: return T($"{count} {Plural(count, "кабан", "кабана", "кабанов")}", $"{count} {En(count, "boar", "boars")}");
                case Core.Unit.Wolves: return T($"{count} {Plural(count, "волк", "волка", "волков")}", $"{count} {En(count, "wolf", "wolves")}");
                case Core.Unit.Bandits: return T($"{count} {Plural(count, "разбойник", "разбойника", "разбойников")}", $"{count} {En(count, "bandit", "bandits")}");
                case Core.Unit.Skeletons: return T($"{count} {Plural(count, "скелет", "скелета", "скелетов")}", $"{count} {En(count, "skeleton", "skeletons")}");
                case Core.Unit.Spiders: return T($"{count} {Plural(count, "паук", "паука", "пауков")}", $"{count} {En(count, "spider", "spiders")}");
                default: return T($"{count} {Plural(count, "тролль", "тролля", "троллей")}", $"{count} {En(count, "troll", "trolls")}");
            }
        }

        /// <summary>The name of a kind of unit: "Копейщики".</summary>
        public static string Unit(Unit unit)
        {
            switch (unit)
            {
                case Core.Unit.Spearmen: return T("Копейщики", "Spearmen");
                case Core.Unit.Archers: return T("Лучники", "Archers");
                case Core.Unit.Riders: return T("Всадники", "Riders");
                case Core.Unit.Goblins: return T("Гоблины", "Goblins");
                case Core.Unit.Boars: return T("Кабаны", "Boars");
                case Core.Unit.Wolves: return T("Волки", "Wolves");
                case Core.Unit.Bandits: return T("Разбойники", "Bandits");
                case Core.Unit.Skeletons: return T("Скелеты", "Skeletons");
                case Core.Unit.Spiders: return T("Пауки", "Spiders");
                default: return T("Тролли", "Trolls");
            }
        }

        public static string Place(Place place)
        {
            switch (place)
            {
                case Core.Place.Chest: return T("Сундук", "Chest");
                case Core.Place.WoodPile: return T("Брёвна", "Logs");
                case Core.Place.StonePile: return T("Груда камня", "Pile of stone");
                case Core.Place.GoldPile: return T("Кучка золота", "Heap of gold");
                case Core.Place.ShrineAttack: return T("Алтарь силы", "Altar of strength");
                case Core.Place.ShrineDefence: return T("Алтарь стойкости", "Altar of endurance");
                case Core.Place.Watchtower: return T("Сторожевая башня", "Watchtower");
                default: return "";
            }
        }

        public static string PlaceHint(Place place)
        {
            switch (place)
            {
                case Core.Place.Chest: return T("Внутри золото или опыт для героя: выбирай сам.", "Gold or experience for the hero inside: you choose.");
                case Core.Place.WoodPile: return T("Герой заберёт дерево в казну.", "The hero will take the wood to the treasury.");
                case Core.Place.StonePile: return T("Герой заберёт камень в казну.", "The hero will take the stone to the treasury.");
                case Core.Place.GoldPile: return T("Герой заберёт золото в казну.", "The hero will take the gold to the treasury.");
                case Core.Place.ShrineAttack: return T("Каждый герой получит здесь +1 к атаке, один раз.", "Every hero gets +1 attack here, once.");
                case Core.Place.ShrineDefence: return T("Каждый герой получит здесь +1 к защите, один раз.", "Every hero gets +1 defence here, once.");
                case Core.Place.Watchtower: return T("С вершины видно далеко вокруг.", "From the top you can see far around.");
                default: return "";
            }
        }

        /// <summary>The icon of a place in the art library.</summary>
        public static string PlaceIcon(Place place)
        {
            switch (place)
            {
                case Core.Place.WoodPile: return "logs";
                case Core.Place.StonePile: return "stone";
                case Core.Place.GoldPile: return "coins";
                case Core.Place.ShrineAttack: return "swords";
                case Core.Place.ShrineDefence: return "shield_blue";
                case Core.Place.Chest: return "coins";
                default: return "crown";
            }
        }

        /// <summary>What is special about a unit in a fight, for its card on the battlefield; null when nothing.</summary>
        public static string Abilities(UnitStats stats)
        {
            if (stats.Has(Ability.Shoots))
                return T("Стреляет издалека, пока рядом нет врага.", "Shoots from afar while no enemy is near.");
            if (stats.Has(Ability.NoStrikeBack))
                return T("Никто не успевает дать ему сдачи.", "No one has time to strike back at it.");
            if (stats.Has(Ability.ArrowProof))
                return T("Стрелы бьют его вполсилы.", "Arrows hit it at half strength.");
            if (stats.Has(Ability.Web))
                return T("Кого укусит, тот запутается и не сойдёт с места.", "Whoever it bites gets tangled and can't move.");
            if (stats.Has(Ability.Regenerates))
                return T("Каждый раунд залечивает раны.", "Heals its wounds every round.");
            return null;
        }

        /// <summary>
        /// Whom a monster stack goes for in a fight (<see cref="Units.TemperOf"/>), for its card on
        /// the battlefield, so that the players can plan against it; null for the hired units.
        /// </summary>
        public static string Temper(Unit unit, int count)
        {
            bool one = count == 1;
            switch (Units.TemperOf(unit))
            {
                case Core.Temper.Mob:
                    return T("Толпой на ближайшего.", "Mob the nearest one.");
                case Core.Temper.Charge:
                    return one ? T("Несётся напролом на того, кто перед ним.", "Charges headlong at whoever is in front of it.") : T("Несутся напролом на того, кто перед ними.", "Charge headlong at whoever is in front of them.");
                case Core.Temper.Weakest:
                    return T("Стаей на самого слабого.", "Hunt the weakest as a pack.");
                case Core.Temper.Nearest:
                    return one ? T("Стреляет в того, кто ближе.", "Shoots whoever is closest.") : T("Стреляют в того, кто ближе.", "Shoot whoever is closest.");
                case Core.Temper.Wall:
                    return one ? T("Стрелков оставляет напоследок.", "Leaves the archers for last.") : T("Стрелков оставляют напоследок.", "Leave the archers for last.");
                case Core.Temper.Fastest:
                    return one ? T("Ловит в паутину самых быстрых.", "Catches the fastest in its web.") : T("Ловят в паутину самых быстрых.", "Catch the fastest in their webs.");
                case Core.Temper.Biggest:
                    return one ? T("Идёт на самый большой отряд.", "Goes for the biggest troop.") : T("Идут на самый большой отряд.", "Go for the biggest troop.");
                default:
                    return null;
            }
        }

        /// <summary>Goblins, bandits and the troll give up; beasts back away.</summary>
        static bool Beasts(Unit unit) => unit == Core.Unit.Wolves || unit == Core.Unit.Boars || unit == Core.Unit.Spiders;

        /// <summary>The words over the monsters when they offer to give up: "Сдаются!".</summary>
        public static string Yields(Unit unit, int count) =>
            Beasts(unit) ? (count == 1 ? T("Пятится!", "Backs off!") : T("Пятятся!", "Back off!")) : count == 1 ? T("Сдаётся!", "Gives up!") : T("Сдаются!", "Give up!");

        /// <summary>The title of the offer: "Гоблины сдаются".</summary>
        public static string YieldTitle(Unit unit) =>
            unit == Core.Unit.Troll ? T("Тролль сдаётся", "The troll gives up") : T($"{Unit(unit)} {(Beasts(unit) ? "отступают" : "сдаются")}", $"{Unit(unit)} {(Beasts(unit) ? "back off" : "give up")}");

        /// <summary>The question when the monsters offer to give up, and its answers: let go (Enter), finish off (Esc).</summary>
        public static string YieldOffer => T("Отпустить: бой выигран сейчас, без новых потерь. Добить: герой получит больше опыта.", "Let go: the battle is won now, with no more losses. Finish off: the hero gains more experience.");

        public static string[] YieldAnswers => new[] { T("Отпустить", "Let go"), T("Добить", "Finish off") };

        /// <summary>The line on the result after the monsters were let go: "Гоблины сдались и ушли. Путь свободен!".</summary>
        public static string LetGo(Monster monster, bool lair)
        {
            Unit unit = Units.Of(monster);
            string gone = unit == Core.Unit.Troll ? T("Тролль сдался и ушёл.", "The troll gave up and left.")
                : Beasts(unit) ? T($"{Unit(unit)} убежали.", $"{Unit(unit)} ran away.") : T($"{Unit(unit)} сдались и ушли.", $"{Unit(unit)} gave up and left.");
            return T($"{gone} {(lair ? "Логово разорено!" : "Путь свободен!")}", $"{gone} {(lair ? "The lair is cleared!" : "The way is clear!")}");
        }

        /// <summary>The line over the reward a won fight would pay.</summary>
        public static string FightReward => T("Награда за победу", "Reward for victory");

        /// <summary>Why the reward is bigger than the group's own, beside it: "×2 — бой равный"; empty for an easy fight.</summary>
        public static string RewardFactor(int factor) =>
            factor >= 3 ? T("×3 — бой опасный", "×3 — a dangerous fight") : factor == 2 ? T("×2 — бой равный", "×2 — an even fight") : "";

        /// <summary>The question before a retreat and its answers: retreat, or fight on (Esc).</summary>
        public static string RetreatTitle => T("Отступить?", "Retreat?");

        public static string RetreatAsk => T("Войско уйдёт в ближайший город, но каждый отряд оставит на поле четверть.", "The army will go to the nearest town, but each troop will leave a quarter on the field.");

        public static string[] RetreatAnswers => new[] { T("Отступить", "Retreat"), T("Сражаться дальше", "Fight on") };

        /// <summary>The result's title after a lost fight: a retreat or a rout.</summary>
        public static string LostTitle(bool retreated) => retreated ? T("Отступление", "Retreat") : T("Поражение", "Defeat");

        /// <summary>The result's line after a lost fight, with the town the hero went back to (and the militia waiting there).</summary>
        public static string Lost(string town, bool retreated, int militia) =>
            retreated ? T($"Войско отступило в город {town}. Каждый отряд оставил на поле четверть.", $"The army retreated to {town}. Each troop left a quarter on the field.")
            : militia > 0 ? T($"Войско разбито. В городе {town} героя ждёт ополчение: {Count(Core.Unit.Spearmen, militia)}.", $"The army is beaten. In {town} a militia waits for the hero: {Count(Core.Unit.Spearmen, militia)}.")
            : T($"Войско разбито. Герой вернулся в город {town}.", $"The army is beaten. The hero has returned to {town}.");

        /// <summary>How a fight looks for the hero's army: easy, even, dangerous or hopeless.</summary>
        public static string Odds(Odds odds) =>
            odds.Win >= Core.Odds.Easy ? T("Лёгкий бой", "Easy fight") : odds.Win >= Core.Odds.Even ? T("Равный бой", "Even fight")
            : odds.Win >= 0.05f ? T("Опасно!", "Dangerous!") : T("Не победить", "Can't win");

        /// <summary>The share of a stack the players are likely to lose, in words.</summary>
        public static string Losses(float share) =>
            share < 0.05f ? T("без потерь", "no losses") : share < 0.3f ? T("малые потери", "small losses") : share < 0.7f ? T("большие потери", "heavy losses") : T("почти все погибнут", "almost all will fall");

        /// <summary>What a hero found on a place or in a town; null when there is nothing to tell.</summary>
        public static string Visit(StepResult step)
        {
            if (step.Garrison && step.Settlers)
                return T("Герой забрал отряды и обоз поселенцев, что ждали в городе", "The hero took the troops and the settlers' wagon that waited in town");
            if (step.Garrison)
                return T("Герой забрал отряды, что ждали в городе", "The hero took the troops that waited in town");
            if (step.Settlers)
                return T("Герой повёл обоз поселенцев, что ждал в городе", "The hero leads the settlers' wagon that waited in town");
            switch (step.Place)
            {
                case Core.Place.WoodPile:
                case Core.Place.StonePile:
                case Core.Place.GoldPile:
                    return T("Найдено: ", "Found: ") + Goods(step.Gained);
                case Core.Place.ShrineAttack:
                    return step.Again ? T("Герой уже молился у этого алтаря", "The hero has already prayed at this altar") : T("Алтарь силы: атака героя +1", "Altar of strength: the hero's attack +1");
                case Core.Place.ShrineDefence:
                    return step.Again ? T("Герой уже молился у этого алтаря", "The hero has already prayed at this altar") : T("Алтарь стойкости: защита героя +1", "Altar of endurance: the hero's defence +1");
                case Core.Place.Watchtower:
                    return T("С башни видно далеко вокруг!", "You can see far around from the tower!");
                default:
                    return null;
            }
        }

        /// <summary>"+2 еды, +1 золота" for a stock of goods, or an empty string.</summary>
        public static string Yield(Stock stock)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (stock.Gold != 0) parts.Add(T($"+{stock.Gold} {Plural(stock.Gold, "золото", "золота", "золота")}", $"+{stock.Gold} gold"));
            if (stock.Food != 0) parts.Add(T($"+{stock.Food} {Plural(stock.Food, "еда", "еды", "еды")}", $"+{stock.Food} food"));
            if (stock.Wood != 0) parts.Add(T($"+{stock.Wood} {Plural(stock.Wood, "дерево", "дерева", "дерева")}", $"+{stock.Wood} wood"));
            if (stock.Stone != 0) parts.Add(T($"+{stock.Stone} {Plural(stock.Stone, "камень", "камня", "камней")}", $"+{stock.Stone} stone"));
            return string.Join(", ", parts);
        }

        /// <summary>Four turns make a year, starting in spring.</summary>
        public static string Season(int turn)
        {
            switch ((turn - 1) % 4)
            {
                case 0: return T("Весна", "Spring");
                case 1: return T("Лето", "Summer");
                case 2: return T("Осень", "Autumn");
                default: return T("Зима", "Winter");
            }
        }

        public static int Year(int turn) => (turn - 1) / 4 + 1;

        /// <summary>A line about the land, for the info card.</summary>
        public static string BiomeHint(Cell cell)
        {
            if (cell.IsWater && cell.Lake)
                return T("Тихая вода в кольце берегов.", "Still water in a ring of shores.");
            switch (cell.Biome)
            {
                case Core.Biome.Ocean: return T("Глубокая вода, сюда заплывают только смелые.", "Deep water, only the brave sail here.");
                case Core.Biome.Shallows: return T("Тёплая вода у берега, здесь ловят рыбу.", "Warm water by the shore, where fish are caught.");
                case Core.Biome.Beach: return T("Песок, ракушки и крики чаек.", "Sand, shells and the cries of gulls.");
                case Core.Biome.Meadow: return T("Цветы и сочная трава, раздолье для овец.", "Flowers and lush grass, plenty of room for sheep.");
                case Core.Biome.Plains: return T("Сухая трава до горизонта, здесь растёт пшеница.", "Dry grass to the horizon, where wheat grows.");
                case Core.Biome.Forest: return T("Тенистые деревья, грибы и дичь.", "Shady trees, mushrooms and game.");
                case Core.Biome.Hills: return T("Пологие холмы, в них прячутся камень и золото.", "Gentle hills that hide stone and gold.");
                case Core.Biome.Mountains: return T("Высокие скалы и снег на вершинах.", "High cliffs and snow on the peaks.");
                default: return "";
            }
        }

        /// <summary>"5 дерева, 2 камня" for a cost, or an empty string.</summary>
        public static string Goods(Stock stock)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (stock.Gold > 0) parts.Add(T($"{stock.Gold} {Plural(stock.Gold, "золото", "золота", "золота")}", $"{stock.Gold} gold"));
            if (stock.Food > 0) parts.Add(T($"{stock.Food} {Plural(stock.Food, "еда", "еды", "еды")}", $"{stock.Food} food"));
            if (stock.Wood > 0) parts.Add(T($"{stock.Wood} {Plural(stock.Wood, "дерево", "дерева", "дерева")}", $"{stock.Wood} wood"));
            if (stock.Stone > 0) parts.Add(T($"{stock.Stone} {Plural(stock.Stone, "камень", "камня", "камней")}", $"{stock.Stone} stone"));
            return string.Join(", ", parts);
        }

        public static string Field(FieldBuilding building)
        {
            switch (building)
            {
                case FieldBuilding.Farm: return T("Ферма", "Farm");
                case FieldBuilding.Fishery: return T("Рыбацкий причал", "Fishing pier");
                case FieldBuilding.Pasture: return T("Пастбище", "Pasture");
                case FieldBuilding.LumberCamp: return T("Лесопилка", "Sawmill");
                case FieldBuilding.Quarry: return T("Каменоломня", "Quarry");
                case FieldBuilding.GoldMine: return T("Золотой рудник", "Gold mine");
                default: return "";
            }
        }

        public static string Town(TownBuilding building)
        {
            switch (building)
            {
                case TownBuilding.Market: return T("Рынок", "Market");
                case TownBuilding.Granary: return T("Амбар", "Granary");
                case TownBuilding.Barracks: return T("Казарма", "Barracks");
                case TownBuilding.ArcheryRange: return T("Стрельбище", "Archery range");
                case TownBuilding.Stables: return T("Конюшня", "Stables");
                case TownBuilding.Walls: return T("Стены", "Walls");
                case TownBuilding.Castle: return T("Замок", "Castle");
                default: return T("Таверна", "Tavern");
            }
        }

        /// <summary>What a town building does, in a few words.</summary>
        public static string TownHint(TownBuilding building)
        {
            switch (building)
            {
                case TownBuilding.Market: return T("+2 золота за ход", "+2 gold a turn");
                case TownBuilding.Granary: return T($"город берёт на рост {Towns.GranaryIntake} еды вместо {Towns.Intake}", $"the town takes {Towns.GranaryIntake} food to grow instead of {Towns.Intake}");
                case TownBuilding.Barracks: return T($"копейщики, {Units.PerTurn(Core.Unit.Spearmen)} за ход", $"spearmen, {Units.PerTurn(Core.Unit.Spearmen)} a turn");
                case TownBuilding.ArcheryRange: return T($"лучники, {Units.PerTurn(Core.Unit.Archers)} за ход", $"archers, {Units.PerTurn(Core.Unit.Archers)} a turn");
                case TownBuilding.Stables: return T($"всадники, {Units.PerTurn(Core.Unit.Riders)} за ход", $"riders, {Units.PerTurn(Core.Unit.Riders)} a turn");
                case TownBuilding.Walls: return T("сами отбивают ватаги из логов, и за это — добыча", "beat off bands from lairs by themselves, and get loot for it");
                case TownBuilding.Castle: return T($"город растёт дальше {Towns.CastleHouses} домов, до королевского", $"the town grows beyond {Towns.CastleHouses} houses, into a royal town");
                default: return T($"нанимает героев; город растёт дальше {Towns.TavernHouses} домов", $"hires heroes; the town grows beyond {Towns.TavernHouses} houses");
            }
        }

        /// <summary>1 деревня, 2 городок, 3 город, 4 королевский город.</summary>
        public static string Level(int level)
        {
            switch (level)
            {
                case 1: return T("Деревня", "Village");
                case 2: return T("Городок", "Small town");
                case 3: return T("Город", "Town");
                default: return T("Королевский город", "Royal town");
            }
        }

        /// <summary>Why something cannot be built or bought, with what is missing.</summary>
        public static string Refusal(Refusal refusal, Stock missing = default)
        {
            switch (refusal)
            {
                case Core.Refusal.NotYours: return T("Это не твоя земля", "This is not your land");
                case Core.Refusal.NoResource: return T("Здесь нечего строить", "Nothing to build here");
                case Core.Refusal.AlreadyBuilt: return T("Уже построено", "Already built");
                case Core.Refusal.NotEnough: return T("Не хватает: ", "Not enough: ") + Goods(missing);
                case Core.Refusal.LevelTooLow: return T("Сначала вырасти до городка (5 домов)", "First grow into a small town (5 houses)");
                case Core.Refusal.NoSlot: return T("Нет места: расширь город на соседнюю клетку", "No room: expand the town onto a neighbouring tile");
                case Core.Refusal.NoTavern: return T("Героев нанимают в таверне", "Heroes are hired in a tavern");
                case Core.Refusal.TooManyHeroes: return T($"Больше {HeroRules.MaxHeroes} героев не бывает", $"There can't be more than {HeroRules.MaxHeroes} heroes");
                case Core.Refusal.Occupied: return T("В городе уже стоит герой", "A hero already stands in the town");
                case Core.Refusal.NoBuilding: return T("Сначала построй, где их нанимать", "First build a place to hire them");
                case Core.Refusal.NoRecruits: return T("Новобранцев больше нет: жди следующего хода", "No more recruits: wait for the next turn");
                case Core.Refusal.NoRoom: return T("В войске нет места: не больше пяти отрядов", "No room in the army: five troops at most");
                case Core.Refusal.NoSettlers: return T("С героем нет обоза поселенцев", "The hero has no settlers' wagon");
                case Core.Refusal.SettlersWaiting: return T("Обоз уже ждёт в городе героя", "A wagon already waits in town for a hero");
                case Core.Refusal.BadGround: return T("В горах и на воде город не построить", "A town can't be built in the mountains or on water");
                case Core.Refusal.TooClose: return T($"Слишком близко к другому городу: нужно не меньше {Settling.Spacing} шагов", $"Too close to another town: at least {Settling.Spacing} steps are needed");
                case Core.Refusal.Taken: return T("Клетка занята: выбери пустую, без месторождения", "The tile is taken: choose an empty one without a deposit");
                case Core.Refusal.NoWalls: return T("Сначала обнеси город стенами", "First put walls around the town");
                case Core.Refusal.NotACity: return T("Сначала вырасти до города (8 домов)", "First grow into a town (8 houses)");
                case Core.Refusal.HasRoom: return T("В городе ещё есть свободные места", "The town still has free places");
                case Core.Refusal.NotBesideTown: return T("Город растёт только на клетки рядом со своим центром", "A town grows only onto tiles next to its centre");
                default: return "";
            }
        }

        /// <summary>Why a town does not grow.</summary>
        public static string Stop(GrowthStop stop)
        {
            switch (stop)
            {
                case GrowthStop.Food: return T("не хватает еды для роста", "not enough food to grow");
                case GrowthStop.Tavern: return T("нужна таверна, чтобы расти дальше", "needs a tavern to grow further");
                case GrowthStop.Full: return T("вырос до предела", "has grown to its limit");
                case GrowthStop.Castle: return T("нужен замок, чтобы расти дальше", "needs a castle to grow further");
                case GrowthStop.Room: return T("нет места: расширь город", "no room: expand the town");
                case GrowthStop.Raid: return T("у города стоит ватага из логова", "a band from a lair stands at the town");
                default: return "";
            }
        }

        public static string Quest(Quest quest)
        {
            switch (quest)
            {
                case Core.Quest.Farm: return T("Построй ферму на пшенице", "Build a farm on wheat");
                case Core.Quest.LumberCamp: return T("Построй лесопилку в лесу", "Build a sawmill in a forest");
                case Core.Quest.Quarry: return T("Построй каменоломню", "Build a quarry");
                case Core.Quest.Granary: return T("Построй амбар в городе", "Build a granary in a town");
                case Core.Quest.Town: return T("Вырасти деревню до городка", "Grow a village into a small town");
                case Core.Quest.Market: return T("Построй рынок", "Build a market");
                case Core.Quest.Tavern: return T("Построй таверну", "Build a tavern");
                case Core.Quest.GoldMine: return T("Построй золотой рудник", "Build a gold mine");
                case Core.Quest.City: return T("Вырасти город до 8 домов", "Grow a town to 8 houses");
                case Core.Quest.Fight: return T("Победи чудовищ героем", "Defeat monsters with a hero");
                case Core.Quest.Chest: return T("Найди и открой сундук", "Find and open a chest");
                case Core.Quest.Barracks: return T("Построй казарму", "Build barracks");
                case Core.Quest.Lair: return T("Разори логово чудовищ", "Clear a monster lair");
                case Core.Quest.ArcheryRange: return T("Построй стрельбище", "Build an archery range");
                case Core.Quest.HeroLevel: return T($"Подними героя до {Quests.HeroLevelGoal} уровня", $"Raise a hero to level {Quests.HeroLevelGoal}");
                case Core.Quest.Stables: return T("Построй конюшню", "Build stables");
                case Core.Quest.Settle: return T("Основай новый город поселенцами", "Found a new town with settlers");
                case Core.Quest.Walls: return T("Обнеси город стенами", "Put walls around a town");
                case Core.Quest.Stake: return T("Поставь вешку: покажи городу, куда расти", "Place a stake: show a town where to grow");
                default: return "";
            }
        }

        /// <summary>The icon of a task in the art library.</summary>
        public static string QuestIcon(Quest quest)
        {
            switch (quest)
            {
                case Core.Quest.Farm: return "wheat";
                case Core.Quest.LumberCamp: return "logs";
                case Core.Quest.Quarry: return "stone";
                case Core.Quest.GoldMine: return "ingot";
                case Core.Quest.Town:
                case Core.Quest.City: return "crown";
                case Core.Quest.Fight:
                case Core.Quest.Lair: return "swords";
                case Core.Quest.Chest: return "coins";
                case Core.Quest.HeroLevel: return "crown";
                case Core.Quest.Settle: return "icon_cart";
                case Core.Quest.Walls: return "icon_town";
                // The card shows the kingdom's own flag for it; this is only for a missing flag.
                case Core.Quest.Stake: return "icon_growth";
                default: return "hammer";
            }
        }

        /// <summary>The line of a task done, with its reward.</summary>
        public static string QuestDone(Quest quest) => T($"Задание выполнено: {Quest(quest)}. Награда: {Goods(Quests.Reward(quest))}", $"Task done: {Quest(quest)}. Reward: {Goods(Quests.Reward(quest))}");

        /// <summary>A line of the turn report about a town or a task.</summary>
        public static string Event(TurnEvent e, World world)
        {
            if (e.Kind == TurnEventKind.QuestDone)
                return QuestDone(e.Quest);
            if (e.Kind == TurnEventKind.MonstersGrew)
                return T("Чудовища в диких землях окрепли: их стало больше", "The monsters in the wild have grown stronger: there are more of them");
            if (e.Kind == TurnEventKind.GemSet)
                return e.Country == world.Player ? T($"Новый самоцвет в короне: «{Gem(e.Gem)}»!", $"A new gem in the crown: “{Gem(e.Gem)}”!")
                    : T($"{world.Countries[e.Country].Label}: новый самоцвет в короне, «{Gem(e.Gem)}»", $"{world.Countries[e.Country].Label}: a new gem in the crown, “{Gem(e.Gem)}”");
            if (e.Kind == TurnEventKind.StakeLost)
                return StakeLost(world.Countries[e.Country].Label);
            // A neighbour's troop has no town of the players' to name.
            if (e.Kind == TurnEventKind.TroopSent)
                return TroopRow(world.Countries[e.Country].Label, e.Amount);
            if (e.Kind == TurnEventKind.TroopCleared)
                return T($"{world.Countries[e.Country].Label}: отряд взял логово, оно теперь пустое", $"{world.Countries[e.Country].Label}: a troop took a lair, it is empty now");
            if (e.Kind == TurnEventKind.TroopRecalled)
                return T($"{world.Countries[e.Country].Label}: отряд ушёл домой — логово взял твой герой", $"{world.Countries[e.Country].Label}: a troop went home — your hero took the lair");
            City city = world.Cities[e.City];
            switch (e.Kind)
            {
                case TurnEventKind.Border: return T($"{city.Label}: новая клетка земли", $"{city.Label}: a new tile of land");
                case TurnEventKind.StakeReached: return T($"{city.Label}: граница дошла до вешки", $"{city.Label}: the border has reached the stake");
                case TurnEventKind.LandFull: return T($"{city.Label}: некуда расти. Вырасти город или поставь новый", $"{city.Label}: nowhere to grow. Grow the town or found a new one");
                case TurnEventKind.Grew: return T($"{city.Label}: новый дом, теперь {city.Houses}", $"{city.Label}: a new house, now {city.Houses}");
                case TurnEventKind.LevelUp: return T($"{city.Label} теперь {Level(Towns.Level(city)).ToLowerInvariant()}!", $"{city.Label} is now a {Level(Towns.Level(city)).ToLowerInvariant()}!");
                case TurnEventKind.RaidSent: return T($"{Monster(e.Monster)} вышли из логова и идут к городу {city.Label}", $"{Monster(e.Monster)} have left their lair and are heading for {city.Label}");
                case TurnEventKind.RaidArrived:
                    return T($"{Monster(e.Monster)} у города {city.Label}: {Raids.StayTurns} {Plural(Raids.StayTurns, "ход", "хода", "ходов")} ", $"{Monster(e.Monster)} at {city.Label}: for {Raids.StayTurns} {En(Raids.StayTurns, "turn", "turns")} ") +
                        T("он не растёт, а поле под ними не даёт дохода", "it won't grow, and the field under them gives nothing");
                case TurnEventKind.RaidRepelled: return T($"Стены города {city.Label} отбили ватагу: добыча {e.Amount} золота", $"The walls of {city.Label} beat off the band: {e.Amount} gold in loot");
                case TurnEventKind.RaidLeft: return T($"{Monster(e.Monster)} ушли от города {city.Label}", $"{Monster(e.Monster)} have left {city.Label}");
                default: return $"{city.Label}: {Stop(e.Stop)}";
            }
        }

        // ----- The border -----

        /// <summary>The caption of the border's line on the cards.</summary>
        public static string BorderCaption => T("Граница", "Border");

        /// <summary>The rule, in the words for the boy (the territory plan of 2026-11-11).</summary>
        public static string BorderHint => T("Города сами берут землю вокруг. Чем больше город, тем быстрее и дальше. Пунктир с флажком — клетка, которую город возьмёт следующей.", "Towns take the land around them by themselves. The bigger the town, the faster and farther. A dashed outline with a small flag is the tile a town takes next.");

        /// <summary>When the town takes the wild cell: «примерно», as a neighbour may take it first.</summary>
        public static string BorderTurns(string town, int turns) =>
            T($"{town} возьмёт её примерно через {turns} {Plural(turns, "ход", "хода", "ходов")}", $"{town} will take it in about {turns} {En(turns, "turn", "turns")}");

        /// <summary>Why none of the players' towns will take the wild cell.</summary>
        public static string BorderNever(bool open, bool foreign) =>
            !open ? T("Рядом чудовища: граница обходит их, пока герой их не прогонит.", "Monsters nearby: the border goes around them until a hero drives them off.")
            : foreign ? T("Эта земля у чужого города: твоя граница сюда не придёт.", "This land is by another kingdom's town: your border won't come here.")
            : T("Сюда не дотягивается ни один твой город. Вырасти город или поставь новый рядом.", "None of your towns reaches this far. Grow a town or found a new one nearby.");

        // ----- Raids (the fun plan's Ф3) -----

        /// <summary>The caption of the card's line on a band out of a lair.</summary>
        public static string BandCaption => T("Ватага", "Band");

        /// <summary>The line over the map about a band going for the players' town or standing at it.</summary>
        public static string RaidLine(Monster monster, string town, int turns, bool standing) => standing
            ? T($"{Monster(monster)} у города {town}: ещё {turns} {Plural(turns, "ход", "хода", "ходов")} без роста", $"{Monster(monster)} at {town}: {turns} more {En(turns, "turn", "turns")} without growth")
            : T($"{Monster(monster)} идут к городу {town} — {turns} {Plural(turns, "ход", "хода", "ходов")}", $"{Monster(monster)} heading for {town} — {turns} {En(turns, "turn", "turns")}");

        /// <summary>The card's line on a band: where it goes, or how long it stays.</summary>
        public static string BandLine(string town, int turns, bool standing) => standing
            ? T($"стоит у города {town} ещё {turns} {Plural(turns, "ход", "хода", "ходов")}", $"stands at {town} for {turns} more {En(turns, "turn", "turns")}")
            : T($"идёт к городу {town}, будет там через {turns} {Plural(turns, "ход", "хода", "ходов")}", $"heading for {town}, will be there in {turns} {En(turns, "turn", "turns")}");

        /// <summary>The rule, under the band's card.</summary>
        public static string BandHint => T("Город со стенами отобьётся сам. Без стен он не растёт, пока ватага стоит у него. Герой может перехватить её.", "A walled town beats them off by itself. Without walls it doesn't grow while the band stands at it. A hero can intercept the band.");

        // ----- The neighbours' troops at the lairs (the economy plan's step 4) -----

        /// <summary>The caption of the card's line on a neighbour's troop at the lair («Отряд» is the group's count).</summary>
        public static string TroopCaption => T("Осада", "Siege");

        /// <summary>The card's line on a neighbour's troop at the lair.</summary>
        public static string TroopLine(string kingdom, int turns) =>
            T($"{kingdom}: отряд возьмёт логово через {turns} {Plural(turns, "ход", "хода", "ходов")}", $"{kingdom}: the troop will take the lair in {turns} {En(turns, "turn", "turns")}");

        /// <summary>The line over the map and in the turn's report about a neighbour's troop at a lair the players know.</summary>
        public static string TroopRow(string kingdom, int turns) =>
            T($"{kingdom} осаждает логово: возьмёт его через {turns} {Plural(turns, "ход", "хода", "ходов")}", $"{kingdom} besieges the lair: will take it in {turns} {En(turns, "turn", "turns")}");

        /// <summary>The rule, under the lair's card.</summary>
        public static string TroopHint => T("Победи логово героем раньше — и оно засчитается тебе.", "Beat the lair with a hero first, and it counts for you.");

        // ----- The stake (вешка; the territory plan's П3, without buying) -----

        /// <summary>The caption of the card's line on when the border comes with the stake.</summary>
        public static string StakeCaption => T("С вешкой", "With a stake");

        /// <summary>The caption of the card's line on the cell the stake stands on.</summary>
        public static string StakeLine => T("Вешка", "Stake");

        /// <summary>The card's line beside «Граница»: when the town gets there with the stake on the cell.</summary>
        public static string StakeTurns(int turns) => T($"примерно через {turns} {Plural(turns, "ход", "хода", "ходов")}", $"in about {turns} {En(turns, "turn", "turns")}");

        /// <summary>The card's line on the cell the stake stands on.</summary>
        public static string StakeHere(string town) => T($"стоит здесь: {town} растёт сюда", $"stands here: {town} grows this way");

        public static string StakeSet => T("Поставить вешку", "Place a stake");
        public static string StakeMove => T("Перенести вешку сюда", "Move the stake here");
        public static string StakeRemove => T("Убрать вешку", "Remove the stake");
        public static string StakeSetWhat => T("бесплатно, одна на королевство", "free, one per kingdom");
        public static string StakeMoveWhat => T("со старого места она уйдёт", "it leaves its old place");
        public static string StakeRemoveWhat => T("города снова растут как хотят", "towns grow as they like again");

        /// <summary>The rule, in the words for the boy.</summary>
        public static string StakeHint => T("Вешка показывает городу, куда расти: ближайший город возьмёт землю по дороге к ней. Она бесплатная, а когда клетка станет твоей, вешка уберётся сама.", "A stake shows a town where to grow: the nearest town takes land on the way to it. It is free, and when the tile becomes yours, the stake goes away by itself.");

        /// <summary>The hint while the pointer is over a wild cell a stake may stand on.</summary>
        public static string StakeClick => T("Щёлкни по клетке: сюда можно поставить вешку, и город пойдёт к ней.", "Click a tile: a stake can go here, and the town will grow towards it.");

        /// <summary>The turn report's line when a neighbour took the cell first («успели раньше»).</summary>
        public static string StakeLost(string kingdom) => T($"Вешку пришлось убрать: клетку раньше занял сосед — {kingdom}", $"The stake had to go: a neighbour took the tile first — {kingdom}");

        /// <summary>The toast when the stake may not stand on the cell (the card said so already).</summary>
        public static string StakeRefused => T("Сюда вешку не поставить: ни один твой город не дотянется", "No stake here: none of your towns can reach it");

        /// <summary>The toast after the stake was set or taken away.</summary>
        public static string StakeToast(string town, int turns) =>
            town == null ? T("Вешка убрана", "Stake removed") : T($"Вешка стоит: {town} дойдёт до неё примерно через {turns} {Plural(turns, "ход", "хода", "ходов")}", $"Stake placed: {town} will reach it in about {turns} {En(turns, "turn", "turns")}");

        /// <summary>The town card's line: when the next cell comes, or that there is nowhere to grow.</summary>
        public static string TownBorder(City city)
        {
            if (city.LandFull)
                return T("некуда расти", "nowhere to grow");
            int power = Influence.Power(city);
            int turns = System.Math.Max(1, (Influence.CellPrice - city.Influence + power - 1) / power);
            return T($"влияние {power} · клетка через {turns} {Plural(turns, "ход", "хода", "ходов")}", $"influence {power} · a tile in {turns} {En(turns, "turn", "turns")}");
        }

        /// <summary>The turn report's line for the cells the players' towns took.</summary>
        public static string BorderGrew(int cells, System.Collections.Generic.List<string> towns) =>
            T($"Граница: +{cells} {Plural(cells, "клетка", "клетки", "клеток")} ({string.Join(", ", towns)})", $"Border: +{cells} {En(cells, "tile", "tiles")} ({string.Join(", ", towns)})");

        /// <summary>The kingdoms' list's caption of the land bars and the numbers beside them.</summary>
        public static string KingdomsNote => T("Полоска — земля королевства, рядом — новые клетки за прошлый ход", "The bar is the kingdom's land; beside it, the new tiles of the last turn");

        /// <summary>The tooltip of the cells a kingdom's towns took last turn.</summary>
        public static string LandGain(int cells) =>
            cells > 0 ? T($"За прошлый ход граница выросла на {cells} {Plural(cells, "клетку", "клетки", "клеток")}", $"The border grew by {cells} {En(cells, "tile", "tiles")} last turn") : T("За прошлый ход граница не выросла", "The border didn't grow last turn");

        /// <summary>The turn report's line for a resource that came inside the border.</summary>
        public static string ResourceInside(Resource resource) =>
            T($"{Resource(resource)} теперь в твоих границах. Постройка: {Field(Economy.BuildingFor(resource)).ToLowerInvariant()}", $"{Resource(resource)} is now inside your borders. Building: {Field(Economy.BuildingFor(resource)).ToLowerInvariant()}");

        // ----- The crown -----

        /// <summary>The gem's name, after its feat.</summary>
        public static string Gem(Gem gem)
        {
            switch (gem)
            {
                case Core.Gem.Capital: return T("Столица", "Capital");
                case Core.Gem.Towns: return T("Города", "Towns");
                case Core.Gem.Hunter: return T("Охотник", "Hunter");
                case Core.Gem.Hero: return T("Герой", "Hero");
                default: return T("Земли", "Lands");
            }
        }

        /// <summary>The gem's picture in the interface art (Codex's gems; a coloured stone of the stylesheet while missing).</summary>
        public static string GemIcon(Gem gem)
        {
            switch (gem)
            {
                case Core.Gem.Capital: return "gem_capital";
                case Core.Gem.Towns: return "gem_towns";
                case Core.Gem.Hunter: return "gem_hunter";
                case Core.Gem.Hero: return "gem_hero";
                default: return "gem_lands";
            }
        }

        /// <summary>The tooltip of the gem a neighbour goes after, beside its crown in the kingdoms' list.</summary>
        public static string RivalAim(Gem gem) => T($"Сейчас добивается самоцвета «{Gem(gem)}»", $"Now going after the “{Gem(gem)}” gem");

        /// <summary>The difficulty's name in the new game's question.</summary>
        public static string Difficulty(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Core.Difficulty.Calm: return T("Спокойно", "Calm");
                case Core.Difficulty.Race: return T("Гонка", "Race");
                default: return T("Обычно", "Normal");
            }
        }

        /// <summary>What the difficulty means, under its buttons.</summary>
        public static string DifficultyText(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Core.Difficulty.Calm: return T("Соседи строят не спеша: одну постройку за три хода.", "The neighbours build slowly: one building in three turns.");
                case Core.Difficulty.Race: return T("Соседи торопятся: три постройки за два хода.", "The neighbours hurry: three buildings in two turns.");
                default: return T("Соседи строят две постройки за три хода.", "The neighbours build two buildings in three turns.");
            }
        }

        public static string DifficultyTitle => T("Соседи", "Neighbours");

        /// <summary>What sets the gem, as a task: «Разори 3 логова чудовищ».</summary>
        public static string GemFeat(Gem gem)
        {
            switch (gem)
            {
                case Core.Gem.Capital:
                    return T($"Вырасти столицу до {HousesFor(Crown.CapitalLevel)} домов: она станет королевским городом", $"Grow the capital to {HousesFor(Crown.CapitalLevel)} houses: it becomes a royal town");
                case Core.Gem.Towns:
                    return T($"Вырасти {Crown.TownsGoal} {Plural(Crown.TownsGoal, "город", "города", "городов")} до {HousesFor(Crown.TownsLevel)} домов", $"Grow {Crown.TownsGoal} {En(Crown.TownsGoal, "town", "towns")} to {HousesFor(Crown.TownsLevel)} houses");
                case Core.Gem.Hunter:
                    return T($"Разори {Crown.HunterGoal} {Plural(Crown.HunterGoal, "логово", "логова", "логов")} чудовищ", $"Clear {Crown.HunterGoal} monster {En(Crown.HunterGoal, "lair", "lairs")}");
                case Core.Gem.Hero:
                    return T($"Подними героя до {Crown.HeroGoal} уровня", $"Raise a hero to level {Crown.HeroGoal}");
                default:
                    return T($"Расширь королевство на {Crown.LandsGoal} {Plural(Crown.LandsGoal, "клетку", "клетки", "клеток")}", $"Expand the kingdom by {Crown.LandsGoal} {En(Crown.LandsGoal, "tile", "tiles")}");
            }
        }

        /// <summary>How far the kingdom has come: «логова: 1 из 3».</summary>
        public static string GemProgress(Gem gem, int have, int need)
        {
            have = System.Math.Min(have, need);
            switch (gem)
            {
                case Core.Gem.Capital: return T($"сейчас: {Level(System.Math.Max(1, have)).ToLowerInvariant()}", $"now: {Level(System.Math.Max(1, have)).ToLowerInvariant()}");
                case Core.Gem.Towns: return T($"городов: {have} из {need}", $"towns: {have} of {need}");
                case Core.Gem.Hunter: return T($"логова: {have} из {need}", $"lairs: {have} of {need}");
                case Core.Gem.Hero: return T($"уровень: {have} из {need}", $"level: {have} of {need}");
                default: return T($"клетки: {have} из {need}", $"tiles: {have} of {need}");
            }
        }

        /// <summary>A gem in the crown, and when it came: «в короне, ход 24».</summary>
        public static string GemWon(int turn) => T($"в короне, ход {turn}", $"in the crown, turn {turn}");

        /// <summary>The fewest houses of a town of the level.</summary>
        static int HousesFor(int level)
        {
            int houses = 1;
            while (Towns.Level(houses) < level)
                houses++;
            return houses;
        }

        public static string CrownTitle => T("Корона королевства", "The kingdom's crown");

        /// <summary>The crown's rule, under its title.</summary>
        public static string CrownGoal =>
            T($"Собери {Crown.Needed} {Plural(Crown.Needed, "самоцвет", "самоцвета", "самоцветов")} из {Crown.Order.Length}, и королевство победит. Самоцвет, раз попав в корону, остаётся в ней.", $"Gather {Crown.Needed} {En(Crown.Needed, "gem", "gems")} of {Crown.Order.Length}, and the kingdom wins. Once a gem is in the crown, it stays there.");

        /// <summary>The top bar's crown: «Корона: 1 из 3 самоцветов».</summary>
        public static string CrownTip(int have) => T($"Корона: {have} из {Crown.Needed} самоцветов. Нажми, чтобы увидеть подвиги и королевства (K)", $"Crown: {have} of {Crown.Needed} gems. Click to see the deeds and the kingdoms (K)");

        /// <summary>The line that pops up when the players' kingdom sets a gem.</summary>
        public static string GemToast(Gem gem, int have) =>
            have >= Crown.Needed ? T($"Самоцвет «{Gem(gem)}» в короне. Корона собрана!", $"The “{Gem(gem)}” gem is in the crown. The crown is complete!")
                : T($"Самоцвет «{Gem(gem)}» в короне! Собрано {have} из {Crown.Needed}", $"The “{Gem(gem)}” gem is in the crown! {have} of {Crown.Needed} gathered");

        /// <summary>A kingdom's gems in the kingdoms' list: «самоцветы: 2 из 3».</summary>
        public static string CrownCount(int have) => T($"{have} из {Crown.Needed}", $"{have} of {Crown.Needed}");

        public static string CrownWonTitle => T("Корона собрана!", "The crown is complete!");

        /// <summary>The end screen's line for the players' crown: first, or after a neighbour.</summary>
        public static string CrownWonText(int turn, int place) =>
            place <= 1 ? T($"Твоё королевство первым собрало корону, на {turn}-м ходу. Партия выиграна!", $"Your kingdom completed the crown first, on turn {turn}. You win the game!")
                : T($"Корона собрана на {turn}-м ходу. Место: {place}-е.", $"The crown is complete on turn {turn}. You came {Ordinal(place)}.");

        public static string CrownLostTitle => T("Соседи успели первыми", "The neighbours were first");

        /// <summary>The end screen's line when a neighbour set its crown first.</summary>
        public static string CrownLostText(string kingdom, int turn) =>
            T($"Первая корона у соседей: {kingdom}, на {turn}-м ходу. Можно доиграть и собрать свою.", $"The first crown went to a neighbour: {kingdom}, on turn {turn}. You can play on and complete your own.");

        /// <summary>The end screen's answers: play on (Esc), a new game.</summary>
        public static string[] CrownWonAnswers => new[] { T("Играть дальше", "Play on"), T("Новая игра", "New game") };
        public static string[] CrownLostAnswers => new[] { T("Доиграть свою", "Complete my own"), T("Новая игра", "New game") };

        /// <summary>The end screen's counts of the game.</summary>
        public static string StatVictories => T("Победы в боях", "Battles won");
        public static string StatLairs => T("Разорено логов", "Lairs cleared");
        public static string StatChests => T("Открыто сундуков", "Chests opened");
        public static string StatTowns => T("Основано городов", "Towns founded");

        // ----- The menu (Esc) -----

        public static string MenuButton => T("Меню", "Menu");
        public static string MenuBack => T("Вернуться в игру", "Back to the game");
        public static string MenuBackBattle => T("Вернуться в бой", "Back to the battle");
        public static string MenuNewGame => T("Новая игра", "New game");
        public static string MenuHelp => T("Как играть", "How to play");
        public static string MenuQuit => T("Выйти из игры", "Quit the game");
        public static string MenuSave => T("Сохранить", "Save");
        public static string MenuLoad => T("Загрузить", "Load");

        /// <summary>The menu's quit in a battle: the save before the battle brings it back.</summary>
        public static string MenuQuitBattle => T("Выйти (бой начнётся заново)", "Quit (the battle will start over)");

        /// <summary>The menu's line under the kingdom's name: "Карта №48213 · ход 34".</summary>
        public static string MenuMap(int seed, int turn) => T($"Карта №{seed} · ход {turn}", $"Map No. {seed} · turn {turn}");

        /// <summary>The menu's last line, small: "Версия 0.1.0".</summary>
        public static string Version(string version) => T($"Версия {version}", $"Version {version}");

        /// <summary>
        /// The game's name, under the crown where the main menu has no painted logo: the family
        /// liked Hexcrown, a crown of six faces like the map's hexagons (2026-11-11), and wants
        /// no motto under it.
        /// </summary>
        public const string GameName = "Hexcrown";

        public static string TitleLoading => T("Загрузка…", "Loading…");
        public static string TitleContinue => T("Продолжить", "Continue");

        /// <summary>The game «Продолжить» goes into: "Алое королевство, ход 34".</summary>
        public static string TitleContinueLine(string kingdom, int turn) => T($"{kingdom}, ход {turn}", $"{kingdom}, turn {turn}");

        public static string TitleQuit => T("Выход", "Quit");
        public static string MenuToTitle => T("В главное меню", "To the main menu");

        /// <summary>The new game's screen: the map beside it, another one or one by its number, start or back.</summary>
        public static string NewGameMap(int seed) => T($"Карта №{seed}", $"Map No. {seed}");

        public static string NewGameOther => T("Другая карта", "Another map");
        public static string NewGameNumber => T("Или карта по номеру:", "Or a map by its number:");
        public static string NewGameShow => T("Показать", "Show");
        public static string NewGameStart => T("Начать", "Start");
        public static string NewGameBack => T("Назад", "Back");

        // ----- The saves (the menu's lists) -----

        public static string SaveListTitle => T("Сохранить игру", "Save the game");
        public static string LoadListTitle => T("Загрузить игру", "Load a game");
        public static string SavesBack => T("Назад", "Back");

        /// <summary>The save list's first empty slot, gold.</summary>
        public static string SaveNew => T("Новое сохранение", "New save");

        /// <summary>The load list's parts: this game's autosaves, the earlier games', the players' own.</summary>
        public static string LoadThisGame => T("Эта партия", "This game");
        public static string LoadEarlier => T("Прошлые партии", "Earlier games");
        public static string LoadOwn => T("Свои сохранения", "Your saves");
        public static string LoadOwnNone => T("Своих пока нет: их делает «Сохранить» в меню.", "None of your own yet: “Save” in the menu makes them.");

        /// <summary>A slot's number in the save list.</summary>
        public static string SaveSlot(int slot) => T($"Ячейка {slot}", $"Slot {slot}");

        /// <summary>The question before a save takes an occupied slot, and its answers: replace (Enter), back (Esc).</summary>
        public static string ReplaceTitle => T("Заменить сохранение?", "Replace the save?");
        public static string ReplaceText(string kingdom, int turn) => T($"Заменить «{kingdom}, ход {turn}» новым сохранением?", $"Replace “{kingdom}, turn {turn}” with a new save?");
        public static string[] ReplaceAnswers => new[] { T("Заменить", "Replace"), T("Отмена", "Cancel") };

        /// <summary>Beside the turn counter for two seconds after the game kept itself.</summary>
        public static string Saved => T("Сохранено", "Saved");

        public static string SavedInto(string kingdom, int turn) => T($"Сохранено: {kingdom}, ход {turn}", $"Saved: {kingdom}, turn {turn}");
        public static string SaveFailed => T("Не удалось сохранить: диск не принял файл.", "Could not save: the disk did not take the file.");
        public static string Loaded(string kingdom, int turn) => T($"Партия загружена: {kingdom}, ход {turn}. F1 — подсказка", $"Game loaded: {kingdom}, turn {turn}. F1 — help");

        /// <summary>Why a save does not open, in the list's row and when it is picked.</summary>
        public static string ProblemOf(SaveProblem problem)
        {
            switch (problem)
            {
                case SaveProblem.Foreign: return T("Это не сохранение этой игры.", "This is not a save of this game.");
                case SaveProblem.Old: return T("Сохранение из старой версии игры: оно не откроется.", "A save from an old version of the game: it won't open.");
                case SaveProblem.Newer: return T("Сохранение из более новой версии: обнови игру, чтобы его открыть.", "A save from a newer version: update the game to open it.");
                default: return T("Файл сохранения повреждён: он не откроется.", "The save file is damaged: it won't open.");
            }
        }

        /// <summary>A save row's second line: "ход 34 · лето".</summary>
        public static string SaveTurn(int turn) => T($"ход {turn} · {Season(turn).ToLowerInvariant()}", $"turn {turn} · {Season(turn).ToLowerInvariant()}");

        static string[] MonthsOf => new[]
        {
            T("января", "January"), T("февраля", "February"), T("марта", "March"), T("апреля", "April"), T("мая", "May"), T("июня", "June"), T("июля", "July"), T("августа", "August"), T("сентября", "September"), T("октября", "October"), T("ноября", "November"), T("декабря", "December"),
        };

        /// <summary>A save row's third line: "сегодня, 15:30 · в игре 1 ч 02 мин".</summary>
        public static string SaveWhen(System.DateTime written, int played)
        {
            System.DateTime local = written.ToLocalTime(), today = System.DateTime.Now.Date;
            string day = local.Date == today ? T("сегодня", "today")
                : local.Date == today.AddDays(-1) ? T("вчера", "yesterday")
                : local.Year == today.Year ? $"{local.Day} {MonthsOf[local.Month - 1]}"
                : $"{local.Day} {MonthsOf[local.Month - 1]} {local.Year}";
            int minutes = played / 60;
            string time = minutes < 1 ? T("меньше минуты", "less than a minute") : minutes < 60 ? T($"{minutes} мин", $"{minutes} min") : T($"{minutes / 60} ч {minutes % 60:00} мин", $"{minutes / 60} h {minutes % 60:00} min");
            return T($"{day}, {local:HH:mm} · в игре {time}", $"{day}, {local:HH:mm} · played {time}");
        }

        // ----- The guiding lines (the interface plan's 3.3) -----

        public static string GuidePickHero => T("Щёлкни своего героя (или F)", "Click your hero (or F)");
        public static string GuideBuildCell => T("Щёлкни клетку с кольцом на своей земле", "Click the ringed tile on your land");

        /// <summary>The line once the cell is chosen: the button to press on the bottom panel.</summary>
        public static string GuidePress(string button) => T($"Внизу нажми «{button}»", $"Below, press “{button}”");

        public static string GuideFight => T("Правой кнопкой — по врагу с кольцом", "Right-click the ringed enemy");
        public static string GuideGo => T("Правой ещё раз — герой идёт (или G)", "Right-click again — the hero goes (or G)");
        public static string GuideScout => T("Подходящих врагов не видно: веди героя в туман правой кнопкой", "No suitable enemies in sight: lead the hero into the fog with the right button");
        public static string GuideChest => T("Правой кнопкой — к сундуку с кольцом", "Right-click the ringed chest");
        public static string GuideChestFog => T("Сундуки прячутся в тумане — пошли героя на разведку", "Chests hide in the fog — send the hero to scout");
        public static string GuideStakeCell => T("Щёлкни клетку с кольцом: туда потянется город", "Click the ringed tile: the town will grow towards it");
        public static string GuideTownCell => T("Щёлкни город с кольцом", "Click the ringed town");
        public static string GuideEnterTown => T("Внизу нажми «Войти в город» (T)", "Below, press “Enter town” (T)");
        public static string GuideTownTab => T("Открой вкладку «Постройки»", "Open the “Buildings” tab");

        /// <summary>The line on the town screen: the building's button in its list.</summary>
        public static string GuideTownPress(string button) => T($"Нажми «{button}»", $"Press “{button}”");

        public static string GuideEndTurn => T("Задания ждут следующего хода — жми «Следующий ход» (Enter)", "The tasks wait for the next turn — press “End turn” (Enter)");

        // The one-time hints, each shown once when it is needed.
        public static string HintAlt => T("Держи Alt — увидишь, кто где стоит", "Hold Alt to see who stands where");
        public static string HintCamera => T("Карта двигается: WASD или тяни правой кнопкой; колесо — ближе и дальше", "The map moves: WASD or drag with the right button; the wheel — closer and farther");
        public static string HintArmy => T("Войска нет: зайди в город (T), вкладка «Наём»", "No army: enter a town (T), the “Recruit” tab");
        public static string HintTab => T("Tab — следующий город", "Tab — next town");

        public static string EnterTown => T("Войти в город", "Enter town");
        public static string TabBuildings => T("Постройки", "Buildings");

        // ----- The help's three pages and the return to a game (the interface plan's 3.3) -----
        public static string HelpTitle => T("Как играть", "How to play");
        public static string HelpMain => T("Главное", "Basics");
        public static string HelpKeys => T("Клавиши", "Keys");
        public static string HelpHints => T("Подсказки", "Hints");

        /// <summary>The first page: five things with pictures, as few as one screen of text teaches.</summary>
        public static readonly (string icon, string head, string line)[] HelpBasics =
        {
            ("icon_hero", T("Выбрать", "Select"), T("Левой кнопкой — клетку, город или героя. Внизу появится его карточка с кнопками.", "Left button — a tile, a town or a hero. Its card with buttons appears below.")),
            ("icon_moves", T("Приказать", "Order"), T("Правой кнопкой — путь выбранному герою, правой ещё раз — идти. Путь на врага ведёт в бой.", "Right button — a path for the selected hero, right again — go. A path onto an enemy leads into battle.")),
            ("hammer", T("Строить", "Build"), T("На своей клетке с ресурсом — кнопка внизу; в городе (T) — вкладка «Постройки».", "On your tile with a resource — the button below; in a town (T) — the “Buildings” tab.")),
            ("icon_growth", T("Ход", "Turn"), T("«Следующий ход» (Enter): казна пополняется, города растут и сами берут землю вокруг.", "“End turn” (Enter): the treasury fills up, towns grow and take the land around them.")),
            ("crown", T("Цель", "Goal"), T("Собери корону: три самоцвета из пяти раньше соседей. K — подвиги короны.", "Complete the crown: three gems of five before the neighbours. K — the crown's deeds.")),
        };

        public static string HelpNote => T("Цветные линии — границы королевств. Твоё королевство — то, чей герб наверху слева. ", "Coloured lines are the kingdoms' borders. Your kingdom is the one whose arms are at the top left. ") +
            T("Туман скрывает земли, где не бывали твои герои.", "The fog hides the lands your heroes have not visited.");
        public static string HelpHintsNone => T("Здесь соберутся подсказки, которые ты уже видел в игре.", "Hints you have already seen in the game will gather here.");
        public static string SeenBuild => T("Строить: щёлкни клетку своей земли с ресурсом и внизу нажми кнопку постройки", "Build: click a tile of your land with a resource and press the building button below");
        public static string SeenOrder => T("Герой: щёлкни его (или F), правой кнопкой проложи путь, правой ещё раз (или G) — идти", "Hero: click them (or F), lay a path with the right button, right again (or G) — go");
        public static string SeenChest => T("Сундуки прячутся в тумане — разведывай землю героем", "Chests hide in the fog — scout the land with a hero");
        public static string SeenStake => T("Вешка: щёлкни дикую клетку и нажми «Поставить вешку» — ближайший город потянется туда", "Stake: click a wild tile and press “Place a stake” — the nearest town will reach for it");
        public static string SeenTownBuild => T("Постройки города: войди в город (T), вкладка «Постройки»", "Town buildings: enter the town (T), the “Buildings” tab");
        public static string SeenEndTurn => T("Когда задания ждут следующего хода, кнопка «Следующий ход» зовёт золотой рамкой (Enter)", "When the tasks wait for the next turn, the “End turn” button calls with a gold frame (Enter)");
        public static string Returned(int turn) => T($"Ход {turn}. В прошлый ход:", $"Turn {turn}. Last turn:");

        /// <summary>How dangerous a group is for a kingdom's first army, from 1 to 3.</summary>
        public static int Danger(Monster monster, int count)
        {
            float share = count * Units.Worth(Units.Of(monster)) / Wilds.StartWorth;
            return share < 0.7f ? 1 : share < 1.6f ? 2 : 3;
        }

        /// <summary>Russian plural: 1 дом, 2 дома, 5 домов.</summary>
        public static string Plural(int n, string one, string few, string many)
        {
            n = System.Math.Abs(n) % 100;
            if (n >= 11 && n <= 14)
                return many;
            switch (n % 10)
            {
                case 1: return one;
                case 2: case 3: case 4: return few;
                default: return many;
            }
        }
    }
}
