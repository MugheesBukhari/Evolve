using AStarPathfinding;
using GSS.RealtimeNetworking.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grid = AStarPathfinding.Grid;

using Vector2Int = AStarPathfinding.Vector2Int;

namespace GSS.Evolve
{
    public class Battle
    {
        public long id = 0;
        public DateTime baseTime = DateTime.Now;
        public int frameCount = 0;
        public long defender = 0;
        public long attacker = 0;
        public List<Building> _buildings = new List<Building>();
        public List<Unit> _units = new List<Unit>();
        public List<Hero> _heroes = new List<Hero>();
        public List<OpHero> _opHeroes = new List<OpHero>();
        public List<Spell> _spells = new List<Spell>();
        public List<UnitToAdd> _unitsToAdd = new List<UnitToAdd>();
        public List<HeroToAdd> _heroesToAdd = new List<HeroToAdd>();
        public List<OpHeroToAdd> _OpheroesToAdd = new List<OpHeroToAdd>();
        public List<SpellToAdd> _spellsToAdd = new List<SpellToAdd>();
        private Grid grid = null;
        private Grid unlimitedGrid = null;
        private AStarSearch search = null;
        private AStarSearch unlimitedSearch = null;
        private List<Tile> blockedTiles = new List<Tile>();
        public List<Projectile> projectiles = new List<Projectile>();
        public double percentage = 0;
        public bool end = false;
        public bool surrender = false;
        public int surrenderFrame = 0;
        public float duration = 0;

        public int unitsDeployed = 0;
        public int herosDeployed = 0;
        public bool townhallDestroyed = false;
        public bool fiftyPercentDestroyed = false;
        public bool completelyDestroyed = false;

        public int winTrophies = 0;
        public int loseTrophies = 0;
        private int projectileCount = 0;
        public int previouslootGold = 0;
        public int previouslootPower = 0;
        public int previouslootWood = 0;
        public (int, int, int, int, int, int) GetlootedResources()
        {
            int totalGold = 0;
            int totalPower = 0;
            int totalWood = 0;
            int lootedGold = 0;
            int lootedPower = 0;
            int lootedWood = 0;
            for (int i = 0; i < _buildings.Count; i++)
            {
                switch (_buildings[i].building.id)
                {
                    case Data.BuildingID.commandcenter:
                        totalGold += _buildings[i].lootGoldStorage;
                        lootedGold += _buildings[i].lootedGold;
                        totalPower += _buildings[i].lootPowerStorage;
                        lootedPower += _buildings[i].lootedPower;
                        totalWood += _buildings[i].lootWoodStorage;
                        lootedWood += _buildings[i].lootedWood;
                        break;
                    case Data.BuildingID.mine:
                    case Data.BuildingID.vault:
                        totalGold += _buildings[i].lootGoldStorage;
                        lootedGold += _buildings[i].lootedGold;
                        break;
                    case Data.BuildingID.powerplant:
                    case Data.BuildingID.battery:
                        totalPower += _buildings[i].lootPowerStorage;
                        lootedPower += _buildings[i].lootedPower;
                        break;
                    case Data.BuildingID.gloommill:
                    case Data.BuildingID.warehouse:
                        totalWood += _buildings[i].lootWoodStorage;
                        lootedWood += _buildings[i].lootedWood;
                        break;
                }
            }
            if (previouslootGold < lootedGold)
            {
                Player.instanse.gold += lootedGold - previouslootGold;
                Packet packet = new Packet();
                packet.Write((int)Player.RequestsID.PVEENEMYKILLED);
                packet.Write(lootedGold - previouslootGold);
                packet.Write(0);
                packet.Write(0);
                Sender.TCP_Send(packet);
                previouslootGold = lootedGold;
            }
            if (previouslootPower < lootedPower)
            {
                Player.instanse.power += lootedPower - previouslootPower;
                Packet packet = new Packet();
                packet.Write((int)Player.RequestsID.PVEENEMYKILLED);
                packet.Write(0);
                packet.Write(lootedPower - previouslootPower);
                packet.Write(0);
                Sender.TCP_Send(packet);
                previouslootPower = lootedPower;
            }
            if (previouslootWood < lootedWood)
            {
                Player.instanse.wood += lootedWood - previouslootWood;
                Packet packet = new Packet();
                packet.Write((int)Player.RequestsID.PVEENEMYKILLED);
                packet.Write(0);
                packet.Write(0);
                packet.Write(lootedWood - previouslootWood);
                Sender.TCP_Send(packet);
                previouslootWood = lootedWood;
            }
            return (lootedGold, lootedPower, lootedWood, totalGold, totalPower, totalWood);
        }

        public int stars { get { int s = 0; if (townhallDestroyed) { s++; } if (fiftyPercentDestroyed) { s++; } if (completelyDestroyed) { s++; } return s; } }

        public delegate void SpellSpawned(long databaseID, Data.SpellID id, BattleVector2 target, float radius);
        public delegate void Spawned(long id);
        public delegate void AttackCallback(long index, long target);
        public delegate void boolAttackCallback(long index, long target, bool isSomthing);
        public delegate void IndexCallback(long index);
        public delegate void FloatCallback(long index, float value);
        public delegate void DoubleCallback(long index, double value);
        public delegate void BlankCallback();
        public delegate void ProjectileCallback(int id, BattleVector2 current, BattleVector2 target);
        public ProjectileCallback projectileCallback = null;

        public int GetTrophies()
        {
            int s = stars;
            if (s > 0)
            {
                if (s >= 3)
                {
                    return winTrophies;
                }
                else
                {
                    int t = (int)Math.Floor((double)winTrophies / (double)s);
                    return t * s;
                }
            }
            else
            {
                return loseTrophies * -1;
            }
        }

        public class Projectile
        {
            public int id = 0;
            public int target = -1;
            public float damage = 0;
            public float splash = 0;
            public float timer = 0;
            public TargetType type = TargetType.unit;
            public bool heal = false;
            public bool follow = true;
            public BattleVector2 position = new BattleVector2();
        }

        public enum TargetType
        {
            unit, building, hero, opHero
        }

        public class Tile
        {
            public Tile(Data.BuildingID id, BattleVector2Int position, int index = -1)
            {
                this.id = id;
                this.position = position;
                this.index = index;
            }
            public Data.BuildingID id;
            public BattleVector2Int position;
            public int index = -1;
        }

        public class Spell
        {
            public Data.Spell spell = null;
            public IndexCallback pulseCallback = null;
            public IndexCallback doneCallback = null;
            public BattleVector2 position;
            public bool done = false;
            public int palsesDone = 0;
            public double palsesTimer = 0;
            public BattleVector2 positionOnGrid { get { return new BattleVector2(position.x - Data.battleGridOffset, position.y - Data.battleGridOffset); } }
            public void Initialize(int x, int y)
            {
                if (spell == null) { return; }
                position = GridToWorldPosition(new BattleVector2Int(x, y));
            }
        }

        public class Unit
        {
            public Data.Unit unit = null;
            public float health = 0;
            public int target = -1;
            public int mainTarget = -1;
            public BattleVector2 position;
            public BattleVector2 positionOnGrid { get { return new BattleVector2(position.x - Data.battleGridOffset, position.y - Data.battleGridOffset); } }
            public Path path = null;
            public double pathTime = 0;
            public double pathTraveledTime = 0;
            public double attackTimer = 0;
            public bool moving = false;
            public bool isTargerBuilding = false;
            public Dictionary<int, float> resourceTargets = new Dictionary<int, float>();
            public Dictionary<int, float> defenceTargets = new Dictionary<int, float>();
            public Dictionary<int, float> otherTargets = new Dictionary<int, float>();
            public boolAttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
            public Dictionary<int, float> GetAllTargets()
            {
                Dictionary<int, float> temp = new Dictionary<int, float>();
                if (otherTargets.Count > 0)
                {
                    temp = temp.Concat(otherTargets).ToDictionary(x => x.Key, x => x.Value);
                }
                if (resourceTargets.Count > 0)
                {
                    temp = temp.Concat(resourceTargets).ToDictionary(x => x.Key, x => x.Value);
                }
                if (defenceTargets.Count > 0)
                {
                    temp = temp.Concat(defenceTargets).ToDictionary(x => x.Key, x => x.Value);
                }
                return temp;
            }
            public void AssignTarget(int target, Path path)
            {
                attackTimer = unit.attackSpeed;
                this.target = target;
                this.path = path;
                if (path != null)
                {
                    pathTraveledTime = 0;
                    pathTime = path.length / (unit.moveSpeed * Data.gridCellSize);
                }
                if (targetCallback != null)
                {
                    targetCallback.Invoke(unit.databaseID);
                }
            }
            public void AssignHealerTarget(int target, float distance)
            {
                attackTimer = unit.attackSpeed;
                this.target = target;
                pathTraveledTime = 0;
                pathTime = distance / (unit.moveSpeed * Data.gridCellSize);
            }
            public void TakeDamage(float damage)
            {
                if (health <= 0) { return; }
                health -= damage;
                if (damageCallback != null)
                {
                    damageCallback.Invoke(unit.databaseID, damage);
                }
                if (health < 0) { health = 0; }
                if (health <= 0)
                {
                    if (dieCallback != null)
                    {
                        dieCallback.Invoke(unit.databaseID);
                    }
                }
            }
            public void Heal(float amount)
            {
                if (amount <= 0 || health <= 0) { return; }
                health += amount;
                if (health > unit.health) { health = unit.health; }
                if (healCallback != null)
                {
                    healCallback.Invoke(unit.databaseID, amount);
                }
            }
            public void Initialize(int x, int y)
            {
                if (unit == null) { return; }
                position = GridToWorldPosition(new BattleVector2Int(x, y));
            }
        }
        public class Hero
        {
            public Data.Hero hero = null;
            public float health = 0;
            public int target = -1;
            public int mainTarget = -1;
            public BattleVector2 position;
            public BattleVector2 positionOnGrid { get { return new BattleVector2(position.x - Data.battleGridOffset, position.y - Data.battleGridOffset); } }
            public Path path = null;
            public double pathTime = 0;
            public double pathTraveledTime = 0;
            public double attackTimer = 0;
            public bool moving = false;
            public bool isTargerBuilding = false; // Hero's Target will be Building or Op Hero
            public Dictionary<int, float> resourceTargets = new Dictionary<int, float>();
            public Dictionary<int, float> defenceTargets = new Dictionary<int, float>();
            public Dictionary<int, float> otherTargets = new Dictionary<int, float>();
            public boolAttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
            public Dictionary<int, float> GetAllTargets()
            {
                Dictionary<int, float> temp = new Dictionary<int, float>();
                if (otherTargets.Count > 0)
                {
                    temp = temp.Concat(otherTargets).ToDictionary(x => x.Key, x => x.Value);
                }
                if (resourceTargets.Count > 0)
                {
                    temp = temp.Concat(resourceTargets).ToDictionary(x => x.Key, x => x.Value);
                }
                if (defenceTargets.Count > 0)
                {
                    temp = temp.Concat(defenceTargets).ToDictionary(x => x.Key, x => x.Value);
                }
                return temp;
            }
            public void AssignTarget(int target, Path path)
            {
                attackTimer = hero.attackSpeed;
                this.target = target;
                this.path = path;
                if (path != null)
                {
                    pathTraveledTime = 0;
                    pathTime = path.length / (hero.moveSpeed * Data.gridCellSize);
                }
                if (targetCallback != null)
                {
                    targetCallback.Invoke(hero.databaseID);
                }
            }
            public void AssignHealerTarget(int target, float distance)
            {
                attackTimer = hero.attackSpeed;
                this.target = target;
                pathTraveledTime = 0;
                pathTime = distance / (hero.moveSpeed * Data.gridCellSize);
            }
            public void TakeDamage(float damage)
            {
                if (health <= 0) { return; }
                health -= damage;
                if (damageCallback != null)
                {
                    damageCallback.Invoke(hero.databaseID, damage);
                }
                if (health < 0) { health = 0; }
                if (health <= 0)
                {
                    if (dieCallback != null)
                    {
                        dieCallback.Invoke(hero.databaseID);
                    }
                }
            }
            public void Heal(float amount)
            {
                if (amount <= 0 || health <= 0) { return; }
                health += amount;
                if (health > hero.health) { health = hero.health; }
                if (healCallback != null)
                {
                    healCallback.Invoke(hero.databaseID, amount);
                }
            }
            public void Initialize(int x, int y)
            {
                if (hero == null) { return; }
                position = GridToWorldPosition(new BattleVector2Int(x, y));
            }
        }

        public class OpHero
        {
            public Data.Hero opHero = null;
            public float health = 0;
            public int target = -1;
            public bool hasRandomPath = false;
            public int mainTarget = -1;
            public BattleVector2 position;
            public BattleVector2 positionOnGrid { get { return new BattleVector2(position.x - Data.battleGridOffset, position.y - Data.battleGridOffset); } }
            public Path path = null;
            public double pathTime = 0;
            public double pathTraveledTime = 0;
            public double attackTimer = 0;
            public bool moving = false;
            public bool isTargetUnit = true;
            public boolAttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
            public void AssignTarget(int target, Path path)
            {
                // attackTimer = opHero.attackSpeed;
                this.target = target;
                this.path = path;
                if (path != null)
                {
                    pathTraveledTime = 0;
                    pathTime = path.length / (opHero.moveSpeed * Data.gridCellSize);
                }
                if (targetCallback != null)
                {
                    targetCallback.Invoke(opHero.databaseID);
                }
            }
            public void AssignRandomPoint(Path path)
            {
                //attackTimer = opHero.attackSpeed;
                this.path = path;
                if (path != null)
                {
                    pathTraveledTime = 0;
                    pathTime = path.length / (opHero.moveSpeed * Data.gridCellSize);
                    hasRandomPath = true;
                }
                //if (targetCallback != null)
                //{
                //    targetCallback.Invoke(opHero.databaseID);
                //}
            }
            public void AssignHealerTarget(int target, float distance)
            {
                attackTimer = opHero.attackSpeed;
                this.target = target;
                pathTraveledTime = 0;
                pathTime = distance / (opHero.moveSpeed * Data.gridCellSize);
            }
            public void TakeDamage(float damage)
            {
                if (health <= 0) { return; }
                health -= damage;
                if (damageCallback != null)
                {
                    damageCallback.Invoke(opHero.databaseID, damage);
                }
                if (health < 0) { health = 0; }
                if (health <= 0)
                {
                    if (dieCallback != null)
                    {
                        dieCallback.Invoke(opHero.databaseID);
                    }
                }
            }
            public void Heal(float amount)
            {
                if (amount <= 0 || health <= 0) { return; }
                health += amount;
                if (health > opHero.health) { health = opHero.health; }
                if (healCallback != null)
                {
                    healCallback.Invoke(opHero.databaseID, amount);
                }
            }
            public void Initialize(int x, int y)
            {
                if (opHero == null) { return; }
                position = GridToWorldPosition(new BattleVector2Int(x, y));
            }
        }

        public class Building
        {
            public Data.Building building = null;
            public float health = 0;
            public int target = -1;
            public double attackTimer = 0;
            public double percentage = 0;
            public BattleVector2 worldCenterPosition;
            public AttackCallback attackCallback = null;
            public DoubleCallback destroyCallback = null;
            public FloatCallback damageCallback = null;
            public BlankCallback starCallback = null;
            public bool isTargetUnit = true;
            public int lootGoldStorage = 0;
            public int lootPowerStorage = 0;
            public int lootWoodStorage = 0;

            public int lootedGold = 0;
            public int lootedPower = 0;
            public int lootedWood = 0;

            public void TakeDamage(float damage, ref Grid grid, ref List<Tile> blockedTiles, ref double percentage, ref bool fiftySatar, ref bool hallStar, ref bool allStar)
            {
                if (health <= 0) { return; }
                health -= damage;
                //if (damageCallback != null)
                //{
                //    damageCallback.Invoke(building.databaseID, damage);
                //}
                if (health < 0) { health = 0; }

                double loot = 1d - ((double)health / (double)building.health);
                if (lootGoldStorage > 0) { lootedGold = (int)Math.Floor(lootGoldStorage * loot); }
                if (lootPowerStorage > 0) { lootedPower = (int)Math.Floor(lootPowerStorage * loot); }
                if (lootWoodStorage > 0) { lootedWood = (int)Math.Floor(lootWoodStorage * loot); }
                if (lootGoldStorage > 0 || lootPowerStorage > 0 || lootWoodStorage > 0)
                {
                    if (damageCallback != null)
                    {
                        damageCallback.Invoke(building.databaseID, damage);
                    }
                }
                if (health <= 0)
                {
                    for (int x = building.x; x < building.x + building.columns; x++)
                    {
                        for (int y = building.y; y < building.y + building.rows; y++)
                        {
                            grid[x, y].Blocked = false;
                            for (int i = 0; i < blockedTiles.Count; i++)
                            {
                                if (blockedTiles[i].position.x == x && blockedTiles[i].position.y == y)
                                {
                                    blockedTiles.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }
                    if (this.percentage > 0)
                    {
                        percentage += this.percentage;
                    }
                    if (destroyCallback != null)
                    {
                        destroyCallback.Invoke(building.databaseID, this.percentage);
                    }
                    if (building.id == Data.BuildingID.commandcenter && !hallStar)
                    {
                        hallStar = true;
                        if (starCallback != null)
                        {
                            starCallback.Invoke();
                        }
                    }
                    int p = (int)Math.Floor(percentage * 100d);
                    if (p >= 50 && !fiftySatar)
                    {
                        fiftySatar = true;
                        if (starCallback != null)
                        {
                            starCallback.Invoke();
                        }
                    }
                    if (p >= 100 && !allStar)
                    {
                        allStar = true;
                        if (starCallback != null)
                        {
                            starCallback.Invoke();
                        }
                    }
                }
            }
            public void Initialize()
            {
                health = building.health;
                percentage = building.percentage;
                lootedGold = 0;
                lootedPower = 0;
                lootedWood = 0;
            }
        }

        public class UnitToAdd
        {
            public Unit unit = null;
            public int x;
            public int y;
            public Spawned callback = null;
            public AttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
        }
        public class HeroToAdd
        {
            public Hero hero = null;
            public int x;
            public int y;
            public Spawned callback = null;
            public AttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
        }
        public class OpHeroToAdd
        {
            public OpHero opHero = null;
            public int x;
            public int y;
            public Spawned callback = null;
            public AttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
        }

        public class SpellToAdd
        {
            public Spell spell = null;
            public int x;
            public int y;
            public SpellSpawned callback = null;
        }

        public void Initialize(List<Building> buildings, DateTime time, AttackCallback attackCallback = null, DoubleCallback destroyCallback = null, FloatCallback damageCallback = null, BlankCallback starGained = null, ProjectileCallback projectileCallback = null)
        {
            baseTime = time;
            duration = Data.battleDuration;
            frameCount = 0;
            percentage = 0;
            unitsDeployed = 0;
            herosDeployed = 0;
            fiftyPercentDestroyed = false;
            townhallDestroyed = false;
            completelyDestroyed = false;
            end = false;
            projectileCount = 0;
            surrender = false;
            this.projectileCallback = projectileCallback;
            _buildings = buildings;
            grid = new Grid(Data.gridSize + (Data.battleGridOffset * 2), Data.gridSize + (Data.battleGridOffset * 2));
            unlimitedGrid = new Grid(Data.gridSize + (Data.battleGridOffset * 2), Data.gridSize + (Data.battleGridOffset * 2));
            search = new AStarSearch(grid);
            unlimitedSearch = new AStarSearch(unlimitedGrid);
            for (int i = 0; i < _buildings.Count; i++)
            {
                _buildings[i].attackCallback = attackCallback;
                _buildings[i].destroyCallback = destroyCallback;
                _buildings[i].damageCallback = damageCallback;
                _buildings[i].starCallback = starGained;

                _buildings[i].Initialize();
                _buildings[i].worldCenterPosition = new BattleVector2((_buildings[i].building.x + (_buildings[i].building.columns / 2f)) * Data.gridCellSize, (_buildings[i].building.y + (_buildings[i].building.rows / 2f)) * Data.gridCellSize);

                int startX = _buildings[i].building.x;
                int endX = _buildings[i].building.x + _buildings[i].building.columns;

                int startY = _buildings[i].building.y;
                int endY = _buildings[i].building.y + _buildings[i].building.rows;

                if (_buildings[i].building.id != Data.BuildingID.wall && _buildings[i].building.columns > 1 && _buildings[i].building.rows > 1)
                {
                    startX++;
                    startY++;
                    endX--;
                    endY--;
                    if (endX <= startX || endY <= startY)
                    {
                        continue;
                    }
                }

                for (int x = startX; x < endX; x++)
                {
                    for (int y = startY; y < endY; y++)
                    {
                        grid[x, y].Blocked = true;
                        blockedTiles.Add(new Tile(_buildings[i].building.id, new BattleVector2Int(x, y), i));
                    }
                }
            }
        }

        public bool IsAliveUnitsOnGrid()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].health > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public bool IsAliveHeroesOnGrid()
        {
            for (int i = 0; i < _heroes.Count; i++)
            {
                if (_heroes[i].health > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CanBattleGoOn()
        {
            if (Math.Abs(percentage - 1d) > 0.0001d && (IsAliveUnitsOnGrid() || IsAliveHeroesOnGrid()))
            {
                double time = (float)frameCount * Data.battleFrameRate;
                if (time < duration)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CanAddUnit(int x, int y)
        {
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;

            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].health <= 0)
                {
                    continue;
                }

                int startX = _buildings[i].building.x;
                int endX = _buildings[i].building.x + _buildings[i].building.columns;

                int startY = _buildings[i].building.y;
                int endY = _buildings[i].building.y + _buildings[i].building.rows;

                for (int x2 = startX; x2 < endX; x2++)
                {
                    for (int y2 = startY; y2 < endY; y2++)
                    {
                        if (x == x2 && y == y2)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        public bool CanAddHero(int x, int y)
        {
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;

            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].health <= 0)
                {
                    continue;
                }

                int startX = _buildings[i].building.x;
                int endX = _buildings[i].building.x + _buildings[i].building.columns;

                int startY = _buildings[i].building.y;
                int endY = _buildings[i].building.y + _buildings[i].building.rows;

                for (int x2 = startX; x2 < endX; x2++)
                {
                    for (int y2 = startY; y2 < endY; y2++)
                    {
                        if (x == x2 && y == y2)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public bool CanAddSpell(int x, int y)
        {
            return true;
        }

        public void AddUnit(Data.Unit unit, int x, int y, Spawned callback = null, boolAttackCallback attackCallback = null, IndexCallback dieCallback = null, FloatCallback damageCallback = null, FloatCallback healCallback = null, IndexCallback targetCallback = null)
        {
            if (end)
            {
                return;
            }
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;
            UnitToAdd unitToAdd = new UnitToAdd();
            unitToAdd.callback = callback;
            Unit battleUnit = new Unit();
            battleUnit.attackCallback = attackCallback;
            battleUnit.dieCallback = dieCallback;
            battleUnit.damageCallback = damageCallback;
            battleUnit.healCallback = healCallback;
            battleUnit.targetCallback = targetCallback;
            battleUnit.unit = unit;
            battleUnit.Initialize(x, y);
            battleUnit.health = unit.health;
            unitToAdd.unit = battleUnit;
            unitToAdd.x = x;
            unitToAdd.y = y;
            _unitsToAdd.Add(unitToAdd);
        }
        public void AddHero(Data.Hero hero, int x, int y, Spawned callback = null, boolAttackCallback attackCallback = null, IndexCallback dieCallback = null, FloatCallback damageCallback = null, FloatCallback healCallback = null, IndexCallback targetCallback = null)
        {
            if (end)
            {
                return;
            }
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;
            HeroToAdd heroToAdd = new HeroToAdd();
            heroToAdd.callback = callback;
            Hero battleHero = new Hero();
            battleHero.attackCallback = attackCallback;
            battleHero.dieCallback = dieCallback;
            battleHero.damageCallback = damageCallback;
            battleHero.healCallback = healCallback;
            battleHero.targetCallback = targetCallback;
            battleHero.hero = hero;
            battleHero.Initialize(x, y);
            battleHero.health = hero.health;
            heroToAdd.hero = battleHero;
            heroToAdd.x = x;
            heroToAdd.y = y;
            _heroesToAdd.Add(heroToAdd);
        }
        public void AddOpHero(Data.Hero hero, int x, int y, Spawned callback = null, boolAttackCallback attackCallback = null, IndexCallback dieCallback = null, FloatCallback damageCallback = null, FloatCallback healCallback = null, IndexCallback targetCallback = null)
        {
            if (end)
            {
                return;
            }
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;
            OpHeroToAdd opHeroToAdd = new OpHeroToAdd();
            opHeroToAdd.callback = callback;
            OpHero battleOpHero = new OpHero();
            battleOpHero.attackCallback = attackCallback;
            battleOpHero.dieCallback = dieCallback;
            battleOpHero.damageCallback = damageCallback;
            battleOpHero.healCallback = healCallback;
            battleOpHero.targetCallback = targetCallback;
            battleOpHero.opHero = hero;
            battleOpHero.Initialize(x, y);
            battleOpHero.health = hero.health;
            opHeroToAdd.opHero = battleOpHero;
            opHeroToAdd.x = x;
            opHeroToAdd.y = y;
            _OpheroesToAdd.Add(opHeroToAdd);
        }

        public void AddSpell(Data.Spell spell, int x, int y, SpellSpawned callback = null, IndexCallback pulseCallback = null, IndexCallback doneCallback = null)
        {
            if (end)
            {
                return;
            }
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;
            SpellToAdd spellToAdd = new SpellToAdd();
            spellToAdd.callback = callback;
            Spell battleSpell = new Spell();
            battleSpell.doneCallback = doneCallback;
            battleSpell.pulseCallback = pulseCallback;
            battleSpell.spell = spell;
            battleSpell.Initialize(x, y);
            spellToAdd.spell = battleSpell;
            spellToAdd.x = x;
            spellToAdd.y = y;
            _spellsToAdd.Add(spellToAdd);
        }

        public void ExecuteFrame()
        {
            int addIndex = _units.Count;
            for (int i = _unitsToAdd.Count - 1; i >= 0; i--)
            {
                /*
                if (CanAddUnit(_unitsToAdd[i].x, _unitsToAdd[i].y))
                {
                    
                }*/
                unitsDeployed += _unitsToAdd[i].unit.unit.housing;
                _unitsToAdd[i].x += Data.battleGridOffset;
                _unitsToAdd[i].y += Data.battleGridOffset;
                _units.Insert(addIndex, _unitsToAdd[i].unit);

                addIndex++;
                if (_unitsToAdd[i].callback != null)
                {
                    _unitsToAdd[i].callback.Invoke(_unitsToAdd[i].unit.unit.databaseID);
                }
                _unitsToAdd.RemoveAt(i);
            }

            addIndex = _heroes.Count;
            for (int i = _heroesToAdd.Count - 1; i >= 0; i--)
            {
                herosDeployed += 1;
                _heroesToAdd[i].x += Data.battleGridOffset;
                _heroesToAdd[i].y += Data.battleGridOffset;
                _heroes.Insert(addIndex, _heroesToAdd[i].hero);

                addIndex++;
                if (_heroesToAdd[i].callback != null)
                {
                    _heroesToAdd[i].callback.Invoke(_heroesToAdd[i].hero.hero.databaseID);
                }
                _heroesToAdd.RemoveAt(i);
            }
            addIndex = _opHeroes.Count;
            for (int i = _OpheroesToAdd.Count - 1; i >= 0; i--)
            {
                _OpheroesToAdd[i].x += Data.battleGridOffset;
                _OpheroesToAdd[i].y += Data.battleGridOffset;
                _opHeroes.Insert(addIndex, _OpheroesToAdd[i].opHero);

                addIndex++;
                if (_OpheroesToAdd[i].callback != null)
                {
                    _OpheroesToAdd[i].callback.Invoke(_OpheroesToAdd[i].opHero.opHero.databaseID);
                }
                _OpheroesToAdd.RemoveAt(i);
            }

            addIndex = _spells.Count;
            for (int i = _spellsToAdd.Count - 1; i >= 0; i--)
            {
                _spellsToAdd[i].x += Data.battleGridOffset;
                _spellsToAdd[i].y += Data.battleGridOffset;
                _spells.Insert(addIndex, _spellsToAdd[i].spell);
                if (_spellsToAdd[i].callback != null)
                {
                    _spellsToAdd[i].callback.Invoke(_spellsToAdd[i].spell.spell.databaseID, _spellsToAdd[i].spell.spell.id, _spells[addIndex].positionOnGrid, _spellsToAdd[i].spell.spell.server.radius);
                }
                addIndex++;
                _spellsToAdd.RemoveAt(i);
            }

            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].building.targetType != Data.BuildingTargetType.none && _buildings[i].health > 0)
                {
                    HandleBuilding(i, Data.battleFrameRate);
                }
            }

            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].health > 0)
                {
                    HandleUnit(i, Data.battleFrameRate);
                }
            }
            for (int i = 0; i < _heroes.Count; i++)
            {
                if (_heroes[i].health > 0)
                {
                    HandleHero(i, Data.battleFrameRate);
                }
            }
            for (int i = 0; i < _opHeroes.Count; i++)
            {
                if (_opHeroes[i].health > 0)
                {
                    HandleOpHero(i, Data.battleFrameRate);
                }
            }

            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done == false)
                {
                    HandleSpell(i, Data.battleFrameRate);
                }
            }

            if (projectiles.Count > 0)
            {
                for (int i = projectiles.Count - 1; i >= 0; i--)
                {
                    projectiles[i].timer -= Data.battleFrameRate;
                    if (projectiles[i].timer <= 0)
                    {
                        if (projectiles[i].type == TargetType.unit)
                        {
                            if (projectiles[i].heal)
                            {
                                _units[projectiles[i].target].Heal(projectiles[i].damage);
                                for (int j = 0; j < _units.Count; j++)
                                {
                                    if (_units[j].health <= 0 || j == projectiles[i].target || _units[j].unit.movement == Data.UnitMoveType.fly)
                                    {
                                        continue;
                                    }
                                    float distance = BattleVector2.Distance(_units[j].position, _units[projectiles[i].target].position);
                                    if (distance < projectiles[i].splash * Data.gridCellSize)
                                    {
                                        _units[j].Heal(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                    }
                                }
                            }
                            else
                            {
                                _units[projectiles[i].target].TakeDamage(projectiles[i].damage);
                                if (projectiles[i].splash > 0)
                                {
                                    for (int j = 0; j < _units.Count; j++)
                                    {
                                        if (j != projectiles[i].target)
                                        {
                                            float distance = BattleVector2.Distance(_units[j].position, _units[projectiles[i].target].position);
                                            if (distance < projectiles[i].splash * Data.gridCellSize)
                                            {
                                                _units[j].TakeDamage(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (projectiles[i].type == TargetType.hero)
                        {
                            if (projectiles[i].heal)
                            {
                                _heroes[projectiles[i].target].Heal(projectiles[i].damage);
                                for (int j = 0; j < _heroes.Count; j++)
                                {
                                    if (_heroes[j].health <= 0 || j == projectiles[i].target || _heroes[j].hero.movement == Data.HeroMoveType.fly)
                                    {
                                        continue;
                                    }
                                    float distance = BattleVector2.Distance(_heroes[j].position, _heroes[projectiles[i].target].position);
                                    if (distance < projectiles[i].splash * Data.gridCellSize)
                                    {
                                        _heroes[j].Heal(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                    }
                                }
                            }
                            else
                            {
                                _heroes[projectiles[i].target].TakeDamage(projectiles[i].damage);
                                if (projectiles[i].splash > 0)
                                {
                                    for (int j = 0; j < _heroes.Count; j++)
                                    {
                                        if (j != projectiles[i].target)
                                        {
                                            float distance = BattleVector2.Distance(_heroes[j].position, _heroes[projectiles[i].target].position);
                                            if (distance < projectiles[i].splash * Data.gridCellSize)
                                            {
                                                _heroes[j].TakeDamage(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (projectiles[i].type == TargetType.opHero)
                        {
                            if (projectiles[i].heal)
                            {
                                _opHeroes[projectiles[i].target].Heal(projectiles[i].damage);
                                for (int j = 0; j < _opHeroes.Count; j++)
                                {
                                    if (_opHeroes[j].health <= 0 || j == projectiles[i].target || _opHeroes[j].opHero.movement == Data.HeroMoveType.fly)
                                    {
                                        continue;
                                    }
                                    float distance = BattleVector2.Distance(_opHeroes[j].position, _opHeroes[projectiles[i].target].position);
                                    if (distance < projectiles[i].splash * Data.gridCellSize)
                                    {
                                        _opHeroes[j].Heal(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                    }
                                }
                            }
                            else
                            {
                                _opHeroes[projectiles[i].target].TakeDamage(projectiles[i].damage);
                                if (projectiles[i].splash > 0)
                                {
                                    for (int j = 0; j < _opHeroes.Count; j++)
                                    {
                                        if (j != projectiles[i].target)
                                        {
                                            float distance = BattleVector2.Distance(_opHeroes[j].position, _opHeroes[projectiles[i].target].position);
                                            if (distance < projectiles[i].splash * Data.gridCellSize)
                                            {
                                                _opHeroes[j].TakeDamage(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            _buildings[projectiles[i].target].TakeDamage(projectiles[i].damage, ref grid, ref blockedTiles, ref percentage, ref fiftyPercentDestroyed, ref townhallDestroyed, ref completelyDestroyed);
                        }
                        projectiles.RemoveAt(i);
                    }
                }
            }

            frameCount++;
        }
        bool IsTargetValid(int index)
        {
            int targetIndex = _buildings[index].target;

            // Check if target index is valid for _units
            if (targetIndex >= 0 && targetIndex < _units.Count)
            {
                var targetUnit = _units[targetIndex];

                // Check if the target unit is not null and its health is greater than zero
                if (targetUnit != null && targetUnit.health > 0)
                {
                    // Check if the target unit is in range
                    if (IsUnitInRange(targetIndex, index))
                    {
                        // Check if the target unit is not underground with a path
                        if (!(targetUnit.unit.movement == Data.UnitMoveType.underground && targetUnit.path != null))
                        {
                            // A valid unit is found
                            return false;
                        }
                    }
                }
            }

            // Check if target index is valid for _heroes
            if (targetIndex >= 0 && targetIndex < _heroes.Count)
            {
                var targetHero = _heroes[targetIndex];

                // Check if the target hero is not null and its health is greater than zero
                if (targetHero != null && targetHero.health > 0)
                {
                    // Check if the target hero is in range
                    if (IsHeroInRange(targetIndex, index))
                    {
                        // Check if the target hero is not underground with a path
                        if (!(targetHero.hero.movement == Data.HeroMoveType.underground && targetHero.path != null))
                        {
                            // A valid hero is found
                            return false;
                        }
                    }
                }
            }

            // No valid unit or hero is found
            return true;
        }
        private void HandleBuilding(int index, double deltaTime)
        {
            if (_buildings[index].target >= 0)
            {
                //bool isTargetValid = true;
                //int targetIndex = _buildings[index].target;

                //// Check if target index is valid for _units
                //if (targetIndex >= 0 && targetIndex < _units.Count)
                //{
                //    var targetUnit = _units[targetIndex];

                //    // Check if the target unit is null or its health is zero
                //    if (targetUnit == null || targetUnit.health <= 0)
                //    {
                //        isTargetValid = false;
                //    }

                //    // Check if the target unit is out of range
                //    if (isTargetValid && !IsUnitInRange(targetIndex, index))
                //    {
                //        isTargetValid = false;
                //    }

                //    // Check if the target unit is underground and has a path
                //    if (isTargetValid && targetUnit.unit.movement == Data.UnitMoveType.underground && targetUnit.path != null)
                //    {
                //        isTargetValid = false;
                //    }
                //}

                //// Check if target index is valid for _heroes
                //if (isTargetValid && targetIndex >= 0 && targetIndex < _heroes.Count)
                //{
                //    var targetHero = _heroes[targetIndex];

                //    // Check if the target hero is null or its health is zero
                //    if (targetHero == null || targetHero.health <= 0)
                //    {
                //        isTargetValid = false;
                //    }

                //    // Check if the target hero is out of range
                //    if (isTargetValid && !IsHeroInRange(targetIndex, index))
                //    {
                //        isTargetValid = false;
                //    }

                //    // Check if the target hero is underground and has a path
                //    if (isTargetValid && targetHero.hero.movement == Data.HeroMoveType.underground && targetHero.path != null)
                //    {
                //        isTargetValid = false;
                //    }
                //}

                //// Final check for the validity of the target
                ////{

                //if (_units[_buildings[index].target].health <= 0 || !IsUnitInRange(_buildings[index].target, index) || _heroes[_buildings[index].target].health <= 0 || !IsHeroInRange(_buildings[index].target, index) || ((_units[_buildings[index].target].unit.movement == Data.UnitMoveType.underground && _units[_buildings[index].target].path != null) || (_heroes[_buildings[index].target].hero.movement == Data.HeroMoveType.underground && _heroes[_buildings[index].target].path != null)))
                if (IsTargetValid(index))
                {
                    // If the building's target is dead or not in range then remove it as target
                    _buildings[index].target = -1;
                }
                else
                {
                    // Building has a target
                    bool freeze = false;
                    for (int i = 0; i < _spells.Count; i++)
                    {
                        if (_spells[i].done) { continue; }
                        if (_spells[i].spell.id == Data.SpellID.rotwood)
                        {
                            double p = GetBuildingInSpellRangePercentage(i, index);
                            if (p > 0)
                            {
                                freeze = true;
                                break;
                            }
                        }
                    }
                    if (!freeze)
                    {

                        //if (IsUnitCanBeSeen(_buildings[index].target, index))
                        //{
                        if (IsUnitInRange(_buildings[index].target, index))
                        {
                            _buildings[index].attackTimer += deltaTime;
                            int attacksCount = (int)Math.Floor(_buildings[index].attackTimer / _buildings[index].building.speed);
                            if (attacksCount > 0)
                            {
                                _buildings[index].attackTimer -= (attacksCount * _buildings[index].building.speed);
                                for (int i = 1; i <= attacksCount; i++)
                                {
                                    if (_buildings[index].building.radius > 0 && _buildings[index].building.rangedSpeed > 0)
                                    {
                                        float distance = BattleVector2.Distance(_units[_buildings[index].target].position, _buildings[index].worldCenterPosition);
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.unit;
                                        projectile.target = _buildings[index].target;
                                        projectile.timer = distance / (_buildings[index].building.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = _buildings[index].building.damage;
                                        projectile.splash = _buildings[index].building.splashRange;
                                        projectile.follow = true;
                                        projectile.position = _buildings[index].worldCenterPosition;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _buildings[index].worldCenterPosition, _units[_buildings[index].target].position);
                                        }
                                    }
                                    else
                                    {
                                        _units[_buildings[index].target].TakeDamage(_buildings[index].building.damage);
                                        if (_buildings[index].building.splashRange > 0)
                                        {
                                            for (int j = 0; j < _units.Count; j++)
                                            {
                                                if (j != _buildings[index].target)
                                                {
                                                    float distance = BattleVector2.Distance(_units[j].position, _units[_buildings[index].target].position);
                                                    if (distance < _buildings[index].building.splashRange * Data.gridCellSize)
                                                    {
                                                        _units[j].TakeDamage(_buildings[index].building.damage * (1f - (distance / _buildings[index].building.splashRange * Data.gridCellSize)));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_buildings[index].attackCallback != null)
                                    {
                                        _buildings[index].attackCallback.Invoke(_buildings[index].building.databaseID, _units[_buildings[index].target].unit.databaseID);
                                    }
                                }
                            }
                        }
                        else if (IsHeroInRange(_buildings[index].target, index))
                        {
                            _buildings[index].attackTimer += deltaTime;
                            int attacksCount = (int)Math.Floor(_buildings[index].attackTimer / _buildings[index].building.speed);
                            if (attacksCount > 0)
                            {
                                _buildings[index].attackTimer -= (attacksCount * _buildings[index].building.speed);
                                for (int i = 1; i <= attacksCount; i++)
                                {
                                    if (_buildings[index].building.radius > 0 && _buildings[index].building.rangedSpeed > 0)
                                    {
                                        float distance = BattleVector2.Distance(_heroes[_buildings[index].target].position, _buildings[index].worldCenterPosition);
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.hero;
                                        projectile.target = _buildings[index].target;
                                        projectile.timer = distance / (_buildings[index].building.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = _buildings[index].building.damage;
                                        projectile.splash = _buildings[index].building.splashRange;
                                        projectile.follow = true;
                                        projectile.position = _buildings[index].worldCenterPosition;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _buildings[index].worldCenterPosition, _heroes[_buildings[index].target].position);
                                        }
                                    }
                                    else
                                    {
                                        _heroes[_buildings[index].target].TakeDamage(_buildings[index].building.damage);
                                        if (_buildings[index].building.splashRange > 0)
                                        {
                                            for (int j = 0; j < _heroes.Count; j++)
                                            {
                                                if (j != _buildings[index].target)
                                                {
                                                    float distance = BattleVector2.Distance(_heroes[j].position, _heroes[_buildings[index].target].position);
                                                    if (distance < _buildings[index].building.splashRange * Data.gridCellSize)
                                                    {
                                                        _heroes[j].TakeDamage(_buildings[index].building.damage * (1f - (distance / _buildings[index].building.splashRange * Data.gridCellSize)));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_buildings[index].attackCallback != null)
                                    {
                                        _buildings[index].attackCallback.Invoke(_buildings[index].building.databaseID, _heroes[_buildings[index].target].hero.databaseID);
                                    }
                                }
                            }
                        }
                        //}
                        //else
                        //{
                        //    _buildings[index].target = -1;
                        //}
                    }
                }
            }
            if (_buildings[index].target < 0)
            {
                // Find a new target for this building
                if (FindTargetForBuilding(index, false))
                {
                    HandleBuilding(index, deltaTime);
                }

                if (FindTargetForBuilding(index, true))
                {
                    HandleBuilding(index, deltaTime);
                }
            }
        }

        private bool FindTargetForBuilding(int index, bool isUnit)
        {
            if (isUnit)
            {

                for (int i = 0; i < _units.Count; i++)
                {
                    if (_units[i].health <= 0 || _units[i].unit.movement == Data.UnitMoveType.underground && _units[i].path != null)
                    {
                        continue;
                    }

                    if (_buildings[index].building.targetType == Data.BuildingTargetType.ground && _units[i].unit.movement == Data.UnitMoveType.fly)
                    {
                        continue;
                    }

                    if (_buildings[index].building.targetType == Data.BuildingTargetType.air && _units[i].unit.movement != Data.UnitMoveType.fly)
                    {
                        continue;
                    }

                    if (IsUnitInRange(i, index) /*&& IsUnitCanBeSeen(i, index)*/)
                    {
                        //_buildings[index].attackTimer = _buildings[index].building.speed;
                        _buildings[index].target = i;
                        return true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < _heroes.Count; i++)
                {
                    if (_heroes[i].health <= 0 || _heroes[i].hero.movement == Data.HeroMoveType.underground && _heroes[i].path != null)
                    {
                        continue;
                    }

                    if (_buildings[index].building.targetType == Data.BuildingTargetType.ground && _heroes[i].hero.movement == Data.HeroMoveType.fly)
                    {
                        continue;
                    }

                    if (_buildings[index].building.targetType == Data.BuildingTargetType.air && _heroes[i].hero.movement != Data.HeroMoveType.fly)
                    {
                        continue;
                    }

                    if (IsHeroInRange(i, index) /*&& IsUnitCanBeSeen(i, index)*/)
                    {
                        _buildings[index].attackTimer = _buildings[index].building.speed;
                        _buildings[index].target = i;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsUnitInRange(int unitIndex, int buildingIndex)
        {
            // Validate indices for units and buildings
            if (unitIndex < 0 || unitIndex >= _units.Count || buildingIndex < 0 || buildingIndex >= _buildings.Count)
            {
                return false;
            }

            var building = _buildings[buildingIndex];
            var unit = _units[unitIndex];

            // Ensure the building and unit are not null
            if (building == null || unit == null || unit.health <= 0)
            {
                return false;
            }

            // Ensure the positions are not null
            //if (building.worldCenterPosition == null || unit.position == null)
            //{
            //    return false;
            //}

            // Calculate the distance between the building and the unit
            float distance = BattleVector2.Distance(building.worldCenterPosition, unit.position);
            if (distance <= (building.building.radius * Data.gridCellSize))
            {
                if (building.building.blindRange > 0 && distance <= building.building.blindRange)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private bool IsUnitInRangeforOpHero(int unitIndex, int OpHeroIndex)
        {
            // if (_opHeroes[OpHeroIndex].isTargetUnit)
            {
                if (_units.Count < unitIndex || unitIndex == -1/*&& _opHeroes[opHeroIndex].health <= 0*/)
                    return false;

                if (_units[unitIndex] == null || _units[unitIndex].health <= 0)
                    return false;

                float distance = BattleVector2.Distance(_opHeroes[OpHeroIndex].position, _units[unitIndex].position);
                if (distance <= _opHeroes[OpHeroIndex].opHero.attackRange)
                {
                    //_opHeroes[OpHeroIndex].hasRandomPath = false;
                    //_opHeroes[OpHeroIndex].isTargetUnit = true;
                    return true;
                }
                return false;
            }
            return false;
        }
        private bool IsUnitInRangeofPortal(int unitIndex, int OpHeroIndex)
        {
            Building portal = null;

            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].building.id == Data.BuildingID.portal)
                    portal = _buildings[i];
            }

            if (_opHeroes[OpHeroIndex].isTargetUnit)
            {
                if (_units.Count < unitIndex /*&& _opHeroes[opHeroIndex].health <= 0*/)
                    return false;

                float distance = BattleVector2.Distance(_units[unitIndex].position, new BattleVector2(portal.building.x, portal.building.y));
                if (distance <= portal.building.radius)
                {
                    return true;
                }
                return false;
            }
            return false;
        }


        private bool IsHeroInRangeforOpHero(int heroIndex, int OpHeroIndex)
        {
            // Check if OpHeroIndex is valid
            if (OpHeroIndex < 0 || OpHeroIndex >= _opHeroes.Count)
            {
                return false;
            }

            var opHero = _opHeroes[OpHeroIndex];

            // Check if the opponent hero is a target unit and their health is greater than 0
            //  if (!opHero.isTargetUnit)
            {
                // Ensure heroIndex is within bounds
                if (heroIndex < 0 || heroIndex >= _heroes.Count)
                {
                    return false;
                }

                var hero = _heroes[heroIndex];

                // Check if the hero and its position are not null
                if (hero == null || hero.health <= 0)
                {
                    return false;
                }

                // Check if the opponent hero's position is not null

                // Calculate the distance and check if it's within the attack range
                float distance = BattleVector2.Distance(opHero.position, hero.position);
                if (distance <= opHero.opHero.attackRange)
                {
                    //opHero.hasRandomPath = false;
                    // opHero.isTargetUnit = false;
                    return true;
                }
            }

            return false;
        }

        private bool IsHeroInRange(int heroIndex, int buildingIndex)
        {
            if (heroIndex < 0 || heroIndex >= _heroes.Count || buildingIndex < 0 || buildingIndex >= _buildings.Count)
            {
                return false;
            }

            var building = _buildings[buildingIndex];
            var hero = _heroes[heroIndex];

            // Ensure the building and hero are not null
            if (building == null || hero == null || hero.health <= 0)
            {
                return false;
            }

            // Ensure the positions are not null
            //if (building.worldCenterPosition == null || hero.position == null)
            //{
            //    return false;
            //}

            // Calculate the distance between the building and the hero
            float distance = BattleVector2.Distance(building.worldCenterPosition, hero.position);
            if (distance <= (building.building.radius * Data.gridCellSize))
            {
                if (building.building.blindRange > 0 && distance <= building.building.blindRange)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        //private bool IsUnitCanBeSeen(int unitIndex, int buildingIndex)
        //{
        //    for (int i = 0; i < _spells.Count; i++)
        //    {
        //        if (_spells[i].done) { continue; }
        //        if (_spells[i].spell.id == Data.SpellID.invisibility)
        //        {
        //            float distance = BattleVector2.Distance(_units[unitIndex].position, _spells[i].position);
        //            if (distance <= (_spells[i].spell.server.radius * Data.gridCellSize))
        //            {
        //                return false;
        //            }
        //        }
        //    }
        //    return true;
        //}

        private void HandleUnit(int index, double deltaTime)
        {
            /*if (_units[index].unit.id == Data.UnitID.healer)
            {
                if (_units[index].target >= 0 && (_units[_units[index].target].health <= 0 || _units[_units[index].target].health >= _units[_units[index].target].unit.health))
                {
                    _units[index].moving = false;
                    _units[index].target = -1;
                }
                if (_units[index].target < 0)
                {
                    _units[index].moving = false;
                    FindHealerTargets(index);
                }
                if (_units[index].target >= 0)
                {
                    _units[index].moving = false;
                    float distance = BattleVector2.Distance(_units[index].position, _units[_units[index].target].position);
                    if (distance + Data.gridCellSize <= _units[index].unit.attackRange)
                    {
                        _units[index].attackTimer += deltaTime;
                        if (_units[index].attackTimer >= _units[index].unit.attackSpeed)
                        {
                            _units[index].attackTimer -= _units[index].unit.attackSpeed;
                            if (_units[index].unit.attackRange > 0 && _units[index].unit.rangedSpeed > 0)
                            {
                                Projectile projectile = new Projectile();
                                projectile.type = TargetType.unit;
                                projectile.target = _units[index].target;
                                projectile.timer = distance / (_units[index].unit.rangedSpeed * Data.gridCellSize);
                                projectile.damage = GetUnitDamage(index);
                                projectile.follow = true;
                                projectile.position = _units[index].position;
                                projectile.heal = true;
                                projectileCount++;
                                projectile.id = projectileCount;
                                projectiles.Add(projectile);
                                if (projectileCallback != null)
                                {
                                    projectileCallback.Invoke(projectile.id, _units[index].position, _units[_units[index].target].position);
                                }
                            }
                            else
                            {
                                float baseHeal = GetUnitDamage(index);
                                _units[_units[index].target].Heal(baseHeal);
                                for (int i = 0; i < _units.Count; i++)
                                {
                                    if (_units[i].health <= 0 || i == index || i == _units[index].target)
                                    {
                                        continue;
                                    }
                                    float d = BattleVector2.Distance(_units[i].position, _units[_units[index].target].position);
                                    if (d < _units[i].unit.splashRange * Data.gridCellSize)
                                    {
                                        float amount = baseHeal * (1f - (d / _units[i].unit.splashRange * Data.gridCellSize));
                                        _units[i].Heal(amount);
                                    }
                                }
                            }
                            if (_units[index].attackCallback != null)
                            {
                                _units[index].attackCallback.Invoke(_units[index].unit.databaseID, 0);
                            }
                        }
                    }
                    else
                    {
                        // Move the healer
                        _units[index].moving = true;
                        float d = (float)deltaTime * GetUnitMoveSpeed(index) * Data.gridCellSize;
                        _units[index].position = BattleVector2.LerpUnclamped(_units[index].position, _units[_units[index].target].position, d / distance);
                        return;
                    }
                }
            }
            else
            {*/
            if (_units[index].path != null)
            {
                if (_units[index].target < 0 || (_units[index].isTargerBuilding && _units[index].target >= 0 && _buildings[_units[index].target].health <= 0) || (!_units[index].isTargerBuilding && _units[index].target >= 0 && _opHeroes[_units[index].target].health <= 0))
                {
                    _units[index].moving = false;
                    _units[index].path = null;
                    _units[index].target = -1;
                }
                else
                {
                    _units[index].moving = true;
                    /*
                    if(_units[index].unit.movement == Data.UnitMoveType.ground)
                    {
                        bool inJumpRange = false;
                        for (int i = 0; i < _spells.Count; i++)
                        {
                            if (_spells[i].done) { continue; }
                            if (_spells[i].spell.id == Data.SpellID.jump)
                            {
                                float distance = BattleVector2.Distance(_units[index].position, _spells[i].position);
                                if (distance <= (_spells[i].spell.server.radius * Data.gridCellSize))
                                {
                                    inJumpRange = true;
                                    break;
                                }
                            }
                        }
                        if (inJumpRange)
                        {

                        }
                    }
                    */

                    double remainedTime = _units[index].pathTime - _units[index].pathTraveledTime;
                    if (remainedTime >= deltaTime)
                    {
                        double moveExtra = 1;
                        double s = GetUnitMoveSpeed(index);
                        if (s != _units[index].unit.moveSpeed)
                        {
                            moveExtra = s / _units[index].unit.moveSpeed;
                        }
                        _units[index].pathTraveledTime += (deltaTime * moveExtra);
                        if (_units[index].pathTraveledTime > _units[index].pathTime)
                        {
                            _units[index].pathTraveledTime = _units[index].pathTime;
                        }
                        if (_units[index].pathTraveledTime < 0)
                        {
                            _units[index].pathTraveledTime = 0;
                        }
                        deltaTime = 0;
                    }
                    else
                    {
                        _units[index].pathTraveledTime = _units[index].pathTime;
                        deltaTime -= remainedTime;
                    }

                    // Update unit's position based on path
                    _units[index].position = GetPathPosition(_units[index].path.points, (float)(_units[index].pathTraveledTime / _units[index].pathTime));

                    // Check if target is in range
                    if ((_units[index].unit.attackRange > 0 && (_units[index].isTargerBuilding && IsBuildingInRange(index, _units[index].target, false))) || (_units[index].unit.attackRange > 0 && (!_units[index].isTargerBuilding && IsOpHeroInRangeForUnit(index, _units[index].target, false))))
                    {
                        _units[index].path = null;
                    }
                    else
                    {
                        // check if unit reached the end of the path
                        BattleVector2 targetPosition = GridToWorldPosition(new BattleVector2Int(_units[index].path.points.Last().Location.X, _units[index].path.points.Last().Location.Y));
                        float distance = BattleVector2.Distance(_units[index].position, targetPosition);
                        if (distance <= Data.gridCellSize * 0.05f)
                        {
                            _units[index].position = targetPosition;
                            _units[index].path = null;
                            _units[index].moving = false;
                        }
                    }
                }
            }

            if (_units[index].target >= 0)
            {
                bool isBuilding = _units[index].isTargerBuilding;
                if (isBuilding)
                {

                    if (_buildings[_units[index].target].health > 0)
                    {
                        if (_buildings[_units[index].target].building.id == Data.BuildingID.wall && _units[index].mainTarget >= 0 && _buildings[_units[index].mainTarget].health <= 0)
                        {
                            _units[index].moving = false;
                            _units[index].target = -1;
                        }
                        else
                        {
                            if (_units[index].path == null)
                            {
                                // Attack the target
                                _units[index].moving = false;
                                _units[index].attackTimer += deltaTime;
                                if (_units[index].attackTimer >= _units[index].unit.attackSpeed)
                                {
                                    float multiplier = 1;
                                    if (_units[index].unit.priority != Data.TargetPriority.all || _units[index].unit.priority != Data.TargetPriority.none)
                                    {
                                        switch (_buildings[_units[index].target].building.id)
                                        {
                                            case Data.BuildingID.commandcenter:
                                            case Data.BuildingID.mine:
                                            case Data.BuildingID.vault:
                                            case Data.BuildingID.powerplant:
                                            case Data.BuildingID.battery:
                                            case Data.BuildingID.gloommill:
                                            case Data.BuildingID.warehouse:
                                                if (_units[index].unit.priority == Data.TargetPriority.resources)
                                                {
                                                    multiplier = _units[index].unit.priorityMultiplier;
                                                }
                                                break;
                                            case Data.BuildingID.wall:
                                                if (_units[index].unit.priority == Data.TargetPriority.walls)
                                                {
                                                    multiplier = _units[index].unit.priorityMultiplier;
                                                }
                                                break;
                                            case Data.BuildingID.blaster:
                                            case Data.BuildingID.javelineer:
                                            case Data.BuildingID.nuke:
                                            case Data.BuildingID.bunker:
                                            case Data.BuildingID.solarcrest:
                                                if (_units[index].unit.priority == Data.TargetPriority.defenses)
                                                {
                                                    multiplier = _units[index].unit.priorityMultiplier;
                                                }
                                                break;
                                        }
                                    }

                                    float distance = BattleVector2.Distance(_units[index].position, _buildings[_units[index].target].worldCenterPosition);
                                    if (_units[index].unit.attackRange > 0 && _units[index].unit.rangedSpeed > 0)
                                    {
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.building;
                                        projectile.target = _units[index].target;
                                        projectile.timer = distance / (_units[index].unit.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = GetUnitDamage(index) * multiplier;
                                        projectile.follow = true;
                                        projectile.position = _units[index].position;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _units[index].position, _buildings[_units[index].target].worldCenterPosition);
                                        }
                                    }
                                    else
                                    {
                                        _buildings[_units[index].target].TakeDamage(GetUnitDamage(index) * multiplier, ref grid, ref blockedTiles, ref percentage, ref fiftyPercentDestroyed, ref townhallDestroyed, ref completelyDestroyed);
                                    }
                                    _units[index].attackTimer -= _units[index].unit.attackSpeed;
                                    if (_units[index].attackCallback != null)
                                    {
                                        _units[index].attackCallback.Invoke(_units[index].unit.databaseID, _buildings[_units[index].target].building.databaseID, _units[index].isTargerBuilding);
                                    }
                                    //if (_units[index].unit.id == Data.UnitID.wallbreaker)
                                    //{
                                    //    _units[index].TakeDamage(_units[index].health);
                                    //}
                                }
                            }
                        }

                    }
                    else
                    {
                        _units[index].moving = false;
                        _units[index].target = -1;
                    }
                }

                else if (!isBuilding)
                {
                    if (_opHeroes[_units[index].target].health > 0)
                    {
                        if (_units[index].mainTarget >= 0 && _opHeroes[_units[index].mainTarget].health <= 0)
                        {
                            _units[index].moving = false;
                            _units[index].target = -1;
                        }
                        else
                        {
                            if (_units[index].path == null)
                            {
                                // Attack the target
                                _units[index].moving = false;
                                _units[index].attackTimer += deltaTime;
                                if (_units[index].attackTimer >= _units[index].unit.attackSpeed)
                                {
                                    float multiplier = 1;
                                    float distance = BattleVector2.Distance(_units[index].position, _opHeroes[_units[index].target].position);
                                    if (_units[index].unit.attackRange > 0 && _units[index].unit.rangedSpeed > 0)
                                    {
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.opHero;
                                        projectile.target = _units[index].target;
                                        projectile.timer = distance / (_units[index].unit.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = GetUnitDamage(index) * multiplier;
                                        projectile.follow = true;
                                        projectile.position = _units[index].position;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _units[index].position, _opHeroes[_units[index].target].position);
                                        }
                                    }
                                    else
                                    {
                                        _opHeroes[_units[index].target].TakeDamage(GetUnitDamage(index) * multiplier);
                                    }
                                    _units[index].attackTimer -= _units[index].unit.attackSpeed;
                                    if (_units[index].attackCallback != null)
                                    {
                                        _units[index].attackCallback.Invoke(_units[index].unit.databaseID, _opHeroes[_units[index].target].opHero.databaseID, _units[index].isTargerBuilding);
                                    }
                                    //if (_heros[index].hero.id == Data.HeroID.wallbreaker)
                                    //{
                                    //    _heros[index].TakeDamage(_heros[index].health);
                                    //}
                                }
                            }
                        }
                    }
                    else
                    {
                        _units[index].moving = false;
                        _units[index].target = -1;
                    }
                }

            }

            if (_units[index].target < 0)
            {
                // Find a target and path
                _units[index].moving = false;

                float OpHeroDistance = 999;
                float BuildingDistance = 999;
                FindUnitTargets(index);

                if (_units[index].target >= 0)
                    OpHeroDistance = BattleVector2.Distance(_units[index].position, _opHeroes[_units[index].target].position);

                FindTargets(index, _units[index].unit.priority, false);
                if (_units[index].target >= 0)
                    BuildingDistance = BattleVector2.Distance(_units[index].position, _buildings[_units[index].target].worldCenterPosition);

                if (OpHeroDistance < BuildingDistance)
                {
                    FindUnitTargets(index);
                    if (deltaTime > 0 && _units[index].target >= 0 && !_units[index].isTargerBuilding)
                    {
                        HandleUnit(index, deltaTime);
                    }
                    else
                    {
                        FindTargets(index, _units[index].unit.priority, false);
                        if (deltaTime > 0 && _units[index].target >= 0 && _units[index].isTargerBuilding)
                        {
                            HandleUnit(index, deltaTime);
                        }
                    }
                }
                else
                {
                    FindTargets(index, _units[index].unit.priority, false);
                    if (deltaTime > 0 && _units[index].target >= 0 && _units[index].isTargerBuilding)
                    {
                        HandleUnit(index, deltaTime);
                    }
                }
            }
            //}
        }
        private void HandleHero(int index, double deltaTime)
        {
            /*if (_heros[index].hero.id == Data.HeroID.healer)
{
    if (_heros[index].target >= 0 && (_heros[_heros[index].target].health <= 0 || _heros[_heros[index].target].health >= _heros[_heros[index].target].hero.health))
    {
        _heros[index].moving = false;
        _heros[index].target = -1;
    }
    if (_heros[index].target < 0)
    {
        _heros[index].moving = false;
        FindHealerTargets(index);
    }
    if (_heros[index].target >= 0)
    {
        _heros[index].moving = false;
        float distance = BattleVector2.Distance(_heros[index].position, _heros[_heros[index].target].position);
        if (distance + Data.gridCellSize <= _heros[index].hero.attackRange)
        {
            _heros[index].attackTimer += deltaTime;
            if (_heros[index].attackTimer >= _heros[index].hero.attackSpeed)
            {
                _heros[index].attackTimer -= _heros[index].hero.attackSpeed;
                if (_heros[index].hero.attackRange > 0 && _heros[index].hero.rangedSpeed > 0)
                {
                    Projectile projectile = new Projectile();
                    projectile.type = TargetType.hero;
                    projectile.target = _heros[index].target;
                    projectile.timer = distance / (_heros[index].hero.rangedSpeed * Data.gridCellSize);
                    projectile.damage = GetUnitDamage(index);
                    projectile.follow = true;
                    projectile.position = _heros[index].position;
                    projectile.heal = true;
                    projectileCount++;
                    projectile.id = projectileCount;
                    projectiles.Add(projectile);
                    if (projectileCallback != null)
                    {
                        projectileCallback.Invoke(projectile.id, _heros[index].position, _heros[_heros[index].target].position);
                    }
                }
                else
                {
                    float baseHeal = GetUnitDamage(index);
                    _heros[_heros[index].target].Heal(baseHeal);
                    for (int i = 0; i < _heros.Count; i++)
                    {
                        if (_heros[i].health <= 0 || i == index || i == _heros[index].target)
                        {
                            continue;
                        }
                        float d = BattleVector2.Distance(_heros[i].position, _heros[_heros[index].target].position);
                        if (d < _heros[i].hero.splashRange * Data.gridCellSize)
                        {
                            float amount = baseHeal * (1f - (d / _heros[i].hero.splashRange * Data.gridCellSize));
                            _heros[i].Heal(amount);
                        }
                    }
                }
                if (_heros[index].attackCallback != null)
                {
                    _heros[index].attackCallback.Invoke(_heros[index].hero.databaseID, 0);
                }
            }
        }
        else
        {
            // Move the healer
            _heros[index].moving = true;
            float d = (float)deltaTime * GetUnitMoveSpeed(index) * Data.gridCellSize;
            _heros[index].position = BattleVector2.LerpUnclamped(_heros[index].position, _heros[_heros[index].target].position, d / distance);
            return;
        }
    }
}
else
{*/
            if (_heroes[index].path != null)
            {


                if (_heroes[index].target < 0 || (_heroes[index].isTargerBuilding && _heroes[index].target >= 0 && _buildings[_heroes[index].target].health <= 0) || (!_heroes[index].isTargerBuilding && _heroes[index].target >= 0 && _opHeroes[_heroes[index].target].health <= 0))
                {
                    _heroes[index].moving = false;
                    _heroes[index].path = null;
                    _heroes[index].target = -1;
                }
                else
                {
                    _heroes[index].moving = true;
                    /*
                    if(_heros[index].hero.movement == Data.UnitMoveType.ground)
                    {
                        bool inJumpRange = false;
                        for (int i = 0; i < _spells.Count; i++)
                        {
                            if (_spells[i].done) { continue; }
                            if (_spells[i].spell.id == Data.SpellID.jump)
                            {
                                float distance = BattleVector2.Distance(_heros[index].position, _spells[i].position);
                                if (distance <= (_spells[i].spell.server.radius * Data.gridCellSize))
                                {
                                    inJumpRange = true;
                                    break;
                                }
                            }
                        }
                        if (inJumpRange)
                        {

                        }
                    }
                    */

                    double remainedTime = _heroes[index].pathTime - _heroes[index].pathTraveledTime;
                    if (remainedTime >= deltaTime)
                    {
                        double moveExtra = 1;
                        double s = GetHeroMoveSpeed(index);
                        if (s != _heroes[index].hero.moveSpeed)
                        {
                            moveExtra = s / _heroes[index].hero.moveSpeed;
                        }
                        _heroes[index].pathTraveledTime += (deltaTime * moveExtra);
                        if (_heroes[index].pathTraveledTime > _heroes[index].pathTime)
                        {
                            _heroes[index].pathTraveledTime = _heroes[index].pathTime;
                        }
                        if (_heroes[index].pathTraveledTime < 0)
                        {
                            _heroes[index].pathTraveledTime = 0;
                        }
                        deltaTime = 0;
                    }
                    else
                    {
                        _heroes[index].pathTraveledTime = _heroes[index].pathTime;
                        deltaTime -= remainedTime;
                    }

                    // Update hero's position based on path
                    _heroes[index].position = GetPathPosition(_heroes[index].path.points, (float)(_heroes[index].pathTraveledTime / _heroes[index].pathTime));

                    // Check if target is in range
                    if ((_heroes[index].hero.attackRange > 0 && IsBuildingInRange(index, _heroes[index].target, true)) || (_heroes[index].hero.attackRange > 0 && IsOpHeroInRangeForUnit(index, _heroes[index].target, true)))
                    {
                        _heroes[index].path = null;
                    }
                    else
                    {
                        // check if hero reached the end of the path
                        BattleVector2 targetPosition = GridToWorldPosition(new BattleVector2Int(_heroes[index].path.points.Last().Location.X, _heroes[index].path.points.Last().Location.Y));
                        float distance = BattleVector2.Distance(_heroes[index].position, targetPosition);
                        if (distance <= Data.gridCellSize * 0.05f)
                        {
                            _heroes[index].position = targetPosition;
                            _heroes[index].path = null;
                            _heroes[index].moving = false;
                        }
                    }
                }
            }

            if (_heroes[index].target >= 0)
            {
                bool isBuilding = _heroes[index].isTargerBuilding;
                if (isBuilding)
                {
                    if (_buildings[_heroes[index].target].health > 0)
                    {
                        if (_buildings[_heroes[index].target].building.id == Data.BuildingID.wall && _heroes[index].mainTarget >= 0 && _buildings[_heroes[index].mainTarget].health <= 0)
                        {
                            _heroes[index].moving = false;
                            _heroes[index].target = -1;
                        }
                        else
                        {
                            if (_heroes[index].path == null)
                            {
                                // Attack the target
                                _heroes[index].moving = false;
                                _heroes[index].attackTimer += deltaTime;
                                if (_heroes[index].attackTimer >= _heroes[index].hero.attackSpeed)
                                {
                                    float multiplier = 1;
                                    if (_heroes[index].hero.priority != Data.TargetPriority.all || _heroes[index].hero.priority != Data.TargetPriority.none)
                                    {
                                        switch (_buildings[_heroes[index].target].building.id)
                                        {
                                            case Data.BuildingID.commandcenter:
                                            case Data.BuildingID.mine:
                                            case Data.BuildingID.vault:
                                            case Data.BuildingID.powerplant:
                                            case Data.BuildingID.battery:
                                            case Data.BuildingID.gloommill:
                                            case Data.BuildingID.warehouse:
                                                if (_heroes[index].hero.priority == Data.TargetPriority.resources)
                                                {
                                                    multiplier = _heroes[index].hero.priorityMultiplier;
                                                }
                                                break;
                                            case Data.BuildingID.wall:
                                                if (_heroes[index].hero.priority == Data.TargetPriority.walls)
                                                {
                                                    multiplier = _heroes[index].hero.priorityMultiplier;
                                                }
                                                break;
                                            case Data.BuildingID.blaster:
                                            case Data.BuildingID.javelineer:
                                            case Data.BuildingID.nuke:
                                            case Data.BuildingID.bunker:
                                            case Data.BuildingID.solarcrest:
                                                if (_heroes[index].hero.priority == Data.TargetPriority.defenses)
                                                {
                                                    multiplier = _heroes[index].hero.priorityMultiplier;
                                                }
                                                break;
                                        }
                                    }

                                    float distance = BattleVector2.Distance(_heroes[index].position, _buildings[_heroes[index].target].worldCenterPosition);
                                    if (_heroes[index].hero.attackRange > 0 && _heroes[index].hero.rangedSpeed > 0)
                                    {
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.building;
                                        projectile.target = _heroes[index].target;
                                        projectile.timer = distance / (_heroes[index].hero.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = GetHeroDamage(index) * multiplier;
                                        projectile.follow = true;
                                        projectile.position = _heroes[index].position;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _heroes[index].position, _buildings[_heroes[index].target].worldCenterPosition);
                                        }
                                    }
                                    else
                                    {
                                        _buildings[_heroes[index].target].TakeDamage(GetHeroDamage(index) * multiplier, ref grid, ref blockedTiles, ref percentage, ref fiftyPercentDestroyed, ref townhallDestroyed, ref completelyDestroyed);
                                    }
                                    _heroes[index].attackTimer -= _heroes[index].hero.attackSpeed;
                                    if (_heroes[index].attackCallback != null)
                                    {
                                        _heroes[index].attackCallback.Invoke(_heroes[index].hero.databaseID, _buildings[_heroes[index].target].building.databaseID, _heroes[index].isTargerBuilding);
                                    }
                                    //if (_heros[index].hero.id == Data.HeroID.wallbreaker)
                                    //{
                                    //    _heros[index].TakeDamage(_heros[index].health);
                                    //}
                                }
                            }
                        }
                    }
                    else
                    {
                        _heroes[index].moving = false;
                        _heroes[index].target = -1;
                    }
                }
                else if (!isBuilding)
                {
                    if (_opHeroes[_heroes[index].target].health > 0)
                    {
                        if (_heroes[index].mainTarget >= 0 && _opHeroes[_heroes[index].mainTarget].health <= 0)
                        {
                            _heroes[index].moving = false;
                            _heroes[index].target = -1;
                        }
                        else
                        {
                            if (_heroes[index].path == null)
                            {
                                // Attack the target
                                _heroes[index].moving = false;
                                _heroes[index].attackTimer += deltaTime;
                                if (_heroes[index].attackTimer >= _heroes[index].hero.attackSpeed)
                                {
                                    float multiplier = 1;
                                    //if (_heroes[index].hero.priority != Data.TargetPriority.all || _heroes[index].hero.priority != Data.TargetPriority.none)
                                    //{
                                    //    switch (_buildings[_heroes[index].target].building.id)
                                    //    {
                                    //        case Data.BuildingID.commandcenter:
                                    //        case Data.BuildingID.mine:
                                    //        case Data.BuildingID.vault:
                                    //        case Data.BuildingID.powerplant:
                                    //        case Data.BuildingID.battery:
                                    //        case Data.BuildingID.gloommill:
                                    //        case Data.BuildingID.warehouse:
                                    //            if (_heroes[index].hero.priority == Data.TargetPriority.resources)
                                    //            {
                                    //                multiplier = _heroes[index].hero.priorityMultiplier;
                                    //            }
                                    //            break;
                                    //        case Data.BuildingID.wall:
                                    //            if (_heroes[index].hero.priority == Data.TargetPriority.walls)
                                    //            {
                                    //                multiplier = _heroes[index].hero.priorityMultiplier;
                                    //            }
                                    //            break;
                                    //        case Data.BuildingID.blaster:
                                    //        case Data.BuildingID.javelineer:
                                    //        case Data.BuildingID.nuke:
                                    //        case Data.BuildingID.bunker:
                                    //        case Data.BuildingID.solarcrest:
                                    //            if (_heroes[index].hero.priority == Data.TargetPriority.defenses)
                                    //            {
                                    //                multiplier = _heroes[index].hero.priorityMultiplier;
                                    //            }
                                    //            break;
                                    //    }
                                    //}

                                    float distance = BattleVector2.Distance(_heroes[index].position, _opHeroes[_heroes[index].target].position);
                                    if (_heroes[index].hero.attackRange > 0 && _heroes[index].hero.rangedSpeed > 0)
                                    {
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.opHero;
                                        projectile.target = _heroes[index].target;
                                        projectile.timer = distance / (_heroes[index].hero.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = GetHeroDamage(index) * multiplier;
                                        projectile.follow = true;
                                        projectile.position = _heroes[index].position;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _heroes[index].position, _opHeroes[_heroes[index].target].position);
                                        }
                                    }
                                    else
                                    {
                                        _opHeroes[_heroes[index].target].TakeDamage(GetHeroDamage(index) * multiplier);
                                    }
                                    _heroes[index].attackTimer -= _heroes[index].hero.attackSpeed;
                                    if (_heroes[index].attackCallback != null)
                                    {
                                        _heroes[index].attackCallback.Invoke(_heroes[index].hero.databaseID, _opHeroes[_heroes[index].target].opHero.databaseID, _heroes[index].isTargerBuilding);
                                    }
                                    //if (_heros[index].hero.id == Data.HeroID.wallbreaker)
                                    //{
                                    //    _heros[index].TakeDamage(_heros[index].health);
                                    //}
                                }
                            }
                        }
                    }
                    else
                    {
                        _heroes[index].moving = false;
                        _heroes[index].target = -1;
                    }
                }

            }

            if (_heroes[index].target < 0)
            {
                // Find a target and path
                _heroes[index].moving = false;

                float OpHeroDistance = 999;
                float BuildingDistance = 999;
                FindHeroTargets(index);

                if (_heroes[index].target >= 0)
                    OpHeroDistance = BattleVector2.Distance(_heroes[index].position, _opHeroes[_heroes[index].target].position);

                FindTargets(index, _heroes[index].hero.priority, true);
                if (_heroes[index].target >= 0)
                    BuildingDistance = BattleVector2.Distance(_heroes[index].position, _buildings[_heroes[index].target].worldCenterPosition);
                if (OpHeroDistance < BuildingDistance)
                {
                    FindHeroTargets(index);
                    if (deltaTime > 0 && _heroes[index].target >= 0 && !_heroes[index].isTargerBuilding)
                    {
                        HandleHero(index, deltaTime);
                    }
                    else
                    {

                        FindTargets(index, _heroes[index].hero.priority, true);

                        if (deltaTime > 0 && _heroes[index].target >= 0 && _heroes[index].isTargerBuilding)
                        {
                            HandleHero(index, deltaTime);
                        }
                    }
                }
                else
                {
                    FindTargets(index, _heroes[index].hero.priority, true);

                    if (deltaTime > 0 && _heroes[index].target >= 0 && _heroes[index].isTargerBuilding)
                    {
                        HandleHero(index, deltaTime);
                    }
                }

            }
            //}
        }
        bool checkTargetHealth(bool isUnit, int index)
        {
            int targetIndex = _opHeroes[index].target;
            //if (targetIndex < 0) return false;


            if (isUnit && _units.Count > targetIndex)
                return _units[targetIndex].health <= 0;
            else if (!isUnit && _heroes.Count > targetIndex)
                return _heroes[targetIndex].health <= 0;
            else
                return false;
        }

        private void HandleOpHero(int index, double deltaTime)
        {

            if (_opHeroes[index].path != null)
            {

                if (_opHeroes[index].hasRandomPath && !(IsUnitInRangeforOpHero(_opHeroes[index].target, index)) && !(IsHeroInRangeforOpHero(_opHeroes[index].target, index)))
                {

                    //_opHeroes[index].target = -1;


                    _opHeroes[index].moving = true;

                    double remainedTime = _opHeroes[index].pathTime - _opHeroes[index].pathTraveledTime;
                    if (remainedTime >= deltaTime)
                    {
                        double moveExtra = 1;
                        double s = GetOpHeroMoveSpeed(index);
                        if (s != _opHeroes[index].opHero.moveSpeed)
                        {
                            moveExtra = s / _opHeroes[index].opHero.moveSpeed;
                        }
                        _opHeroes[index].pathTraveledTime += (deltaTime * moveExtra);
                        if (_opHeroes[index].pathTraveledTime > _opHeroes[index].pathTime)
                        {
                            _opHeroes[index].pathTraveledTime = _opHeroes[index].pathTime;
                        }
                        if (_opHeroes[index].pathTraveledTime < 0)
                        {
                            _opHeroes[index].pathTraveledTime = 0;
                        }
                        deltaTime = 0;
                    }
                    else
                    {
                        _opHeroes[index].pathTraveledTime = _opHeroes[index].pathTime;
                        deltaTime -= remainedTime;
                    }

                    // Update hero's position based on path
                    _opHeroes[index].position = GetPathPosition(_opHeroes[index].path.points, (float)(_opHeroes[index].pathTraveledTime / _opHeroes[index].pathTime));

                    // Check if target is in range
                    //if (IsUnitInRangeforOpHero(_opHeroes[index].target, index) || IsHeroInRangeforOpHero(_opHeroes[index].target, index))
                    //{
                    //    _opHeroes[index].path = null;
                    //    _opHeroes[index].hasRandomPath = false;
                    //}
                    // check if hero reached the end of the path
                    BattleVector2 targetPosition = GridToWorldPosition(new BattleVector2Int(_opHeroes[index].path.points.Last().Location.X, _opHeroes[index].path.points.Last().Location.Y));
                    float distance = BattleVector2.Distance(_opHeroes[index].position, targetPosition);
                    if (distance <= Data.gridCellSize * 0.05f)
                    {
                        _opHeroes[index].position = targetPosition;
                        _opHeroes[index].path = null;
                        _opHeroes[index].moving = false;
                        _opHeroes[index].hasRandomPath = false;
                    }

                }
                else if (_opHeroes[index].target < 0 || checkTargetHealth(_opHeroes[index].isTargetUnit, index) || checkTargetHealth(!_opHeroes[index].isTargetUnit, index) /* (_opHeroes[index].isTargetUnit && _opHeroes[index].target >= 0 && _units[_opHeroes[index].target].health <= 0) || (!_opHeroes[index].isTargetUnit && _opHeroes[index].target >= 0 && _heroes[_opHeroes[index].target].health <= 0)*/)
                {
                    _opHeroes[index].moving = false;
                    _opHeroes[index].path = null;
                    _opHeroes[index].target = -1;
                    // _opHeroes[index].hasRandomPath = false;
                }
                else
                {
                    _opHeroes[index].moving = true;

                    double remainedTime = _opHeroes[index].pathTime - _opHeroes[index].pathTraveledTime;
                    if (remainedTime >= deltaTime)
                    {
                        double moveExtra = 1;
                        double s = GetOpHeroMoveSpeed(index);
                        if (s != _opHeroes[index].opHero.moveSpeed)
                        {
                            moveExtra = s / _opHeroes[index].opHero.moveSpeed;
                        }
                        _opHeroes[index].pathTraveledTime += (deltaTime * moveExtra);
                        if (_opHeroes[index].pathTraveledTime > _opHeroes[index].pathTime)
                        {
                            _opHeroes[index].pathTraveledTime = _opHeroes[index].pathTime;
                        }
                        if (_opHeroes[index].pathTraveledTime < 0)
                        {
                            _opHeroes[index].pathTraveledTime = 0;
                        }
                        deltaTime = 0;
                    }
                    else
                    {
                        _opHeroes[index].pathTraveledTime = _opHeroes[index].pathTime;
                        deltaTime -= remainedTime;
                    }

                    // Update hero's position based on path
                    _opHeroes[index].position = GetPathPosition(_opHeroes[index].path.points, (float)(_opHeroes[index].pathTraveledTime / _opHeroes[index].pathTime));

                    // Check if target is in range
                    if ((_opHeroes[index].opHero.attackRange > 0 && IsUnitInRangeforOpHero(_opHeroes[index].target, index)) || (_opHeroes[index].opHero.attackRange > 0 && IsHeroInRangeforOpHero(_opHeroes[index].target, index)))
                    {
                        _opHeroes[index].path = null;
                    }
                    else
                    {
                        // check if hero reached the end of the path
                        BattleVector2 targetPosition = GridToWorldPosition(new BattleVector2Int(_opHeroes[index].path.points.Last().Location.X, _opHeroes[index].path.points.Last().Location.Y));
                        float distance = BattleVector2.Distance(_opHeroes[index].position, targetPosition);
                        if (distance <= Data.gridCellSize * 0.05f)
                        {
                            _opHeroes[index].position = targetPosition;
                            _opHeroes[index].path = null;
                            _opHeroes[index].moving = false;
                        }
                    }
                }
            }

            if (_opHeroes[index].target >= 0)
            {
                bool isUnit = _opHeroes[index].isTargetUnit;

                if (isUnit /*&& _units.Count> _opHeroes[index].target*/)
                {
                    if (_units[_opHeroes[index].target].health > 0)
                    {
                        if (_opHeroes[index].mainTarget >= 0 && _units[_opHeroes[index].mainTarget].health <= 0)
                        {
                            _opHeroes[index].moving = false;
                            _opHeroes[index].target = -1;
                        }
                        else
                        {
                            if (_opHeroes[index].path == null)
                            {
                                // Attack the target
                                _opHeroes[index].moving = false;
                                _opHeroes[index].attackTimer += deltaTime;
                                //                        UI_Battle.instanse.ShowLog("Attack Timer : " + _opHeroes[index].attackTimer);
                                if (_opHeroes[index].attackTimer >= _opHeroes[index].opHero.attackSpeed)
                                {

                                    float multiplier = 1;


                                    float distance = BattleVector2.Distance(_opHeroes[index].position, _units[_opHeroes[index].target].position);
                                    if (_opHeroes[index].opHero.attackRange > 0 && _opHeroes[index].opHero.rangedSpeed > 0)
                                    {
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.unit;
                                        projectile.target = _opHeroes[index].target;
                                        projectile.timer = distance / (_opHeroes[index].opHero.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = GetOpHeroDamage(index) * multiplier;
                                        projectile.follow = true;
                                        projectile.position = _opHeroes[index].position;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _opHeroes[index].position, _units[_opHeroes[index].target].position);
                                        }
                                    }
                                    else
                                    {
                                        //UI_Battle.instanse.ShowLog("Hero Damage!");
                                        _units[_opHeroes[index].target].TakeDamage(GetOpHeroDamage(index));
                                    }
                                    _opHeroes[index].attackTimer -= _opHeroes[index].opHero.attackSpeed;
                                    if (_opHeroes[index].attackCallback != null)
                                    {
                                        //UI_Battle.instanse.ShowLog("Attack Shoot!");
                                        _opHeroes[index].attackCallback.Invoke(_opHeroes[index].opHero.databaseID, _units[_opHeroes[index].target].unit.databaseID, _opHeroes[index].isTargetUnit);
                                    }

                                }
                            }
                        }
                    }
                    else
                    {
                        _opHeroes[index].moving = false;
                        _opHeroes[index].target = -1;
                    }
                }
                else if (!isUnit /*&& _heroes.Count > _opHeroes[index].target*/)
                {
                    if (_heroes[_opHeroes[index].target].health > 0)
                    {
                        if (_opHeroes[index].mainTarget >= 0 && _heroes[_opHeroes[index].mainTarget].health <= 0)
                        {
                            _opHeroes[index].moving = false;
                            _opHeroes[index].target = -1;
                        }
                        else
                        {
                            if (_opHeroes[index].path == null)
                            {
                                // Attack the target
                                _opHeroes[index].moving = false;
                                _opHeroes[index].attackTimer += deltaTime;
                                if (_opHeroes[index].attackTimer >= _opHeroes[index].opHero.attackSpeed)
                                {
                                    float multiplier = 1;


                                    float distance = BattleVector2.Distance(_opHeroes[index].position, _heroes[_opHeroes[index].target].position);
                                    if (_opHeroes[index].opHero.attackRange > 0 && _opHeroes[index].opHero.rangedSpeed > 0)
                                    {
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.hero;
                                        projectile.target = _opHeroes[index].target;
                                        projectile.timer = distance / (_opHeroes[index].opHero.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = GetOpHeroDamage(index) * multiplier;
                                        projectile.follow = true;
                                        projectile.position = _opHeroes[index].position;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _opHeroes[index].position, _heroes[_opHeroes[index].target].position);
                                        }
                                    }
                                    else
                                    {
                                        _heroes[_opHeroes[index].target].TakeDamage(GetOpHeroDamage(index));
                                    }
                                    _opHeroes[index].attackTimer -= _opHeroes[index].opHero.attackSpeed;
                                    if (_opHeroes[index].attackCallback != null)
                                    {
                                        _opHeroes[index].attackCallback.Invoke(_opHeroes[index].opHero.databaseID, _heroes[_opHeroes[index].target].hero.databaseID, _opHeroes[index].isTargetUnit);
                                    }

                                }
                            }
                        }
                    }
                    else
                    {
                        _opHeroes[index].moving = false;
                        _opHeroes[index].target = -1;
                    }
                }

                /*
                                //if ((isUnit && _units[_opHeroes[index].target].health > 0) || (!isUnit && _heroes[_opHeroes[index].target].health > 0))
                                //{
                                //    //if (_units[_heroes[index].target].unit.id == Data.BuildingID.wall && _heroes[index].mainTarget >= 0 && _buildings[_heroes[index].mainTarget].health <= 0)
                                //    //{
                                //    //    _heroes[index].moving = false;
                                //    //    _heroes[index].target = -1;
                                //    //}
                                //    //else
                                //    {
                                //        if (_opHeroes[index].path == null)
                                //        {
                                //            // Attack the target
                                //            _opHeroes[index].moving = false;
                                //            _opHeroes[index].attackTimer += deltaTime;
                                //            if (_opHeroes[index].attackTimer >= _opHeroes[index].opHero.attackSpeed)
                                //            {
                                //                float multiplier = 1;
                                //                //if (_opHeroes[index].hero.priority != Data.TargetPriority.all || _opHeroes[index].hero.priority != Data.TargetPriority.none)
                                //                //{
                                //                //    switch (_buildings[_heroes[index].target].building.id)
                                //                //    {
                                //                //        case Data.BuildingID.commandcenter:
                                //                //        case Data.BuildingID.mine:
                                //                //        case Data.BuildingID.vault:
                                //                //        case Data.BuildingID.powerplant:
                                //                //        case Data.BuildingID.battery:
                                //                //        case Data.BuildingID.gloommill:
                                //                //        case Data.BuildingID.warehouse:
                                //                //            if (_heroes[index].hero.priority == Data.TargetPriority.resources)
                                //                //            {
                                //                //                multiplier = _heroes[index].hero.priorityMultiplier;
                                //                //            }
                                //                //            break;
                                //                //        case Data.BuildingID.wall:
                                //                //            if (_heroes[index].hero.priority == Data.TargetPriority.walls)
                                //                //            {
                                //                //                multiplier = _heroes[index].hero.priorityMultiplier;
                                //                //            }
                                //                //            break;
                                //                //        case Data.BuildingID.blaster:
                                //                //        case Data.BuildingID.javelineer:
                                //                //        case Data.BuildingID.nuke:
                                //                //        case Data.BuildingID.bunker:
                                //                //        case Data.BuildingID.solarcrest:
                                //                //            if (_heroes[index].hero.priority == Data.TargetPriority.defenses)
                                //                //            {
                                //                //                multiplier = _heroes[index].hero.priorityMultiplier;
                                //                //            }
                                //                //            break;
                                //                //    }
                                //                //}
                                //                float distance;
                                //                if (isUnit)
                                //                    distance = BattleVector2.Distance(_opHeroes[index].position, _units[_opHeroes[index].target].position);
                                //                else
                                //                    distance = BattleVector2.Distance(_opHeroes[index].position, _heroes[_opHeroes[index].target].position);

                                //                if (_opHeroes[index].opHero.attackRange > 0 && _opHeroes[index].opHero.rangedSpeed > 0)
                                //                {
                                //                    Projectile projectile = new Projectile();
                                //                    if (isUnit)
                                //                        projectile.type = TargetType.unit;
                                //                    else
                                //                        projectile.type = TargetType.hero;

                                //                    projectile.target = _opHeroes[index].target;
                                //                    projectile.timer = distance / (_opHeroes[index].opHero.rangedSpeed * Data.gridCellSize);
                                //                    projectile.damage = GetOpHeroDamage(index) * multiplier;
                                //                    projectile.follow = true;
                                //                    projectile.position = _opHeroes[index].position;
                                //                    projectileCount++;
                                //                    projectile.id = projectileCount;
                                //                    projectiles.Add(projectile);
                                //                    if (projectileCallback != null)
                                //                    {
                                //                        if (isUnit)
                                //                            projectileCallback.Invoke(projectile.id, _opHeroes[index].position, _units[_opHeroes[index].target].position);
                                //                        else
                                //                            projectileCallback.Invoke(projectile.id, _opHeroes[index].position, _heroes[_opHeroes[index].target].position);
                                //                    }
                                //                }
                                //                else
                                //                {
                                //                    if (isUnit)
                                //                        _units[_opHeroes[index].target].TakeDamage(GetOpHeroDamage(index) * multiplier);
                                //                    else
                                //                        _heroes[_opHeroes[index].target].TakeDamage(GetOpHeroDamage(index) * multiplier);
                                //                }
                                //                _opHeroes[index].attackTimer -= _opHeroes[index].opHero.attackSpeed;
                                //                if (_opHeroes[index].attackCallback != null)
                                //                {
                                //                    if (isUnit)
                                //                        _opHeroes[index].attackCallback.Invoke(_opHeroes[index].opHero.databaseID, _units[_opHeroes[index].target].unit.databaseID, isUnit);
                                //                    else
                                //                        _opHeroes[index].attackCallback.Invoke(_opHeroes[index].opHero.databaseID, _heroes[_opHeroes[index].target].hero.databaseID, isUnit);
                                //                }
                                //                //if (_heros[index].hero.id == Data.HeroID.wallbreaker)
                                //                //{
                                //                //    _heros[index].TakeDamage(_heros[index].health);
                                //                //}
                                //            }
                                //        }
                                //    }
                                //}
                                //else
                                //{
                                //    _opHeroes[index].moving = false;
                                //    _opHeroes[index].target = -1;
                                //}
                */
            }

            if (_opHeroes[index].target < 0)
            {
                // Find a target and path

                FindOpHeroTargets(index, true);
                //  FindTargets(index, _opHeroes[index].hero.priority, true);
                if (deltaTime > 0 && _opHeroes[index].target >= 0 && _opHeroes[index].isTargetUnit)
                {
                    _opHeroes[index].moving = false;
                    //_opHeroes[index].moving = false;
                    //_opHeroes[index].path = null;
                    //       _opHeroes[index].hasRandomPath = false;
                    HandleOpHero(index, deltaTime);
                }
                else
                {
                    FindOpHeroTargets(index, false);
                    //  FindTargets(index, _opHeroes[index].hero.priority, true);
                    if (deltaTime > 0 && _opHeroes[index].target >= 0 && !_opHeroes[index].isTargetUnit)
                    {
                        _opHeroes[index].moving = false;
                        //_opHeroes[index].moving = false;
                        //_opHeroes[index].path = null;
                        //       _opHeroes[index].hasRandomPath = false;
                        HandleOpHero(index, deltaTime);
                    }
                    else if (deltaTime > 0 && /*_opHeroes[index].target < 0 &&*/ (!_opHeroes[index].hasRandomPath && _opHeroes[index].path == null))
                    {
                        var path = GetPathToRandomPoint(0, index);
                        //if (path.Item1 >= 0)
                        {
                            //_opHeroes[index].AssignTarget(path.Item1, path.Item2);
                            _opHeroes[index].AssignRandomPoint(path.Item2);
                            //_opHeroes[index].isTargetUnit = true;
                            HandleOpHero(index, deltaTime);
                        }
                    }
                }
            }
        }
        private void FindHealerTargets(int index)
        {
            int target = -1;
            float distance = 99999;
            // TODO: Larger mass of units is priority
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].health <= 0 || i == index || _units[i].health >= _units[i].unit.health || _units[i].unit.movement == Data.UnitMoveType.fly)
                {
                    continue;
                }
                float d = BattleVector2.Distance(_units[i].position, _units[index].position);
                if (d < distance)
                {
                    target = i;
                    distance = d;
                }
            }
            if (target >= 0)
            {
                _units[index].AssignHealerTarget(target, distance + Data.gridCellSize);
            }
        }

        private void ListUnitTargets(int index, Data.TargetPriority priority)
        {
            _units[index].resourceTargets.Clear();
            _units[index].defenceTargets.Clear();
            _units[index].otherTargets.Clear();
            if (priority == Data.TargetPriority.walls)
            {
                priority = Data.TargetPriority.all;
            }
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].health <= 0 || _buildings[i].building.id == Data.BuildingID.wall || !IsBuildingCanBeAttacked(_buildings[i].building.id))
                {
                    continue;
                }
                float distance = BattleVector2.Distance(_buildings[i].worldCenterPosition, _units[index].position);
                switch (_buildings[i].building.id)
                {
                    case Data.BuildingID.commandcenter:
                    case Data.BuildingID.powerplant:
                    case Data.BuildingID.battery:
                    case Data.BuildingID.gloommill:
                    case Data.BuildingID.warehouse:
                    case Data.BuildingID.mine:
                    case Data.BuildingID.vault:
                        _units[index].resourceTargets.Add(i, distance);
                        break;
                    case Data.BuildingID.blaster:
                    case Data.BuildingID.javelineer:
                    case Data.BuildingID.nuke:
                    case Data.BuildingID.solarcrest:
                        _units[index].defenceTargets.Add(i, distance);
                        break;
                    case Data.BuildingID.wall:
                        // Don't include
                        break;
                    default:
                        _units[index].otherTargets.Add(i, distance);
                        break;
                }
            }
        }
        private void ListHeroTargets(int index, Data.TargetPriority priority)
        {
            _heroes[index].resourceTargets.Clear();
            _heroes[index].defenceTargets.Clear();
            _heroes[index].otherTargets.Clear();
            if (priority == Data.TargetPriority.walls)
            {
                priority = Data.TargetPriority.all;
            }
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].health <= 0 || _buildings[i].building.id == Data.BuildingID.wall || !IsBuildingCanBeAttacked(_buildings[i].building.id))
                {
                    continue;
                }
                float distance = BattleVector2.Distance(_buildings[i].worldCenterPosition, _heroes[index].position);
                switch (_buildings[i].building.id)
                {
                    case Data.BuildingID.commandcenter:
                    case Data.BuildingID.powerplant:
                    case Data.BuildingID.battery:
                    case Data.BuildingID.gloommill:
                    case Data.BuildingID.warehouse:
                    case Data.BuildingID.mine:
                    case Data.BuildingID.vault:
                        _heroes[index].resourceTargets.Add(i, distance);
                        break;
                    case Data.BuildingID.blaster:
                    case Data.BuildingID.javelineer:
                    case Data.BuildingID.nuke:
                    case Data.BuildingID.solarcrest:
                        _heroes[index].defenceTargets.Add(i, distance);
                        break;
                    case Data.BuildingID.wall:
                        // Don't include
                        break;
                    default:
                        _heroes[index].otherTargets.Add(i, distance);
                        break;
                }
            }
        }
        private bool FindOpHeroTargets(int index, bool isUnit)
        {
            if (isUnit)
            {

                //  _opHeroes[index].isTargetUnit = true;
                for (int i = 0; i < _units.Count; i++)
                {
                    if (_units[i].health <= 0)
                    {
                        continue;
                    }
                    if (IsUnitInRangeforOpHero(i, index) /*&& IsUnitCanBeSeen(i, index)*/)
                    {
                        var path = GetPathToUnit(i, index);
                        if (path.Item1 >= 0)
                        {
                            _opHeroes[index].AssignTarget(path.Item1, path.Item2);
                            _opHeroes[index].isTargetUnit = true;
                            _opHeroes[index].hasRandomPath = false;
                            //_opHeroes[index].target = i;
                            //_opHeroes[index].path = null;
                            return (true);
                        }
                    }

                    //_opHeroes[index].isTargetUnit = true;
                    // return (true);
                    //}
                }
            }
            else
            {
                // _opHeroes[index].isTargetUnit = false;
                for (int i = 0; i < _heroes.Count; i++)
                {
                    if (_heroes[i].health <= 0)
                    {
                        continue;
                    }
                    if (IsHeroInRangeforOpHero(i, index) /*&& IsUnitCanBeSeen(i, index)*/)
                    {
                        var path = GetPathToHero(i, index);
                        if (path.Item1 >= 0)
                        {
                            _opHeroes[index].AssignTarget(path.Item1, null);
                            _opHeroes[index].isTargetUnit = false;

                            //_opHeroes[index].target = i;
                            //_opHeroes[index].path = null;
                            // _opHeroes[index].isTargetUnit = false;
                            _opHeroes[index].hasRandomPath = false;
                            return (true);
                        }

                        //_opHeroes[index].target = i;
                        //_opHeroes[index].isTargetUnit = false;
                        //return (true);
                    }
                }
            }
            return (false);
        }
        private bool FindHeroTargets(int index)
        {
            for (int i = 0; i < _opHeroes.Count; i++)
            {
                if (_opHeroes[i].health <= 0)
                {
                    continue;
                }
                var path = GetPathToOpHero(i, index, true);
                if (path.Item1 >= 0)
                {
                    _heroes[index].AssignTarget(path.Item1, path.Item2);
                    _heroes[index].isTargerBuilding = false;
                    return true;
                }
            }
            return (false);
        }
        private bool FindUnitTargets(int index)
        {


            for (int i = 0; i < _opHeroes.Count; i++)
            {
                if (_opHeroes[i].health <= 0)
                {
                    continue;
                }
                var path = GetPathToOpHero(i, index, false);
                if (path.Item1 >= 0)
                {
                    _units[index].isTargerBuilding = false;
                    _units[index].AssignTarget(path.Item1, path.Item2);
                    //_units[index].target = i;
                    //_units[index].path = path.Item2;
                    return true;
                }
                //if (IsOpHeroInRangeForUnit(index, i, false) /*&& IsUnitCanBeSeen(i, index)*/)
                //{

                //    //_buildings[index].attackTimer = _buildings[index].building.speed;
                //    //_buildings[index].target = i;
                //    return true;
                //}
            }
            return (false);
        }

        private void FindTargets(int index, Data.TargetPriority priority, bool hero)
        {
            if (hero)
            {
                ListHeroTargets(index, priority);
                if (priority == Data.TargetPriority.defenses)
                {
                    if (_heroes[index].defenceTargets.Count > 0)
                    {
                        AssignTarget(index, ref _heroes[index].defenceTargets, true);
                    }
                    else
                    {
                        FindTargets(index, Data.TargetPriority.all, true);
                        return;
                    }
                }
                else if (priority == Data.TargetPriority.resources)
                {
                    if (_heroes[index].resourceTargets.Count > 0)
                    {
                        AssignTarget(index, ref _heroes[index].resourceTargets, true);
                    }
                    else
                    {
                        FindTargets(index, Data.TargetPriority.all, true);
                        return;
                    }
                }
                else if (priority == Data.TargetPriority.all || priority == Data.TargetPriority.walls)
                {
                    Dictionary<int, float> temp = _heroes[index].GetAllTargets();
                    if (temp.Count > 0)
                    {
                        AssignTarget(index, ref temp, hero, priority == Data.TargetPriority.walls);
                    }
                }
            }
            else if (!hero)
            {
                ListUnitTargets(index, priority);
                if (priority == Data.TargetPriority.defenses)
                {
                    if (_units[index].defenceTargets.Count > 0)
                    {
                        AssignTarget(index, ref _units[index].defenceTargets, false);
                    }
                    else
                    {
                        FindTargets(index, Data.TargetPriority.all, false);
                        return;
                    }
                }
                else if (priority == Data.TargetPriority.resources)
                {
                    if (_units[index].resourceTargets.Count > 0)
                    {
                        AssignTarget(index, ref _units[index].resourceTargets, false);
                    }
                    else
                    {
                        FindTargets(index, Data.TargetPriority.all, false);
                        return;
                    }
                }
                else if (priority == Data.TargetPriority.all || priority == Data.TargetPriority.walls)
                {
                    Dictionary<int, float> temp = _units[index].GetAllTargets();
                    if (temp.Count > 0)
                    {
                        AssignTarget(index, ref temp, false, priority == Data.TargetPriority.walls);
                    }
                }
            }
        }
        private void AssignTarget(int index, ref Dictionary<int, float> targets, bool hero, bool wallsPriority = false)
        {
            if (wallsPriority)
            {
                var wallPath = GetPathToWall(index, ref targets, hero);
                if (wallPath.Item1 >= 0)
                {
                    if (hero)
                    {
                        _heroes[index].isTargerBuilding = true;
                        _heroes[index].AssignTarget(wallPath.Item1, wallPath.Item2);
                    }
                    else
                    {
                        _units[index].isTargerBuilding = true;
                        _units[index].AssignTarget(wallPath.Item1, wallPath.Item2);
                    }
                    return;
                }
            }

            int min = targets.Aggregate((a, b) => a.Value < b.Value ? a : b).Key;
            var path = GetPathToBuilding(min, index, hero);
            if (path.Item1 >= 0)
            {
                if (hero)
                {
                    _heroes[index].isTargerBuilding = true;
                    _heroes[index].AssignTarget(path.Item1, path.Item2);
                }
                else
                {
                    _units[index].isTargerBuilding = true;
                    _units[index].AssignTarget(path.Item1, path.Item2);
                }
            }
            else
            {
                if (hero)
                    _heroes[index].isTargerBuilding = false;
                else
                    _units[index].isTargerBuilding = false;
            }

        }

        private (int, Path) GetPathToWall(int unitIndex, ref Dictionary<int, float> targets, bool hero)
        {
            if (!hero)
            {


                BattleVector2Int unitGridPosition = WorldToGridPosition(_units[unitIndex].position);
                List<Path> tiles = new List<Path>();
                foreach (var target in (targets.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value)))
                {
                    List<Cell> points = search.Find(new Vector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)).ToList();
                    if (Path.IsValid(ref points, new Vector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)))
                    {
                        continue;
                    }
                    else
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            if (_units[i].health <= 0 || _units[i].unit.movement != Data.UnitMoveType.ground || i != unitIndex || _units[i].target < 0 || _units[i].mainTarget != target.Key || _units[i].mainTarget < 0 || _buildings[_units[i].mainTarget].building.id != Data.BuildingID.wall || _buildings[_units[i].mainTarget].health <= 0)
                            {
                                continue;
                            }
                            BattleVector2Int pos = WorldToGridPosition(_units[i].position);
                            List<Cell> pts = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)).ToList();
                            if (Path.IsValid(ref pts, new Vector2Int(pos.x, pos.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)))
                            {
                                float dis = GetPathLength(pts, false);
                                if (id <= Data.battleGroupWallAttackRadius)
                                {
                                    Vector2Int end = _units[i].path.points.Last().Location;
                                    Path p = new Path();
                                    if (p.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                                    {
                                        _units[unitIndex].mainTarget = target.Key;
                                        p.blocks = _units[i].path.blocks;
                                        p.length = GetPathLength(p.points);
                                        return (_units[i].target, p);
                                    }
                                }
                            }
                        }
                        Path path = new Path();
                        if (path.Create(ref unlimitedSearch, unitGridPosition, new BattleVector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y)))
                        {
                            path.length = GetPathLength(path.points);
                            for (int i = 0; i < path.points.Count; i++)
                            {
                                for (int j = 0; j < blockedTiles.Count; j++)
                                {
                                    if (blockedTiles[j].position.x == path.points[i].Location.X && blockedTiles[j].position.y == path.points[i].Location.Y)
                                    {
                                        if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                        {
                                            int t = blockedTiles[j].index;
                                            for (int k = path.points.Count - 1; k >= j; k--)
                                            {
                                                path.points.RemoveAt(k);
                                            }
                                            path.length = GetPathLength(path.points);
                                            return (t, path);
                                        }
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                BattleVector2Int heroGridPosition = WorldToGridPosition(_heroes[unitIndex].position);
                List<Path> tiles = new List<Path>();
                foreach (var target in (targets.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value)))
                {
                    List<Cell> points = search.Find(new Vector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)).ToList();
                    if (Path.IsValid(ref points, new Vector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)))
                    {
                        continue;
                    }
                    else
                    {
                        for (int i = 0; i < _heroes.Count; i++)
                        {
                            if (_heroes[i].health <= 0 || _heroes[i].hero.movement != Data.HeroMoveType.ground || i != unitIndex || _heroes[i].target < 0 || _heroes[i].mainTarget != target.Key || _heroes[i].mainTarget < 0 || _buildings[_heroes[i].mainTarget].building.id != Data.BuildingID.wall || _buildings[_heroes[i].mainTarget].health <= 0)
                            {
                                continue;
                            }
                            BattleVector2Int pos = WorldToGridPosition(_heroes[i].position);
                            List<Cell> pts = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)).ToList();
                            if (Path.IsValid(ref pts, new Vector2Int(pos.x, pos.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)))
                            {
                                float dis = GetPathLength(pts, false);
                                if (id <= Data.battleGroupWallAttackRadius)
                                {
                                    Vector2Int end = _heroes[i].path.points.Last().Location;
                                    Path p = new Path();
                                    if (p.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                                    {
                                        _heroes[unitIndex].mainTarget = target.Key;
                                        p.blocks = _heroes[i].path.blocks;
                                        p.length = GetPathLength(p.points);
                                        return (_heroes[i].target, p);
                                    }
                                }
                            }
                        }
                        Path path = new Path();
                        if (path.Create(ref unlimitedSearch, heroGridPosition, new BattleVector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y)))
                        {
                            path.length = GetPathLength(path.points);
                            for (int i = 0; i < path.points.Count; i++)
                            {
                                for (int j = 0; j < blockedTiles.Count; j++)
                                {
                                    if (blockedTiles[j].position.x == path.points[i].Location.X && blockedTiles[j].position.y == path.points[i].Location.Y)
                                    {
                                        if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                        {
                                            int t = blockedTiles[j].index;
                                            for (int k = path.points.Count - 1; k >= j; k--)
                                            {
                                                path.points.RemoveAt(k);
                                            }
                                            path.length = GetPathLength(path.points);
                                            return (t, path);
                                        }
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }
            return (-1, null);
        }

        private (int, Path) GetPathToUnit(int unitIndex, int opHeroIndex)
        {
            BattleVector2Int OpHeroGridPosition = WorldToGridPosition(_opHeroes[opHeroIndex].position);
            BattleVector2Int unitGridPosition = WorldToGridPosition(_units[unitIndex].position);

            List<Path> tiles = new List<Path>();
            if (_opHeroes[opHeroIndex].opHero.movement == Data.HeroMoveType.ground)
            {
                #region With Walls Effect
                int closest = -1;
                float distance = 99999;
                int blocks = 999;
                //for (int x = 0; x < columns.Count; x++)
                //{
                //    for (int y = 0; y < rows.Count; y++)
                //    {
                //        if (x >= 0 && y >= 0 && x < Data.gridSize && y < Data.gridSize)
                //        {
                Path path1 = new Path();
                Path path2 = new Path();
                path1.Create(ref search, unitGridPosition, OpHeroGridPosition);
                path2.Create(ref unlimitedSearch, unitGridPosition, OpHeroGridPosition);
                if (path1.points != null && path1.points.Count > 0)
                {
                    path1.length = GetPathLength(path1.points);
                    int lengthToBlocks = (int)Math.Floor(path1.length / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                    if (path1.length < distance && lengthToBlocks <= blocks)
                    {
                        closest = tiles.Count;
                        distance = path1.length;
                        blocks = lengthToBlocks;
                    }
                    tiles.Add(path1);
                }
                if (path2.points != null && path2.points.Count > 0)
                {
                    path2.length = GetPathLength(path2.points);
                    for (int i = 0; i < path2.points.Count; i++)
                    {
                        for (int j = 0; j < blockedTiles.Count; j++)
                        {
                            if (blockedTiles[j].position.x == path2.points[i].Location.X && blockedTiles[j].position.y == path2.points[i].Location.Y)
                            {
                                if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                {
                                    path2.blocks.Add(blockedTiles[j]);
                                    // path2.blocksHealth += _buildings[blockedTiles[j].index].health;
                                }
                                break;
                            }
                        }
                    }
                    if (path2.length < distance && path2.blocks.Count <= blocks)
                    {
                        closest = tiles.Count;
                        distance = path1.length;
                        blocks = path2.blocks.Count;
                    }
                    tiles.Add(path2);
                }
                tiles[closest].points.Reverse();
                if (tiles[closest].blocks.Count > 0)
                {
                    for (int i = 0; i < _opHeroes.Count; i++)
                    {
                        if (_opHeroes[i].health <= 0 || _opHeroes[i].opHero.movement != Data.HeroMoveType.ground || i != opHeroIndex || _opHeroes[i].target < 0 || _opHeroes[i].mainTarget != unitIndex || _opHeroes[i].mainTarget < 0 || _units[_opHeroes[i].mainTarget].health <= 0)
                        {
                            continue;
                        }
                        BattleVector2Int pos = WorldToGridPosition(_opHeroes[i].position);
                        List<Cell> points = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(OpHeroGridPosition.x, OpHeroGridPosition.y)).ToList();
                        if (!Path.IsValid(ref points, new Vector2Int(pos.x, pos.y), new Vector2Int(OpHeroGridPosition.x, OpHeroGridPosition.y)))
                        {
                            continue;
                        }
                        // float dis = GetPathLength(points, false);
                        if (id <= Data.battleGroupWallAttackRadius)
                        {
                            Vector2Int end = _opHeroes[i].path.points.Last().Location;
                            Path path = new Path();
                            if (path.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                            {
                                _opHeroes[opHeroIndex].mainTarget = unitIndex;
                                path.blocks = _opHeroes[i].path.blocks;
                                path.length = GetPathLength(path.points);
                                return (_opHeroes[i].target, path);
                            }
                        }
                    }

                    Tile last = tiles[closest].blocks.Last();
                    for (int i = tiles[closest].points.Count - 1; i >= 0; i--)
                    {
                        int x = tiles[closest].points[i].Location.X;
                        int y = tiles[closest].points[i].Location.Y;
                        tiles[closest].points.RemoveAt(i);
                        if (x == last.position.x && y == last.position.y)
                        {
                            break;
                        }
                    }
                    _opHeroes[opHeroIndex].mainTarget = unitIndex;
                    return (last.index, tiles[closest]);
                }
                else
                {
                    return (unitIndex, tiles[closest]);
                }
                #endregion
            }
            else
            {
                #region Without Walls Effect
                int closest = -1;
                float distance = 99999;
                Path path = new Path();
                if (path.Create(ref unlimitedSearch, unitGridPosition, OpHeroGridPosition))
                {
                    path.length = GetPathLength(path.points);
                    if (path.length < distance)
                    {
                        closest = tiles.Count;
                        distance = path.length;
                    }
                    tiles.Add(path);
                    //        }
                    //    }
                    //}
                }
                if (closest >= 0)
                {
                    tiles[closest].points.Reverse();
                    return (unitIndex, tiles[closest]);
                }
                #endregion
            }

            return (-1, null);
        }
        Data.Building portal = null;
        public BattleVector2Int GetRandomPositionOnGrid()
        {
            BattleVector2Int pos = new BattleVector2Int(0, 0);
            portal = null;

            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].building.id == Data.BuildingID.portal)
                {
                    portal = _buildings[i].building;
                    break;
                }
            }

            Random rand = new Random();
            bool isWalkable = false;
            bool isInRange = false;
            int alteration = 0;
            do
            {
                pos.x = rand.Next(0, Data.gridSize);
                pos.y = rand.Next(0, Data.gridSize);

                isWalkable = !grid[pos.x, pos.y].Blocked; // Invert the condition to check for walkable cells
                isInRange = IsInRange(pos, portal);
                alteration++;
                if (alteration > 100)
                {
                    Debug.Print("maximum alteration reached");
                    break;
                }

            } while (!isWalkable || !isInRange); // Change to '||' to continue until both conditions are met

            BattleVector2 bv2 = GridToWorldPosition(pos);
            return new BattleVector2Int((int)bv2.x, (int)bv2.y);
        }

        private bool IsInRange(BattleVector2Int pos, Data.Building portal)
        {
            if (portal == null)
            {
                return false;
            }

            BattleVector2Int portalPos = new BattleVector2Int(portal.x, portal.y);
            bool isInRange = BattleVector2.Distance(portalPos, pos) < portal.radius;

            if (isInRange)
            {
                Debug.Print("this point is in range");
            }
            else
            {
                Debug.Print("this point is not in range");
            }

            return isInRange;
        }

        bool randomizer = false;
        private (int, Path) GetPathToRandomPoint(int unitIndex, int opHeroIndex)
        {
            BattleVector2Int OpHeroGridPosition = WorldToGridPosition(_opHeroes[opHeroIndex].position);

            List<Path> tiles = new List<Path>();
            int closest = -1;
            float distance = 99999;
            //UI_Main.printLog(GetRandomPositionOnGrid() + "Positon Random:");
            Path path = new Path();
            if (path.Create(ref unlimitedSearch, GetRandomPositionOnGrid(), OpHeroGridPosition))
            {
                path.length = GetPathLength(path.points);
                if (path.length < distance)
                {
                    closest = tiles.Count;
                    distance = path.length;
                }
                tiles.Add(path);
                //        }
                //    }
                //}
            }
            if (closest >= 0)
            {
                tiles[closest].points.Reverse();
                return (unitIndex, tiles[closest]);
            }
            //}
            return (-1, null);
        }
        private (int, Path) GetPathToHero(int unitIndex, int opHeroIndex)
        {
            BattleVector2Int OpHeroGridPosition = WorldToGridPosition(_opHeroes[opHeroIndex].position);
            BattleVector2Int unitGridPosition = WorldToGridPosition(_heroes[unitIndex].position);

            List<Path> tiles = new List<Path>();
            if (_opHeroes[opHeroIndex].opHero.movement == Data.HeroMoveType.ground)
            {
                #region With Walls Effect
                int closest = -1;
                float distance = 99999;
                int blocks = 999;
                //for (int x = 0; x < columns.Count; x++)
                //{
                //    for (int y = 0; y < rows.Count; y++)
                //    {
                //        if (x >= 0 && y >= 0 && x < Data.gridSize && y < Data.gridSize)
                //        {
                Path path1 = new Path();
                Path path2 = new Path();
                path1.Create(ref search, unitGridPosition, OpHeroGridPosition);
                path2.Create(ref unlimitedSearch, unitGridPosition, OpHeroGridPosition);
                if (path1.points != null && path1.points.Count > 0)
                {
                    path1.length = GetPathLength(path1.points);
                    int lengthToBlocks = (int)Math.Floor(path1.length / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                    if (path1.length < distance && lengthToBlocks <= blocks)
                    {
                        closest = tiles.Count;
                        distance = path1.length;
                        blocks = lengthToBlocks;
                    }
                    tiles.Add(path1);
                }
                if (path2.points != null && path2.points.Count > 0)
                {
                    path2.length = GetPathLength(path2.points);
                    for (int i = 0; i < path2.points.Count; i++)
                    {
                        for (int j = 0; j < blockedTiles.Count; j++)
                        {
                            if (blockedTiles[j].position.x == path2.points[i].Location.X && blockedTiles[j].position.y == path2.points[i].Location.Y)
                            {
                                if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                {
                                    path2.blocks.Add(blockedTiles[j]);
                                    // path2.blocksHealth += _buildings[blockedTiles[j].index].health;
                                }
                                break;
                            }
                        }
                    }
                    if (path2.length < distance && path2.blocks.Count <= blocks)
                    {
                        closest = tiles.Count;
                        distance = path1.length;
                        blocks = path2.blocks.Count;
                    }
                    tiles.Add(path2);
                }
                tiles[closest].points.Reverse();
                if (tiles[closest].blocks.Count > 0)
                {
                    for (int i = 0; i < _opHeroes.Count; i++)
                    {
                        if (_opHeroes[i].health <= 0 || _opHeroes[i].opHero.movement != Data.HeroMoveType.ground || i != opHeroIndex || _opHeroes[i].target < 0 || _opHeroes[i].mainTarget != unitIndex || _opHeroes[i].mainTarget < 0 || _heroes[_opHeroes[i].mainTarget].health <= 0)
                        {
                            continue;
                        }
                        BattleVector2Int pos = WorldToGridPosition(_opHeroes[i].position);
                        List<Cell> points = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(OpHeroGridPosition.x, OpHeroGridPosition.y)).ToList();
                        if (!Path.IsValid(ref points, new Vector2Int(pos.x, pos.y), new Vector2Int(OpHeroGridPosition.x, OpHeroGridPosition.y)))
                        {
                            continue;
                        }
                        // float dis = GetPathLength(points, false);
                        if (id <= Data.battleGroupWallAttackRadius)
                        {
                            Vector2Int end = _opHeroes[i].path.points.Last().Location;
                            Path path = new Path();
                            if (path.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                            {
                                _opHeroes[opHeroIndex].mainTarget = unitIndex;
                                path.blocks = _opHeroes[i].path.blocks;
                                path.length = GetPathLength(path.points);
                                return (_opHeroes[i].target, path);
                            }
                        }
                    }

                    Tile last = tiles[closest].blocks.Last();
                    for (int i = tiles[closest].points.Count - 1; i >= 0; i--)
                    {
                        int x = tiles[closest].points[i].Location.X;
                        int y = tiles[closest].points[i].Location.Y;
                        tiles[closest].points.RemoveAt(i);
                        if (x == last.position.x && y == last.position.y)
                        {
                            break;
                        }
                    }
                    _opHeroes[opHeroIndex].mainTarget = unitIndex;
                    return (last.index, tiles[closest]);
                }
                else
                {
                    return (unitIndex, tiles[closest]);
                }
                #endregion
            }
            else
            {
                #region Without Walls Effect
                int closest = -1;
                float distance = 99999;
                Path path = new Path();
                if (path.Create(ref unlimitedSearch, unitGridPosition, OpHeroGridPosition))
                {
                    path.length = GetPathLength(path.points);
                    if (path.length < distance)
                    {
                        closest = tiles.Count;
                        distance = path.length;
                    }
                    tiles.Add(path);
                    //        }
                    //    }
                    //}
                }
                if (closest >= 0)
                {
                    tiles[closest].points.Reverse();
                    return (unitIndex, tiles[closest]);
                }
                #endregion
            }

            return (-1, null);

        }
        private (int, Path) GetPathToOpHero(int opHeroIndex, int unitIndex, bool hero)
        {
            if (!hero)
            {

                BattleVector2Int unitGridPosition = WorldToGridPosition(_units[unitIndex].position);
                BattleVector2Int OpHeroGridPosition = WorldToGridPosition(_opHeroes[opHeroIndex].position);

                List<Path> tiles = new List<Path>();
                if (_units[unitIndex].unit.movement == Data.UnitMoveType.ground)
                {
                    #region With Walls Effect
                    int closest = -1;
                    float distance = 99999;
                    int blocks = 999;
                    //for (int x = 0; x < columns.Count; x++)
                    //{
                    //    for (int y = 0; y < rows.Count; y++)
                    //    {
                    //        if (x >= 0 && y >= 0 && x < Data.gridSize && y < Data.gridSize)
                    //        {
                    Path path1 = new Path();
                    Path path2 = new Path();
                    path1.Create(ref search, OpHeroGridPosition, unitGridPosition);
                    path2.Create(ref unlimitedSearch, OpHeroGridPosition, unitGridPosition);
                    if (path1.points != null && path1.points.Count > 0)
                    {
                        path1.length = GetPathLength(path1.points);
                        int lengthToBlocks = (int)Math.Floor(path1.length / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                        if (path1.length < distance && lengthToBlocks <= blocks)
                        {
                            closest = tiles.Count;
                            distance = path1.length;
                            blocks = lengthToBlocks;
                        }
                        tiles.Add(path1);
                    }
                    if (path2.points != null && path2.points.Count > 0)
                    {
                        path2.length = GetPathLength(path2.points);
                        for (int i = 0; i < path2.points.Count; i++)
                        {
                            for (int j = 0; j < blockedTiles.Count; j++)
                            {
                                if (blockedTiles[j].position.x == path2.points[i].Location.X && blockedTiles[j].position.y == path2.points[i].Location.Y)
                                {
                                    if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                    {
                                        path2.blocks.Add(blockedTiles[j]);
                                        // path2.blocksHealth += _buildings[blockedTiles[j].index].health;
                                    }
                                    break;
                                }
                            }
                        }
                        if (path2.length < distance && path2.blocks.Count <= blocks)
                        {
                            closest = tiles.Count;
                            distance = path1.length;
                            blocks = path2.blocks.Count;
                        }
                        tiles.Add(path2);
                        //        }
                        //    }
                        //}
                    }
                    tiles[closest].points.Reverse();
                    if (tiles[closest].blocks.Count > 0)
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            if (_units[i].health <= 0 || _units[i].unit.movement != Data.UnitMoveType.ground || i != unitIndex || _units[i].target < 0 || _units[i].mainTarget != opHeroIndex || _units[i].mainTarget < 0 || _opHeroes[_units[i].mainTarget].health <= 0)
                            {
                                continue;
                            }
                            BattleVector2Int pos = WorldToGridPosition(_units[i].position);
                            List<Cell> points = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)).ToList();
                            if (!Path.IsValid(ref points, new Vector2Int(pos.x, pos.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)))
                            {
                                continue;
                            }
                            // float dis = GetPathLength(points, false);
                            if (id <= Data.battleGroupWallAttackRadius)
                            {
                                Vector2Int end = _units[i].path.points.Last().Location;
                                Path path = new Path();
                                if (path.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                                {
                                    _units[unitIndex].mainTarget = opHeroIndex;
                                    path.blocks = _units[i].path.blocks;
                                    path.length = GetPathLength(path.points);
                                    return (_units[i].target, path);
                                }
                            }
                        }

                        Tile last = tiles[closest].blocks.Last();
                        for (int i = tiles[closest].points.Count - 1; i >= 0; i--)
                        {
                            int x = tiles[closest].points[i].Location.X;
                            int y = tiles[closest].points[i].Location.Y;
                            tiles[closest].points.RemoveAt(i);
                            if (x == last.position.x && y == last.position.y)
                            {
                                break;
                            }
                        }
                        _units[unitIndex].mainTarget = opHeroIndex;
                        return (last.index, tiles[closest]);
                    }
                    else
                    {
                        return (opHeroIndex, tiles[closest]);
                    }
                    #endregion
                }
                else
                {
                    #region Without Walls Effect
                    int closest = -1;
                    float distance = 99999;
                    //for (int x = 0; x < columns.Count; x++)
                    //{
                    //    for (int y = 0; y < rows.Count; y++)
                    //    {
                    //        if (columns[x] >= 0 && rows[y] >= 0 && columns[x] < Data.gridSize && rows[y] < Data.gridSize)
                    //        {
                    Path path = new Path();
                    if (path.Create(ref unlimitedSearch, OpHeroGridPosition, unitGridPosition))
                    {
                        path.length = GetPathLength(path.points);
                        if (path.length < distance)
                        {
                            closest = tiles.Count;
                            distance = path.length;
                        }
                        tiles.Add(path);
                        //        }
                        //    }
                        //}
                    }
                    if (closest >= 0)
                    {
                        tiles[closest].points.Reverse();
                        return (opHeroIndex, tiles[closest]);
                    }
                    #endregion
                }
            }
            else
            {

                BattleVector2Int heroGridPosition = WorldToGridPosition(_heroes[unitIndex].position);
                BattleVector2Int opHeroGridPosition = WorldToGridPosition(_opHeroes[opHeroIndex].position);

                List<Path> tiles = new List<Path>();
                if (_heroes[unitIndex].hero.movement == Data.HeroMoveType.ground)
                {
                    #region With Walls Effect
                    int closest = -1;
                    float distance = 99999;
                    int blocks = 999;

                    Path path1 = new Path();
                    Path path2 = new Path();
                    path1.Create(ref search, opHeroGridPosition, heroGridPosition);
                    path2.Create(ref unlimitedSearch, opHeroGridPosition, heroGridPosition);
                    if (path1.points != null && path1.points.Count > 0)
                    {
                        path1.length = GetPathLength(path1.points);
                        int lengthToBlocks = (int)Math.Floor(path1.length / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                        if (path1.length < distance && lengthToBlocks <= blocks)
                        {
                            closest = tiles.Count;
                            distance = path1.length;
                            blocks = lengthToBlocks;
                        }
                        tiles.Add(path1);
                    }
                    if (path2.points != null && path2.points.Count > 0)
                    {
                        path2.length = GetPathLength(path2.points);
                        for (int i = 0; i < path2.points.Count; i++)
                        {
                            for (int j = 0; j < blockedTiles.Count; j++)
                            {
                                if (blockedTiles[j].position.x == path2.points[i].Location.X && blockedTiles[j].position.y == path2.points[i].Location.Y)
                                {
                                    if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                    {
                                        path2.blocks.Add(blockedTiles[j]);
                                    }
                                    break;
                                }
                            }
                        }
                        if (path2.length < distance && path2.blocks.Count <= blocks)
                        {
                            closest = tiles.Count;
                            distance = path1.length;
                            blocks = path2.blocks.Count;
                        }
                        tiles.Add(path2);

                    }
                    tiles[closest].points.Reverse();
                    if (tiles[closest].blocks.Count > 0)
                    {
                        for (int i = 0; i < _heroes.Count; i++)
                        {
                            if (_heroes[i].health <= 0 || _heroes[i].hero.movement != Data.HeroMoveType.ground || i != unitIndex || _heroes[i].target < 0 || _heroes[i].mainTarget != opHeroIndex || _heroes[i].mainTarget < 0 || _opHeroes[_heroes[i].mainTarget].health <= 0)
                            {
                                continue;
                            }
                            BattleVector2Int pos = WorldToGridPosition(_heroes[i].position);
                            List<Cell> points = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)).ToList();
                            if (!Path.IsValid(ref points, new Vector2Int(pos.x, pos.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)))
                            {
                                continue;
                            }
                            // float dis = GetPathLength(points, false);
                            if (id <= Data.battleGroupWallAttackRadius)
                            {
                                Vector2Int end = _heroes[i].path.points.Last().Location;
                                Path path = new Path();
                                if (path.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                                {
                                    _heroes[unitIndex].mainTarget = opHeroIndex;
                                    path.blocks = _heroes[i].path.blocks;
                                    path.length = GetPathLength(path.points);
                                    return (_heroes[i].target, path);
                                }
                            }
                        }

                        Tile last = tiles[closest].blocks.Last();
                        for (int i = tiles[closest].points.Count - 1; i >= 0; i--)
                        {
                            int x = tiles[closest].points[i].Location.X;
                            int y = tiles[closest].points[i].Location.Y;
                            tiles[closest].points.RemoveAt(i);
                            if (x == last.position.x && y == last.position.y)
                            {
                                break;
                            }
                        }
                        _heroes[unitIndex].mainTarget = opHeroIndex;
                        return (last.index, tiles[closest]);
                    }
                    else
                    {
                        return (opHeroIndex, tiles[closest]);
                    }
                    #endregion
                }
                else
                {
                    #region Without Walls Effect
                    int closest = -1;
                    float distance = 99999;

                    Path path = new Path();
                    if (path.Create(ref unlimitedSearch, opHeroGridPosition, heroGridPosition))
                    {
                        path.length = GetPathLength(path.points);
                        if (path.length < distance)
                        {
                            closest = tiles.Count;
                            distance = path.length;
                        }
                        tiles.Add(path);

                    }
                    if (closest >= 0)
                    {
                        tiles[closest].points.Reverse();
                        return (opHeroIndex, tiles[closest]);
                    }
                    #endregion
                }
            }
            return (-1, null);
        }


        private (int, Path) GetPathToBuilding(int buildingIndex, int unitIndex, bool hero)
        {
            if (!hero)
            {


                if (_buildings[buildingIndex].building.id == Data.BuildingID.wall || _buildings[buildingIndex].building.id == Data.BuildingID.decoration || _buildings[buildingIndex].building.id == Data.BuildingID.obstacle
                    || _buildings[buildingIndex].building.id == Data.BuildingID.obstacle2x2 || _buildings[buildingIndex].building.id == Data.BuildingID.obstacle3x3
                    || _buildings[buildingIndex].building.id == Data.BuildingID.decore_tree || _buildings[buildingIndex].building.id == Data.BuildingID.apartment)
                {
                    return (-1, null);
                }

                BattleVector2Int unitGridPosition = WorldToGridPosition(_units[unitIndex].position);

                // Get the x and y list of the building's surrounding tiles
                List<int> columns = new List<int>();
                List<int> rows = new List<int>();
                int startX = _buildings[buildingIndex].building.x;
                int endX = _buildings[buildingIndex].building.x + _buildings[buildingIndex].building.columns - 1;
                int startY = _buildings[buildingIndex].building.y;
                int endY = _buildings[buildingIndex].building.y + _buildings[buildingIndex].building.rows - 1;
                if (_units[unitIndex].unit.movement == Data.UnitMoveType.ground && _buildings[buildingIndex].building.id == Data.BuildingID.wall)
                {
                    startX--;
                    startY--;
                    endX++;
                    endY++;
                }
                columns.Add(startX);
                columns.Add(endX);
                rows.Add(startY);
                rows.Add(endY);

                // Get the list of building's available surrounding tiles
                List<Path> tiles = new List<Path>();
                if (_units[unitIndex].unit.movement == Data.UnitMoveType.ground)
                {
                    #region With Walls Effect
                    int closest = -1;
                    float distance = 99999;
                    int blocks = 999;
                    for (int x = 0; x < columns.Count; x++)
                    {
                        for (int y = 0; y < rows.Count; y++)
                        {
                            if (x >= 0 && y >= 0 && x < Data.gridSize && y < Data.gridSize)
                            {
                                Path path1 = new Path();
                                Path path2 = new Path();
                                path1.Create(ref search, new BattleVector2Int(columns[x], rows[y]), unitGridPosition);
                                path2.Create(ref unlimitedSearch, new BattleVector2Int(columns[x], rows[y]), unitGridPosition);
                                if (path1.points != null && path1.points.Count > 0)
                                {
                                    path1.length = GetPathLength(path1.points);
                                    int lengthToBlocks = (int)Math.Floor(path1.length / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                                    if (path1.length < distance && lengthToBlocks <= blocks)
                                    {
                                        closest = tiles.Count;
                                        distance = path1.length;
                                        blocks = lengthToBlocks;
                                    }
                                    tiles.Add(path1);
                                }
                                if (path2.points != null && path2.points.Count > 0)
                                {
                                    path2.length = GetPathLength(path2.points);
                                    for (int i = 0; i < path2.points.Count; i++)
                                    {
                                        for (int j = 0; j < blockedTiles.Count; j++)
                                        {
                                            if (blockedTiles[j].position.x == path2.points[i].Location.X && blockedTiles[j].position.y == path2.points[i].Location.Y)
                                            {
                                                if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                                {
                                                    path2.blocks.Add(blockedTiles[j]);
                                                    // path2.blocksHealth += _buildings[blockedTiles[j].index].health;
                                                }
                                                break;
                                            }
                                        }
                                    }
                                    if (path2.length < distance && path2.blocks.Count <= blocks)
                                    {
                                        closest = tiles.Count;
                                        distance = path1.length;
                                        blocks = path2.blocks.Count;
                                    }
                                    tiles.Add(path2);
                                }
                            }
                        }
                    }
                    tiles[closest].points.Reverse();
                    if (tiles[closest].blocks.Count > 0)
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            if (_units[i].health <= 0 || _units[i].unit.movement != Data.UnitMoveType.ground || i != unitIndex || _units[i].target < 0 || _units[i].mainTarget != buildingIndex || _units[i].mainTarget < 0 || _buildings[_units[i].mainTarget].building.id != Data.BuildingID.wall || _buildings[_units[i].mainTarget].health <= 0)
                            {
                                continue;
                            }
                            BattleVector2Int pos = WorldToGridPosition(_units[i].position);
                            List<Cell> points = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)).ToList();
                            if (!Path.IsValid(ref points, new Vector2Int(pos.x, pos.y), new Vector2Int(unitGridPosition.x, unitGridPosition.y)))
                            {
                                continue;
                            }
                            // float dis = GetPathLength(points, false);
                            if (id <= Data.battleGroupWallAttackRadius)
                            {
                                Vector2Int end = _units[i].path.points.Last().Location;
                                Path path = new Path();
                                if (path.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                                {
                                    _units[unitIndex].mainTarget = buildingIndex;
                                    path.blocks = _units[i].path.blocks;
                                    path.length = GetPathLength(path.points);
                                    return (_units[i].target, path);
                                }
                            }
                        }

                        Tile last = tiles[closest].blocks.Last();
                        for (int i = tiles[closest].points.Count - 1; i >= 0; i--)
                        {
                            int x = tiles[closest].points[i].Location.X;
                            int y = tiles[closest].points[i].Location.Y;
                            tiles[closest].points.RemoveAt(i);
                            if (x == last.position.x && y == last.position.y)
                            {
                                break;
                            }
                        }
                        _units[unitIndex].mainTarget = buildingIndex;
                        return (last.index, tiles[closest]);
                    }
                    else
                    {
                        return (buildingIndex, tiles[closest]);
                    }
                    #endregion
                }
                else
                {
                    #region Without Walls Effect
                    int closest = -1;
                    float distance = 99999;
                    for (int x = 0; x < columns.Count; x++)
                    {
                        for (int y = 0; y < rows.Count; y++)
                        {
                            if (columns[x] >= 0 && rows[y] >= 0 && columns[x] < Data.gridSize && rows[y] < Data.gridSize)
                            {
                                Path path = new Path();
                                if (path.Create(ref unlimitedSearch, new BattleVector2Int(columns[x], rows[y]), unitGridPosition))
                                {
                                    path.length = GetPathLength(path.points);
                                    if (path.length < distance)
                                    {
                                        closest = tiles.Count;
                                        distance = path.length;
                                    }
                                    tiles.Add(path);
                                }
                            }
                        }
                    }
                    if (closest >= 0)
                    {
                        tiles[closest].points.Reverse();
                        return (buildingIndex, tiles[closest]);
                    }
                    #endregion
                }
            }
            else
            {

                if (_buildings[buildingIndex].building.id == Data.BuildingID.wall || _buildings[buildingIndex].building.id == Data.BuildingID.decoration || _buildings[buildingIndex].building.id == Data.BuildingID.obstacle
                    || _buildings[buildingIndex].building.id == Data.BuildingID.obstacle2x2 || _buildings[buildingIndex].building.id == Data.BuildingID.obstacle3x3
                    || _buildings[buildingIndex].building.id == Data.BuildingID.decore_tree || _buildings[buildingIndex].building.id == Data.BuildingID.apartment)
                {
                    return (-1, null);
                }

                BattleVector2Int heroGridPosition = WorldToGridPosition(_heroes[unitIndex].position);

                // Get the x and y list of the building's surrounding tiles
                List<int> columns = new List<int>();
                List<int> rows = new List<int>();
                int startX = _buildings[buildingIndex].building.x;
                int endX = _buildings[buildingIndex].building.x + _buildings[buildingIndex].building.columns - 1;
                int startY = _buildings[buildingIndex].building.y;
                int endY = _buildings[buildingIndex].building.y + _buildings[buildingIndex].building.rows - 1;
                if (_heroes[unitIndex].hero.movement == Data.HeroMoveType.ground && _buildings[buildingIndex].building.id == Data.BuildingID.wall)
                {
                    startX--;
                    startY--;
                    endX++;
                    endY++;
                }
                columns.Add(startX);
                columns.Add(endX);
                rows.Add(startY);
                rows.Add(endY);

                // Get the list of building's available surrounding tiles
                List<Path> tiles = new List<Path>();
                if (_heroes[unitIndex].hero.movement == Data.HeroMoveType.ground)
                {
                    #region With Walls Effect
                    int closest = -1;
                    float distance = 99999;
                    int blocks = 999;
                    for (int x = 0; x < columns.Count; x++)
                    {
                        for (int y = 0; y < rows.Count; y++)
                        {
                            if (x >= 0 && y >= 0 && x < Data.gridSize && y < Data.gridSize)
                            {
                                Path path1 = new Path();
                                Path path2 = new Path();
                                path1.Create(ref search, new BattleVector2Int(columns[x], rows[y]), heroGridPosition);
                                path2.Create(ref unlimitedSearch, new BattleVector2Int(columns[x], rows[y]), heroGridPosition);
                                if (path1.points != null && path1.points.Count > 0)
                                {
                                    path1.length = GetPathLength(path1.points);
                                    int lengthToBlocks = (int)Math.Floor(path1.length / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                                    if (path1.length < distance && lengthToBlocks <= blocks)
                                    {
                                        closest = tiles.Count;
                                        distance = path1.length;
                                        blocks = lengthToBlocks;
                                    }
                                    tiles.Add(path1);
                                }
                                if (path2.points != null && path2.points.Count > 0)
                                {
                                    path2.length = GetPathLength(path2.points);
                                    for (int i = 0; i < path2.points.Count; i++)
                                    {
                                        for (int j = 0; j < blockedTiles.Count; j++)
                                        {
                                            if (blockedTiles[j].position.x == path2.points[i].Location.X && blockedTiles[j].position.y == path2.points[i].Location.Y)
                                            {
                                                if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                                {
                                                    path2.blocks.Add(blockedTiles[j]);
                                                    // path2.blocksHealth += _buildings[blockedTiles[j].index].health;
                                                }
                                                break;
                                            }
                                        }
                                    }
                                    if (path2.length < distance && path2.blocks.Count <= blocks)
                                    {
                                        closest = tiles.Count;
                                        distance = path1.length;
                                        blocks = path2.blocks.Count;
                                    }
                                    tiles.Add(path2);
                                }
                            }
                        }
                    }
                    tiles[closest].points.Reverse();
                    if (tiles[closest].blocks.Count > 0)
                    {
                        for (int i = 0; i < _heroes.Count; i++)
                        {
                            if (_heroes[i].health <= 0 || _heroes[i].hero.movement != Data.HeroMoveType.ground || i != unitIndex || _heroes[i].target < 0 || _heroes[i].mainTarget != buildingIndex || _heroes[i].mainTarget < 0 || _buildings[_heroes[i].mainTarget].building.id != Data.BuildingID.wall || _buildings[_heroes[i].mainTarget].health <= 0)
                            {
                                continue;
                            }
                            BattleVector2Int pos = WorldToGridPosition(_heroes[i].position);
                            List<Cell> points = search.Find(new Vector2Int(pos.x, pos.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)).ToList();
                            if (!Path.IsValid(ref points, new Vector2Int(pos.x, pos.y), new Vector2Int(heroGridPosition.x, heroGridPosition.y)))
                            {
                                continue;
                            }
                            // float dis = GetPathLength(points, false);
                            if (id <= Data.battleGroupWallAttackRadius)
                            {
                                Vector2Int end = _heroes[i].path.points.Last().Location;
                                Path path = new Path();
                                if (path.Create(ref search, pos, new BattleVector2Int(end.X, end.Y)))
                                {
                                    _heroes[unitIndex].mainTarget = buildingIndex;
                                    path.blocks = _heroes[i].path.blocks;
                                    path.length = GetPathLength(path.points);
                                    return (_heroes[i].target, path);
                                }
                            }
                        }

                        Tile last = tiles[closest].blocks.Last();
                        for (int i = tiles[closest].points.Count - 1; i >= 0; i--)
                        {
                            int x = tiles[closest].points[i].Location.X;
                            int y = tiles[closest].points[i].Location.Y;
                            tiles[closest].points.RemoveAt(i);
                            if (x == last.position.x && y == last.position.y)
                            {
                                break;
                            }
                        }
                        _heroes[unitIndex].mainTarget = buildingIndex;
                        return (last.index, tiles[closest]);
                    }
                    else
                    {
                        return (buildingIndex, tiles[closest]);
                    }
                    #endregion
                }
                else
                {
                    #region Without Walls Effect
                    int closest = -1;
                    float distance = 99999;
                    for (int x = 0; x < columns.Count; x++)
                    {
                        for (int y = 0; y < rows.Count; y++)
                        {
                            if (columns[x] >= 0 && rows[y] >= 0 && columns[x] < Data.gridSize && rows[y] < Data.gridSize)
                            {
                                Path path = new Path();
                                if (path.Create(ref unlimitedSearch, new BattleVector2Int(columns[x], rows[y]), heroGridPosition))
                                {
                                    path.length = GetPathLength(path.points);
                                    if (path.length < distance)
                                    {
                                        closest = tiles.Count;
                                        distance = path.length;
                                    }
                                    tiles.Add(path);
                                }
                            }
                        }
                    }
                    if (closest >= 0)
                    {
                        tiles[closest].points.Reverse();
                        return (buildingIndex, tiles[closest]);
                    }
                    #endregion
                }
            }
            return (-1, null);
        }

        private bool IsBuildingInRange(int unitIndex, int buildingIndex, bool ishero)
        {
            for (int x = _buildings[buildingIndex].building.x; x < _buildings[buildingIndex].building.x + _buildings[buildingIndex].building.columns; x++)
            {
                for (int y = _buildings[buildingIndex].building.y; y < _buildings[buildingIndex].building.y + _buildings[buildingIndex].building.columns; y++)
                {
                    if (ishero)
                    {
                        float distance = BattleVector2.Distance(GridToWorldPosition(new BattleVector2Int(x, y)), _heroes[unitIndex].position);
                        if (distance <= _heroes[unitIndex].hero.attackRange)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        float distance = BattleVector2.Distance(GridToWorldPosition(new BattleVector2Int(x, y)), _units[unitIndex].position);
                        if (distance <= _units[unitIndex].unit.attackRange)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private bool IsOpHeroInRangeForUnit(int unitIndex, int opHeroIndex, bool ishero)
        {
            if (_opHeroes.Count < opHeroIndex /*&& _opHeroes[opHeroIndex].health <= 0*/)
                return false;

            if (ishero)
            {
                if (_opHeroes.Count > opHeroIndex)
                {

                    float distance = BattleVector2.Distance(_opHeroes[opHeroIndex].position, _heroes[unitIndex].position);
                    if (distance <= _heroes[unitIndex].hero.attackRange)
                    {
                        return true;
                    }
                }
            }
            else
            {
                float distance = BattleVector2.Distance(_opHeroes[opHeroIndex].position, _units[unitIndex].position);
                if (distance <= _units[unitIndex].unit.attackRange)
                {
                    return true;
                }
            }
            return false;
        }

        private static float GetPathLength(IList<Cell> path, bool includeCellSize = true)
        {
            float length = 0;
            if (path != null && path.Count > 1)
            {
                for (int i = 1; i < path.Count; i++)
                {
                    length += BattleVector2.Distance(new BattleVector2(path[i - 1].Location.X, path[i - 1].Location.Y), new BattleVector2(path[i].Location.X, path[i].Location.Y));
                }
            }
            if (includeCellSize)
            {
                length *= Data.gridCellSize;
            }
            return length;
        }

        private bool HandleSpell(int index, double deltaTime)
        {
            bool end = false;
            _spells[index].palsesTimer += deltaTime;
            if (_spells[index].palsesTimer >= _spells[index].spell.server.pulsesDuration)
            {
                _spells[index].palsesTimer -= _spells[index].spell.server.pulsesDuration;
                _spells[index].palsesDone += 1;
                switch (_spells[index].spell.id)
                {
                    case Data.SpellID.zap:
                        for (int i = 0; i < _buildings.Count; i++)
                        {
                            if (_buildings[i].health <= 0 || !IsBuildingCanBeAttacked(_buildings[i].building.id)) { continue; }
                            float damage = (float)Math.Ceiling(GetBuildingInSpellRangePercentage(index, i) * _spells[index].spell.server.pulsesValue);
                            if (damage <= 0) { continue; }
                            _buildings[i].TakeDamage(damage, ref grid, ref blockedTiles, ref percentage, ref fiftyPercentDestroyed, ref townhallDestroyed, ref completelyDestroyed);
                        }
                        break;
                    case Data.SpellID.heal:
                        for (int i = 0; i < _units.Count; i++)
                        {
                            if (_units[i].health <= 0) { continue; }
                            float distance = BattleVector2.Distance(_units[i].position, _spells[index].position);
                            if (distance > _spells[index].spell.server.radius * Data.gridCellSize) { continue; }
                            _units[i].Heal(_spells[index].spell.server.pulsesValue);
                        }
                        break;
                    case Data.SpellID.splinterblast:

                        break;
                    case Data.SpellID.clone:

                        break;
                    case Data.SpellID.frenzy:

                        break;
                    case Data.SpellID.rotwood:

                        break;
                }
                if (_spells[index].pulseCallback != null)
                {
                    _spells[index].pulseCallback.Invoke(_spells[index].spell.databaseID);
                }
            }
            if (_spells[index].palsesDone >= _spells[index].spell.server.pulsesCount)
            {
                _spells[index].done = true;
                if (_spells[index].doneCallback != null)
                {
                    _spells[index].doneCallback.Invoke(_spells[index].spell.databaseID);
                }
                end = true;
            }
            return end;
        }

        private double GetBuildingInSpellRangePercentage(int spellIndex, int buildingIndex)
        {
            double percentage = 0;
            float distance = BattleVector2.Distance(_buildings[buildingIndex].worldCenterPosition, _spells[spellIndex].position);
            float radius = Math.Max(_buildings[buildingIndex].building.columns, _buildings[buildingIndex].building.rows) * Data.gridCellSize / 2f;
            float delta = (_spells[spellIndex].spell.server.radius * Data.gridCellSize) - (distance + radius);
            if (delta >= 0)
            {
                percentage = 1;
            }
            else
            {
                delta = Math.Abs(delta);
                if (delta < radius * 2f)
                {
                    percentage = delta / (radius * 2f);
                }
            }
            return percentage;
        }

        private float GetUnitDamage(int index)
        {
            float damage = _units[index].unit.damage;
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.frenzy)
                {
                    damage += (_units[index].unit.damage * _spells[i].spell.server.pulsesValue);
                }
            }
            return damage;
        }
        private float GetHeroDamage(int index)
        {
            float damage = _heroes[index].hero.damage;
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.frenzy)
                {
                    damage += (_heroes[index].hero.damage * _spells[i].spell.server.pulsesValue);
                }
            }
            return damage;
        }
        private float GetOpHeroDamage(int index)
        {
            float damage = _opHeroes[index].opHero.damage;
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.frenzy)
                {
                    damage += (_opHeroes[index].opHero.damage * _spells[i].spell.server.pulsesValue);
                }
            }
            return damage;
        }

        private float GetUnitMoveSpeed(int index)
        {
            float speed = _units[index].unit.moveSpeed;
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.frenzy)
                {
                    speed += _spells[i].spell.server.pulsesValue2;
                }
                //else if (_spells[i].spell.id == Data.SpellID.haste)
                //{
                //    speed += _spells[i].spell.server.pulsesValue;
                //}
            }
            return speed;
        }
        private float GetHeroMoveSpeed(int index)
        {
            float speed = _heroes[index].hero.moveSpeed;
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.frenzy)
                {
                    speed += _spells[i].spell.server.pulsesValue2;
                }
                //else if (_spells[i].spell.id == Data.SpellID.haste)
                //{
                //    speed += _spells[i].spell.server.pulsesValue;
                //}
            }
            return speed;
        }
        private float GetOpHeroMoveSpeed(int index)
        {
            float speed = _opHeroes[index].opHero.moveSpeed;
            //for (int i = 0; i < _spells.Count; i++)
            //{
            //    if (_spells[i].done) { continue; }
            //    if (_spells[i].spell.id == Data.SpellID.frenzy)
            //    {
            //        speed += _spells[i].spell.server.pulsesValue2;
            //    }
            //    //else if (_spells[i].spell.id == Data.SpellID.haste)
            //    //{
            //    //    speed += _spells[i].spell.server.pulsesValue;
            //    //}
            //}
            return speed;
        }

        [System.Serializable]
        public class Path
        {
            public Path()
            {
                length = 0;
                points = null;
                blocks = new List<Tile>();
            }
            public bool Create(ref AStarSearch search, BattleVector2Int start, BattleVector2Int end)
            {
                points = search.Find(new Vector2Int(start.x, start.y), new Vector2Int(end.x, end.y)).ToList();
                if (!IsValid(ref points, new Vector2Int(start.x, start.y), new Vector2Int(end.x, end.y)))
                {
                    points = null;
                    return false;
                }
                else
                {
                    this.start.x = start.x;
                    this.start.y = start.y;
                    this.end.x = end.x;
                    this.end.y = end.y;
                    return true;
                }
            }
            public bool Create(ref AStarSearch search, Vector2Int start, Vector2Int end)
            {
                //Debug.Log("1a0");
                points = search.Find(start, end).ToList();
                //Debug.Log("1a0_1");
                //Debug.Log("Points are null?"+points.Count);
                if (!IsValid(ref points, start, end))
                {
                    //Debug.Log("1a1");
                    points = null;
                    return false;
                }
                else
                {
                    //Debug.Log("1a2");
                    return true;
                }
            }
            public static bool IsValid(ref List<Cell> points, Vector2Int start, Vector2Int end)
            {
                if (points == null || !points.Any() || !points.Last().Location.Equals(end) || !points.First().Location.Equals(start))
                {
                    return false;
                }
                return true;
            }
            public BattleVector2Int start;
            public BattleVector2Int end;
            public List<Cell> points = null;
            public float length = 0;
            public List<Tile> blocks = null;
            // public float blocksHealth = 0;
        }

        private static BattleVector2 GetPathPosition(IList<Cell> path, float t)
        {
            if (t < 0) { t = 0; }
            if (t > 1) { t = 1; }
            float totalLength = GetPathLength(path);
            float length = 0;
            if (path != null && path.Count > 1)
            {
                for (int i = 1; i < path.Count; i++)
                {
                    BattleVector2Int a = new BattleVector2Int(path[i - 1].Location.X, path[i - 1].Location.Y);
                    BattleVector2Int b = new BattleVector2Int(path[i].Location.X, path[i].Location.Y);
                    float l = BattleVector2.Distance(a, b) * Data.gridCellSize;
                    float p = (length + l) / totalLength;
                    if (p >= t)
                    {
                        t = (t - (length / totalLength)) / (p - (length / totalLength));
                        return BattleVector2.LerpUnclamped(GridToWorldPosition(a), GridToWorldPosition(b), t);
                    }
                    length += l;
                }
            }
            return GridToWorldPosition(new BattleVector2Int(path[0].Location.X, path[0].Location.Y));
        }

        private static BattleVector2 GridToWorldPosition(BattleVector2Int position)
        {
            return new BattleVector2(position.x * Data.gridCellSize + Data.gridCellSize / 2f, position.y * Data.gridCellSize + Data.gridCellSize / 2f);
        }

        private static BattleVector2Int WorldToGridPosition(BattleVector2 position)
        {
            return new BattleVector2Int((int)Math.Floor(position.x / Data.gridCellSize), (int)Math.Floor(position.y / Data.gridCellSize));
        }

        public struct BattleVector2
        {
            public float x;
            public float y;

            public BattleVector2(float x, float y) { this.x = x; this.y = y; }

            public static BattleVector2 LerpUnclamped(BattleVector2 a, BattleVector2 b, float t)
            {
                return new BattleVector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
            }

            public static float Distance(BattleVector2 a, BattleVector2 b)
            {
                float diff_x = a.x - b.x;
                float diff_y = a.y - b.y;
                return (float)Math.Sqrt(diff_x * diff_x + diff_y * diff_y);
            }

            public static float Distance(BattleVector2Int a, BattleVector2Int b)
            {
                return Distance(new BattleVector2(a.x, a.y), new BattleVector2(b.x, b.y));
            }

            /// <summary>
            /// Smootly moves a vector2 to another vector2 with desired speed.
            /// </summary>
            /// <param name="source">Position which you want to move from.</param>
            /// <param name="target">Position which you want to reach.</param>
            /// <param name="speed">Move distance per second. Note: Do not multiply delta time to speed.</param>
            public static BattleVector2 LerpStatic(BattleVector2 source, BattleVector2 target, float deltaTime, float speed)
            {
                if ((source.x == target.x && source.y == target.y) || speed <= 0) { return source; }
                float distance = Distance(source, target);
                float t = speed * deltaTime;
                if (t > distance) { t = distance; }
                return LerpUnclamped(source, target, distance == 0 ? 1 : t / distance);
            }
        }

        public struct BattleVector2Int
        {
            public int x;
            public int y;

            public BattleVector2Int(int x, int y) { this.x = x; this.y = y; }
        }

        public static bool IsBuildingCanBeAttacked(Data.BuildingID id)
        {
            switch (id)
            {
                case Data.BuildingID.obstacle:
                case Data.BuildingID.decoration:
                case Data.BuildingID.apartment:
                case Data.BuildingID.decore_tree:
                case Data.BuildingID.rustguard:
                case Data.BuildingID.obstacle2x2:
                case Data.BuildingID.obstacle3x3:
                    return false;
            }
            return true;
        }

    }
}