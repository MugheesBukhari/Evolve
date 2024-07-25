namespace GSS.Evolve
{
    using GSS.RealtimeNetworking.Client;
    using DG.Tweening;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

    public class UI_Battle : MonoBehaviour
    {
        private Battle battle = null;
        private bool isStarted = false;
        private bool readyToStart = false;
        [SerializeField] private GameObject _endPanel = null;
        [SerializeField] private GameObject _infoPanel = null;
        [SerializeField] public Text _playerNameText = null;
        [SerializeField] public Text _timerText = null;
        [SerializeField] public Text _timerDescription = null;
        [SerializeField] public Text _percentageText = null;
        [SerializeField] public Text _lootGoldText = null;
        [SerializeField] public Text _lootElixirText = null;
        [SerializeField] public Text _lootDarkText = null;
        [SerializeField] public Text _winTrophiesText = null;
        [SerializeField] public Text _looseTrophiesText = null;
        [SerializeField] public Text _endGoldText = null;
        [SerializeField] public Text _endElixirText = null;
        [SerializeField] public Text _endDarkText = null;
        [SerializeField] public Text _endTrophiesText = null;
        [SerializeField] public Text _PlayerGoldText = null;
        [SerializeField] public Text _PlayerWoodText = null;
        [SerializeField] public Text _PlayerEnergyText = null;
        [SerializeField] public Image _PlayerGoldBar = null;
        [SerializeField] public Image _PlayerWoodBar = null;
        [SerializeField] public Image _PlayerEnergyBar = null;
        [SerializeField] public Image _PlayerGoldIcon = null;
        [SerializeField] public Image _PlayerWoodIcon = null;
        [SerializeField] public Image _PlayerEnergyIcon = null;
        [SerializeField] public UI_Bar healthBarPrefab = null;
        [SerializeField] private RectTransform healthBarGrid = null;
        [SerializeField] private BattleUnit[] battleUnits = null; public BattleUnit[] BattleUnits { get { return battleUnits; } }
        [SerializeField] private Hero[] hero = null; public Hero[] Hero { get { return hero; } }
        [SerializeField] private Button _findButton = null;
        [SerializeField] private GameObject _damagePanel = null;
        [SerializeField] private GameObject _star1 = null;
        [SerializeField] private GameObject _star2 = null;
        [SerializeField] private GameObject _star3 = null;
        [SerializeField] private GameObject _endStar1 = null;
        [SerializeField] private GameObject _endStar2 = null;
        [SerializeField] private GameObject _endStar3 = null;
        [SerializeField] private GameObject _endWinEffects = null;
        [SerializeField] public Text _endText = null;
        [SerializeField] public Text _findCostText = null;
        [SerializeField] private Button _closeButton = null;
        [SerializeField] private Button _okButton = null;
        [SerializeField] private Button _surrenderButton = null;
        [SerializeField] private List<GameObject> removables = null;
        [SerializeField] public UI_SpellEffect spellEffectPrefab = null;
        private List<BattleUnit> unitsOnGrid = new List<BattleUnit>();
        private List<Hero> heroesOnGrid = new List<Hero>();
        private List<Hero> OpheroesOnGrid = new List<Hero>();
        public List<BuildingOnGrid> buildingsOnGrid = new List<BuildingOnGrid>();
        private DateTime baseTime;
        private List<ItemToAdd> toAddUnits = new List<ItemToAdd>();
        private List<ItemToAdd> toAddHeroes = new List<ItemToAdd>();
        private List<ItemToAdd> toAddOpHeroes = new List<ItemToAdd>();
        private List<ItemToAdd> toAddSpells = new List<ItemToAdd>();
        private long target = 0;
        private bool surrender = false;
        private Data.BattleType _battleType = Data.BattleType.normal;
        private float itemHeight = 1;
        private byte[] opponentBytes = null;

        [Header("Clouds")]
        public GameObject CloudsAnimIn; //just set active true to play
        public GameObject CloudsAnimOut; //just set active true to play
        public class BuildingOnGrid
        {
            public long id = 0;
            public int index = -1;
            public Building building = null;
        }

        private class ItemToAdd
        {
            public ItemToAdd(long id, int x, int y)
            {
                this.id = id;
                this.x = x;
                this.y = y;
            }
            public long id;
            public int x;
            public int y;
        }

        private void Start()
        {
            _closeButton.onClick.AddListener(Close);
            _findButton.onClick.AddListener(Find);
            _okButton.onClick.AddListener(CloseEndPanel);
            _surrenderButton.onClick.AddListener(Surrender);
            itemHeight = (battleItemsGridRoot.anchorMax.y - battleItemsGridRoot.anchorMin.y) * Screen.height;


            if (win_Banner == null)
            {
                win_Banner = Banner.sprite;
                win_sunray = sunray.sprite;
                win_BannerText = BannerText.sprite;
                win_listBg = listBg.sprite;
                win_Bg = Bg.sprite;
                win_homeButton = homeButton.sprite;
                win_rewardBg = rewardBg.sprite;
                win_rewardText = rewardText.sprite;
            }
        }

        private void CloseEndPanel()
        {
            Close();
            if (TutorialManager.Instance.isPvPTutorial)
            {

                TutorialManager.Instance.endPVPTutorial();

            }
        }

        private void MessageResponded(int layoutIndex, int buttonIndex)
        {
            if (layoutIndex == 1)
            {
                MessageBox.Close();
            }
        }

        private void Close()
        {
            UI_Main.instanse._grid.Clear();
            ClearAllRemovables();
            Data.isDataSynced = false;
            //Player.instanse.SyncData(Player.instanse.data);
            isStarted = false;
            readyToStart = false;
            SetStatus(false);
            UI_Main.instanse.SetStatus(true);
            SoundManager.instanse.PlaySound(SoundManager.SoundType.UIClick);
        }

        public void Find()
        {
            readyToStart = false;
            _findButton.gameObject.SetActive(false);
            _closeButton.gameObject.SetActive(false);
        
            UI_Search.instanse.Find();
        }

        List<Data.Building> startbuildings = new List<Data.Building>();
        List<Battle.Building> battleBuildings = new List<Battle.Building>();

        public void NoTarget()
        {
            Close();
            switch (Language.instanse.language)
            {
                case Language.LanguageID.persian:
                    MessageBox.Open(1, 0.8f, true, MessageResponded, new string[] { "در حال حاضر هدفی برای حمله پیدا نشد." }, new string[] { "باشه" });
                    break;
                default:
                    MessageBox.Open(1, 0.8f, true, MessageResponded, new string[] { "There is no target to attack at this moment. Please try again later." }, new string[] { "OK" });
                    break;
            }
        }

        public bool checkUnitAvailable()
        {
            ClearUnits();
            for (int i = 0; i < Player.instanse.data.units.Count; i++)
            {
                if (!Player.instanse.data.units[i].ready)
                {
                    continue;
                }
                int k = -1;
                for (int j = 0; j < units.Count; j++)
                {
                    if (units[j].id == Player.instanse.data.units[i].id)
                    {
                        k = j;
                        break;
                    }
                }
                if (k < 0)
                {
                    k = units.Count;
                    UI_BattleUnit bu = Instantiate(unitsPrefab, battleItemsGrid);
                    bu.Initialize(Player.instanse.data.units[i].id);
                    RectTransform rect = bu.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(itemHeight, itemHeight);
                    units.Add(bu);
                }
                units[k].Add(Player.instanse.data.units[i].databaseID);
            }
            int unitsCount = units.Count;
            ClearUnits();
            return unitsCount > 0;
        }

        Data.Player OpponentPlayerData;
        public bool Display(Data.Player player, List<Data.Building> buildings, long defender, Data.BattleType battleType)
        {
            Player.inBattle = true;
            Player.instanse.UpdateResourcesUI();
            OpponentPlayerData = player;
            opponentBytes = null;
            _playerNameText.text = player.id == 971 ? "Gloomified Base" : player.name;
            ClearSpells();
            ClearUnits();
            ClearHeroes();
            ClearBuilders();
            ClearTrainingUnits();
            // UI_Train.instanse.gameObject.SetActive(false);
            _damagePanel.SetActive(false);
            _star1.SetActive(false);
            _star2.SetActive(false);
            _star3.SetActive(false);
            for (int i = 0; i < Player.instanse.data.units.Count; i++)
            {
                if (!Player.instanse.data.units[i].ready)
                {
                    continue;
                }
                int k = -1;
                for (int j = 0; j < units.Count; j++)
                {
                    if (units[j].id == Player.instanse.data.units[i].id)
                    {
                        k = j;
                        break;
                    }
                }
                if (k < 0)
                {
                    k = units.Count;
                    UI_BattleUnit bu = Instantiate(unitsPrefab, battleItemsGrid);
                    bu.Initialize(Player.instanse.data.units[i].id);
                    RectTransform rect = bu.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(itemHeight, itemHeight);
                    units.Add(bu);
                }
                units[k].Add(Player.instanse.data.units[i].databaseID);
            }

            for (int i = 0; i < Player.instanse.data.heroes.Count; i++)
            {
                if (!Player.instanse.data.heroes[i].summoned || !Player.instanse.data.heroes[i].battle_ready)
                {
                    continue;
                }
                int k = -1;
                for (int j = 0; j < heroes.Count; j++)
                {
                    if (heroes[j].id == Player.instanse.data.heroes[i].id)
                    {
                        k = j;
                        break;
                    }
                }
                if (k < 0)
                {
                    k = heroes.Count;
                    UI_Hero h = Instantiate(heroesPrefab, battleItemsGrid);
                    h.Initialize(Player.instanse.data.heroes[i].id);
                    RectTransform rect = h.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(itemHeight, itemHeight);
                    heroes.Add(h);
                }
                heroes[k].Add(Player.instanse.data.heroes[i].databaseID);
            }


            for (int i = 0; i < Player.instanse.data.spells.Count; i++)
            {
                if (!Player.instanse.data.spells[i].ready)
                {
                    continue;
                }
                int k = -1;
                for (int j = 0; j < spells.Count; j++)
                {
                    if (spells[j].id == Player.instanse.data.spells[i].id)
                    {
                        k = j;
                        break;
                    }
                }
                if (k < 0)
                {
                    k = spells.Count;
                    UI_BattleSpell bs = Instantiate(spellsPrefab, battleItemsGrid);
                    bs.Initialize(Player.instanse.data.spells[i].id);
                    RectTransform rect = bs.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(itemHeight, itemHeight);
                    spells.Add(bs);
                }
                spells[k].Add(Player.instanse.data.spells[i].databaseID);
            }

            if (units.Count <= 0)
            {
                switch (Language.instanse.language)
                {
                    case Language.LanguageID.persian:
                        MessageBox.Open(1, 0.8f, true, MessageResponded, new string[] { "هیچ سربازی برای حمله ندارید." }, new string[] { "باشه" });
                        break;
                    default:
                        MessageBox.Open(1, 0.8f, true, MessageResponded, new string[] { "You do not have any units for battle." }, new string[] { "OK" });
                        break;
                }
                Player.inBattle = false;
                return false;
            }

            _battleType = battleType;
            int townhallLevel = 1;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].id == Data.BuildingID.commandcenter)
                {
                    townhallLevel = buildings[i].level;
                    if (_battleType != Data.BattleType.war)
                    {
                        break;
                    }
                }
                if (_battleType == Data.BattleType.war)
                {
                    buildings[i].x = buildings[i].warX;
                    buildings[i].y = buildings[i].warY;
                }
            }

            target = defender;
            startbuildings = buildings;
            SetOpData();
            battleBuildings.Clear();
            spellEffects.Clear();

            for (int i = 0; i < buildings.Count; i++)
            {
                //Debug.LogError("Us:10");
                if (buildings[i].x < 0 || buildings[i].y < 0)
                {
                    continue;
                }

                Battle.Building building = new Battle.Building();
                building.building = buildings[i];
                switch (building.building.id)
                {
                    case Data.BuildingID.commandcenter:
                        building.lootGoldStorage = Data.GetStorageGoldAndPowerLoot(townhallLevel, building.building.goldStorage);
                        building.lootPowerStorage = Data.GetStorageGoldAndPowerLoot(townhallLevel, building.building.powerStorage);
                        building.lootWoodStorage = Data.GetStorageWoodLoot(townhallLevel, building.building.woodStorage);
                        break;
                    case Data.BuildingID.mine:
                        building.lootGoldStorage = Data.GetMinesGoldAndPowerLoot(townhallLevel, building.building.goldStorage);
                        break;
                    case Data.BuildingID.vault:
                        building.lootGoldStorage = Data.GetStorageGoldAndPowerLoot(townhallLevel, building.building.goldStorage);
                        break;
                    case Data.BuildingID.powerplant:
                        building.lootPowerStorage = Data.GetMinesGoldAndPowerLoot(townhallLevel, building.building.powerStorage);
                        break;
                    case Data.BuildingID.battery:
                        building.lootPowerStorage = Data.GetStorageGoldAndPowerLoot(townhallLevel, building.building.powerStorage);
                        break;
                    case Data.BuildingID.gloommill:
                        building.lootWoodStorage = Data.GetMinesWoodLoot(townhallLevel, building.building.woodStorage);
                        break;
                    case Data.BuildingID.warehouse:
                        building.lootWoodStorage = Data.GetStorageWoodLoot(townhallLevel, building.building.woodStorage);
                        break;
                }
                battleBuildings.Add(building);
            }

            switch (Language.instanse.language)
            {
                case Language.LanguageID.persian:
                    _timerDescription.text = "زمان تا شروع حمله:";
                    break;
                default:
                    _timerDescription.text = "Battle Starts In:";
                    break;
            }
            _timerText.text = TimeSpan.FromSeconds(Data.battlePrepDuration).ToString(@"mm\:ss");

            ClearBuildingsOnGrid();
            ClearUnitsOnGrid();
            ClearHeroesOnGrid();
            ClearOpHeroesOnGrid();

            UI_Main.instanse._grid.Clear();
            for (int i = 0; i < battleBuildings.Count; i++)
            {
                var prefab = UI_Main.instanse.GetBuildingPrefab(battleBuildings[i].building.id);
                if (prefab.Item1 != null)
                {
                    BuildingOnGrid building = new BuildingOnGrid();
                    building.building = Instantiate(prefab.Item1, Vector3.zero, Quaternion.identity);
                    building.building.rows = prefab.Item2.rows;
                    building.building.columns = prefab.Item2.columns;
                    building.building.databaseID = battleBuildings[i].building.databaseID;
                    building.building.PlacedOnGrid(battleBuildings[i].building.x, battleBuildings[i].building.y, true);
                    if (building.building._baseArea)
                    {
                        building.building._baseArea.gameObject.SetActive(false);
                    }
                    building.building.healthBar = Instantiate(healthBarPrefab, healthBarGrid);
                    building.building.healthBar.bar.fillAmount = 1;
                    building.building.healthBar.gameObject.SetActive(false);

                    building.building.data = battleBuildings[i].building;
                    building.id = battleBuildings[i].building.databaseID;
                    building.index = i;
                    buildingsOnGrid.Add(building);
                    UI_Main.instanse._grid.buildings.Add(building.building);
                }

                battleBuildings[i].building.x += Data.battleGridOffset;
                battleBuildings[i].building.y += Data.battleGridOffset;
                if (buildingsOnGrid[i].building.id == Data.BuildingID.commandcenter && PlayerPrefs.GetInt("PVPTutorial", 0) == 0)
                {
                    buildingsOnGrid[i].building._animator.runtimeAnimatorController = TutorialManager.Instance.RuinAnimator;
                    PlayerPrefs.SetInt("PVPTutorial", 1);
                }
                if (buildingsOnGrid[i].building.id == Data.BuildingID.obstacle || buildingsOnGrid[i].building.id == Data.BuildingID.obstacle2x2 || buildingsOnGrid[i].building.id == Data.BuildingID.obstacle3x3)
                {
                    buildingsOnGrid[i].building.CheckLevel();
                }
                if (buildingsOnGrid[i].building.id == Data.BuildingID.portal)
                {
                    Hero ophero = null;

                    foreach (Data.Hero x in player.heroes)
                    {
                        if (x.id == Data.HeroID.sylvia)
                        {
                            ophero = GetHeroPrefab(x.id);
                            if (ophero)
                            {
                                Hero _opHero = Instantiate(ophero, UI_Main.instanse._grid.transform);
                                _opHero.transform.localPosition = BattlePositionToWorldPosotion(new Battle.BattleVector2(battleBuildings[i].building.x - 3, battleBuildings[i].building.y - 1));
                                _opHero.transform.localEulerAngles = Vector3.zero;
                                _opHero.positionTarget = _opHero.transform.localPosition;
                                _opHero._portal = buildingsOnGrid[i].building;
                                _opHero.waitingToAttack = true;
                                _opHero.isOp = true;
                                _opHero.Initialize(0, x.databaseID, x);
                                // _opHero.InitializeHero(true, false);
                                _opHero.healthBar = Instantiate(healthBarPrefab, healthBarGrid);
                                _opHero.healthBar.bar.fillAmount = 1;
                                _opHero.healthBar.gameObject.SetActive(false);

                                OpheroesOnGrid.Add(_opHero);
                                toAddOpHeroes.Add(new ItemToAdd(_opHero.databaseID, battleBuildings[i].building.x - 3, battleBuildings[i].building.y - 1));
                            }
                        }
                    }
                    //ophero = GetHeroPrefab(Data.HeroID.sylvia);

                }
            }

            for (int i = 0; i < buildingsOnGrid.Count; i++)
            {
                buildingsOnGrid[i].building.AdjustUI(true);
            }

            _findButton.gameObject.SetActive(_battleType == Data.BattleType.normal);
            if (_battleType == Data.BattleType.normal)
            {
                int townHallLevel = 1;
                for (int i = 0; i < Player.instanse.data.buildings.Count; i++)
                {
                    if (Player.instanse.data.buildings[i].id == Data.BuildingID.commandcenter)
                    {
                        townHallLevel = Player.instanse.data.buildings[i].level;
                    }
                }
                int cost = Data.GetBattleSearchCost(townHallLevel);
                Player.instanse.gold -= cost;
                Player.instanse.UpdateResourcesUI();
                _findCostText.text = cost.ToString();
                if (cost > Player.instanse.gold)
                {
                    _findButton.interactable = false;
                    _findCostText.color = Color.red;
                }
                else
                {
                    _findButton.interactable = true;
                    _findCostText.color = Color.white;
                }
            }
            if (!TutorialManager.Instance.isPvPTutorial)
                _closeButton.gameObject.SetActive(true);
            _surrenderButton.gameObject.SetActive(false);
            baseTime = DateTime.Now;
            SetStatus(true);

            CloudsAnimOut.SetActive(true);

            toAddSpells.Clear();
            toAddUnits.Clear();
            toAddHeroes.Clear();
            battle = new Battle();
            battle.Initialize(battleBuildings, DateTime.Now, BuildingAttackCallBack, BuildingDestroyedCallBack, BuildingDamageCallBack, StarGained);

            _percentageText.text = Mathf.RoundToInt((float)(battle.percentage * 100f)).ToString() + "%";
            UpdateLoots();

            var trophies = Data.GetBattleTrophies(Player.instanse.data.trophies, player.trophies);
            _winTrophiesText.text = trophies.Item1.ToString();
            _looseTrophiesText.text = "-" + trophies.Item2.ToString();

            surrender = false;
            readyToStart = true;
            isStarted = false;
            foreach (GameObject i in placeholders) { i.SetActive(true); i.transform.parent = null; i.transform.parent = battleItemsGrid; }
            if (TutorialManager.Instance.isPvPTutorial)
            {
                TutorialManager.Instance.SelectUnitFinger.SetActive(true);
            }

            if (TutorialManager.Instance.isPvPTutorial)
            {
                _closeButton.gameObject.SetActive(false);
                _findButton.gameObject.SetActive(false);
            }
            return true;
        }

        private async void SetOpData()
        {
            Data.OpponentData opponent = new Data.OpponentData();
            opponent.id = target;
            opponent.buildings = startbuildings;
            string data = await Data.SerializeAsync<Data.OpponentData>(opponent);
            opponentBytes = await Data.CompressAsync(data);
        }

        private void UpdateLoots()
        {
            var looted = battle.GetlootedResources();
            _lootGoldText.text = (looted.Item4 - looted.Item1).ToString();
            _lootElixirText.text = (looted.Item5 - looted.Item2).ToString();
            _lootDarkText.text = (looted.Item6 - looted.Item3).ToString();
        }

        private void StartBattle()
        {
            switch (Language.instanse.language)
            {
                case Language.LanguageID.persian:
                    _timerDescription.text = "زمان تا پایان حمله:";
                    break;
                default:
                    _timerDescription.text = "Battle Ends In:";
                    break;
            }
            _timerText.text = TimeSpan.FromSeconds(Data.battleDuration).ToString(@"mm\:ss");
            _findButton.gameObject.SetActive(false);
            _closeButton.gameObject.SetActive(false);
            if (!TutorialManager.Instance.isPvPTutorial)
                _surrenderButton.gameObject.SetActive(true);
            _damagePanel.SetActive(true);
            readyToStart = false;
            baseTime = DateTime.Now;
            Packet packet = new Packet();
            packet.Write((int)Player.RequestsID.BATTLESTART);
            packet.Write(opponentBytes.Length);
            packet.Write(opponentBytes);
            packet.Write((int)_battleType);
            Sender.TCP_Send(packet);
        }

        [Header("Lose Senerio")]
        public GameObject heroImage;
        public Image sunray;
        public Image Banner;
        public Image BannerText;
        public Image listBg;
        public Image Bg;
        public Image homeButton;
        public Image rewardBg;
        public Image rewardText;
        public Sprite _sunray;
        public Sprite _Banner;
        public Sprite _BannerText;
        public Sprite _listBg;
        public Sprite _Bg;
        public Sprite _homeButton;
        public Sprite _rewardBg;
        public Sprite _rewardText;


        Sprite win_sunray;
        Sprite win_Banner = null;
        Sprite win_BannerText;
        Sprite win_listBg;
        Sprite win_Bg;
        Sprite win_homeButton;
        Sprite win_rewardBg;
        Sprite win_rewardText;


        public void BattleEnded(int stars, int unitsDeployed, /*int heroesDeployed,*/ int lootedGold, int lootedElixir, int lootedDark, int trophies, int frame)
        {
            _findButton.gameObject.SetActive(false);
            _closeButton.gameObject.SetActive(false);
            _surrenderButton.gameObject.SetActive(false);
            var looted = battle.GetlootedResources();
            //Debug.Log("Battle Ended.");
            //Debug.Log("Frame -> Client:" + battle.frameCount + " Server:" + frame);
            //Debug.Log("Stars -> Client:" + battle.stars + " Server:" + stars);
            //Debug.Log("Units Deployed -> Client:" + battle.unitsDeployed + " Server:" + unitsDeployed);
            //Debug.Log("Looted Gold -> Client:" + looted.Item1 + " Server:" + lootedGold);
            //Debug.Log("Looted Elixir -> Client:" + looted.Item2 + " Server:" + lootedElixir);
            //Debug.Log("Looted Dark Elixir -> Client:" + looted.Item3 + " Server:" + lootedDark);
            //Debug.Log("Trophies -> Client:" + battle.GetTrophies() + " Server:" + trophies);
            _endTrophiesText.text = trophies.ToString();
            _endGoldText.text = lootedGold.ToString();
            _endElixirText.text = lootedElixir.ToString();
            _endDarkText.text = lootedDark.ToString();
            if (_endWinEffects != null)
            {
                _endWinEffects.SetActive(stars > 0);
            }
            _endStar1.SetActive(stars > 0);
            _endStar2.SetActive(stars > 1);
            _endStar3.SetActive(stars > 2);
            if (stars > 0)
            {
                switch (Language.instanse.language)
                {
                    case Language.LanguageID.persian:
                        _endText.text = "پیروزی";
                        break;
                    default:
                        _endText.text = "Victory";
                        break;
                }
                SoundManager.instanse.PlaySound(SoundManager.SoundType.BattleWin);
                Banner.sprite = win_Banner;
                sunray.sprite = win_sunray;
                BannerText.sprite = win_BannerText;
                listBg.sprite = win_listBg;
                Bg.sprite = win_Bg;
                homeButton.sprite = win_homeButton;
                rewardBg.sprite = win_rewardBg;
                rewardText.sprite = win_rewardText;
                heroImage.SetActive(true);
            }
            else
            {
                switch (Language.instanse.language)
                {
                    case Language.LanguageID.persian:
                        _endText.text = "شکست";
                        break;
                    default:
                        _endText.text = "Defeat";
                        break;
                }
                SoundManager.instanse.PlaySound(SoundManager.SoundType.BattleFail);
                Banner.sprite = _Banner;
                sunray.sprite = _sunray;
                BannerText.sprite = _BannerText;
                listBg.sprite = _listBg;
                Bg.sprite = _Bg;
                homeButton.sprite = _homeButton;
                rewardBg.sprite = _rewardBg;
                rewardText.sprite = _rewardText;
                heroImage.SetActive(false);

            }
            for (int i = healthBarGrid.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(healthBarGrid.transform.GetChild(i).gameObject);
            }
            _infoPanel.SetActive(false);
            _endPanel.SetActive(true);
            if (Monelytica.Instance != null)
                Monelytica.Instance.ShowInterestitial(Monelytica.Placeholders.LevelComplete);
        }

        public void StartBattleConfirm(bool confirmed, List<Data.BattleStartBuildingData> buildings, int winTrophies, int loseTrophies)
        {
            if (confirmed)
            {
                battle.winTrophies = winTrophies;
                battle.loseTrophies = loseTrophies;
                for (int i = 0; i < battle._buildings.Count; i++)
                {
                    bool resource = false;
                    switch (battle._buildings[i].building.id)
                    {
                        case Data.BuildingID.commandcenter:
                        case Data.BuildingID.mine:
                        case Data.BuildingID.vault:
                        case Data.BuildingID.powerplant:
                        case Data.BuildingID.battery:
                        case Data.BuildingID.gloommill:
                        case Data.BuildingID.warehouse:
                            resource = true;
                            break;
                            //case Data.BuildingID.portal:
                            //    battle.AddHero();
                            //    break;
                    }
                    if (!resource)
                    {
                        continue;
                    }
                    for (int j = 0; j < buildings.Count; j++)
                    {
                        if (battle._buildings[i].building.databaseID != buildings[j].databaseID)
                        {
                            continue;
                        }
                        battle._buildings[i].lootGoldStorage = buildings[j].lootGoldStorage;
                        battle._buildings[i].lootPowerStorage = buildings[j].lootPowerStorage;
                        battle._buildings[i].lootWoodStorage = buildings[j].lootWoodStorage;
                        break;
                    }

                }
                isStarted = true;
                CharacterManager.instanse.Attack();
                BuilderManager.instanse.Attack();
                UI_Train.instanse.Attack();
            }
            else
            {
                Debug.Log("Battle is not confirmed by the server.");
            }
        }

        private void Surrender()
        {
            surrender = true;
        }

        public void EndBattle(bool surrender, int surrenderFrame)
        {

            _findButton.gameObject.SetActive(false);
            _closeButton.gameObject.SetActive(false);
            _surrenderButton.gameObject.SetActive(false);
            battle.end = true;
            battle.surrender = surrender;
            isStarted = false;
            Packet packet = new Packet();
            packet.Write((int)Player.RequestsID.BATTLEEND);
            packet.Write(surrender);
            packet.Write(surrenderFrame);
            Sender.TCP_Send(packet);
            //Debug.Log("End Battle Called");        
        }

        private void ClearAllRemovables()
        {
            foreach (GameObject removable in removables)
                Destroy(removable);
            removables.Clear();
        }

        [SerializeField] private GameObject _elements = null;
        [SerializeField] private RectTransform battleItemsGrid = null;
        [SerializeField] private RectTransform battleItemsGridRoot = null;
        [SerializeField] public UI_BattleUnit unitsPrefab = null;
        [SerializeField] public UI_Hero heroesPrefab = null;
        [SerializeField] public UI_BattleSpell spellsPrefab = null;
        [SerializeField] public GameObject[] placeholders = null;
        private static UI_Battle _instance = null; public static UI_Battle instanse { get { return _instance; } }
        private bool _active = false; public bool isActive { get { return _active; } }

        [HideInInspector] public int selectedUnit = -1;
        [HideInInspector] public int selectedHero = -1;
        [HideInInspector] public int selectedSpell = -1;

        private List<UI_BattleUnit> units = new List<UI_BattleUnit>();
        private List<UI_Hero> heroes = new List<UI_Hero>();
        private List<UI_BattleSpell> spells = new List<UI_BattleSpell>();


        public void SpellSelected(Data.SpellID id)
        {
            if (selectedUnit >= 0)
            {
                units[selectedUnit].Deselect();
                selectedUnit = -1;
            }
            if (selectedHero >= 0)
            {
                heroes[selectedHero].Deselect();
                selectedHero = -1;
            }
            if (selectedSpell >= 0)
            {
                spells[selectedSpell].Deselect();
                selectedSpell = -1;
            }
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].id == id)
                {
                    selectedSpell = i;
                    break;
                }
            }
            if (selectedSpell >= 0 && spells[selectedSpell].count <= 0)
            {
                spells[selectedSpell].Deselect();
                selectedSpell = -1;
            }
        }

        public void UnitSelected(Data.UnitID id)
        {
            if (selectedUnit >= 0)
            {
                units[selectedUnit].Deselect();
                selectedUnit = -1;
            }
            if (selectedHero >= 0)
            {
                heroes[selectedHero].Deselect();
                selectedHero = -1;
            }
            if (selectedSpell >= 0)
            {
                spells[selectedSpell].Deselect();
                selectedSpell = -1;
            }
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].id == id)
                {
                    selectedUnit = i;
                    break;
                }
            }
            if (selectedUnit >= 0 && units[selectedUnit].count <= 0)
            {
                units[selectedUnit].Deselect();
                selectedUnit = -1;
            }
            if (TutorialManager.Instance.isPvPTutorial && !tutorialUnitSpawnStarted)
            {
                TutorialManager.Instance.SelectUnitFinger.SetActive(false);
                TutorialManager.Instance.placeUnitFinger1.SetActive(true);
            }
        }
        public void HeroSelected(Data.HeroID id)
        {
            if (selectedUnit >= 0)
            {
                units[selectedUnit].Deselect();
                selectedUnit = -1;
            }
            if (selectedHero >= 0)
            {
                heroes[selectedHero].Deselect();
                selectedHero = -1;
            }
            if (selectedSpell >= 0)
            {
                spells[selectedSpell].Deselect();
                selectedSpell = -1;
            }
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i].id == id)
                {
                    selectedHero = i;
                    break;
                }
            }
            if (selectedHero >= 0 && heroes[selectedHero].count <= 0)
            {
                heroes[selectedHero].Deselect();
                selectedHero = -1;
            }
        }
        public void PlaceUnit(int x, int y)
        {
            if (battle != null && opponentBytes != null)
            {
                if (selectedUnit >= 0 && units[selectedUnit].count > 0 && battle.CanAddUnit(x, y))
                {
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.UnitReady);
                    if (!isStarted)
                    {
                        if (!readyToStart)
                        {
                            return;
                        }
                        StartBattle();
                    }
                    long id = units[selectedUnit].Get();
                    if (id >= 0)
                    {
                        if (units[selectedUnit].count <= 0)
                        {
                            units[selectedUnit].Deselect();
                            selectedUnit = -1;
                        }
                        toAddUnits.Add(new ItemToAdd(id, x, y));
                    }
                    tutorialTaped();
                }
                else if (selectedHero >= 0 && heroes[selectedHero].count > 0 && battle.CanAddHero(x, y))
                {
                    SoundManager.instanse.PlaySound(SoundManager.SoundType.UnitReady);
                    if (!isStarted)
                    {
                        if (!readyToStart)
                        {
                            return;
                        }
                        StartBattle();
                    }
                    long id = heroes[selectedHero].Get();
                    if (id >= 0)
                    {
                        if (heroes[selectedHero].count <= 0)
                        {
                            heroes[selectedHero].Deselect();
                            selectedHero = -1;
                        }
                        toAddHeroes.Add(new ItemToAdd(id, x, y));
                    }
                    tutorialTaped();
                }
                else if (selectedSpell >= 0 && spells[selectedSpell].count > 0 && battle.CanAddSpell(x, y))
                {
                    if (!isStarted)
                    {
                        if (!readyToStart)
                        {
                            return;
                        }
                        StartBattle();
                    }
                    long id = spells[selectedSpell].Get();
                    if (id >= 0)
                    {
                        if (spells[selectedSpell].count <= 0)
                        {
                            spells[selectedSpell].Deselect();
                            selectedSpell = -1;
                        }
                        toAddSpells.Add(new ItemToAdd(id, x, y));
                    }
                }

            }
        }
        public bool tutorialUnitSpawnStarted = false;
        private void tutorialTaped()
        {
            if (TutorialManager.Instance.isPvPTutorial)
            {
                switch (tutorialsteps)
                {
                    case 0:
                        tutorialUnitSpawnStarted = true;
                        TutorialManager.Instance.placeUnitFinger1.SetActive(false);
                        TutorialManager.Instance.placeUnitFinger2.SetActive(true);
                        break;
                    case 1:
                        TutorialManager.Instance.placeUnitFinger2.SetActive(false);
                        TutorialManager.Instance.placeUnitFinger3.SetActive(true);
                        break;
                    case 2:
                        TutorialManager.Instance.placeUnitFinger3.SetActive(false);
                        TutorialManager.Instance.stopCameratillTaps = false;

                        break;
                }
                tutorialsteps++;
            }
        }

        int tutorialsteps = 0;
        private void Awake()
        {
            _instance = this;
            _elements.SetActive(false);
        }

        private void ClearUnits()
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i])
                {
                    Destroy(units[i].gameObject);
                }
            }
            units.Clear();
        }
        private void ClearTrainingUnits()
        {
            UI_Train.instanse._ParentElement.SetActive(false);
            for (int i = 0; i < UI_Train.instanse.battleUnits.Count; i++)
            {
                if (UI_Train.instanse.battleUnits[i])
                {
                    UI_Train.instanse.battleUnits[i].TweenCancel();
                    Destroy(UI_Train.instanse.battleUnits[i].gameObject);
                }
            }
            UI_Train.instanse.battleUnits.Clear();
            //ClearTrainingItems();
            //_ParentElement.SetActive(false);
        }
        private void ClearHeroes()
        {
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i])
                {
                    Destroy(heroes[i].gameObject);
                }
            }
            heroes.Clear();
        }
        private void ClearBuilders()
        {
            BuilderManager.instanse.clearBuilders();
        }
        private void ClearSpells()
        {
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i])
                {
                    Destroy(spells[i].gameObject);
                }
            }
            spells.Clear();
        }

        public void SetStatus(bool status)
        {
            if (!status)
            {
                ClearSpells();
                ClearBuildingsOnGrid();
                ClearUnitsOnGrid();
                ClearHeroesOnGrid();
                ClearOpHeroesOnGrid();
                ClearUnits();
                ClearHeroes();
            }
            else
            {
                _endPanel.SetActive(false);
                _infoPanel.SetActive(true);
            }
            Player.inBattle = status;
            _active = status;
            _elements.SetActive(status);
        }

        private void Update()
        {
            if (battle != null && battle.end == false)
            {
                if (isStarted)
                {
                    TimeSpan span = DateTime.Now - baseTime;

                    if (_timerText != null)
                    {
                        _timerText.text = TimeSpan.FromSeconds(Data.battleDuration - span.TotalSeconds).ToString(@"mm\:ss");
                    }

                    int frame = (int)Math.Floor(span.TotalSeconds / Data.battleFrameRate);
                    if (frame > battle.frameCount)
                    {
                        if (toAddUnits.Count > 0 || toAddSpells.Count > 0 || toAddHeroes.Count > 0)
                        {
                            Data.BattleFrame battleFrame = new Data.BattleFrame();
                            battleFrame.frame = battle.frameCount + 1;

                            if (toAddUnits.Count > 0)
                            {
                                for (int i = toAddUnits.Count - 1; i >= 0; i--)
                                {
                                    for (int j = 0; j < Player.instanse.data.units.Count; j++)
                                    {
                                        if (Player.instanse.data.units[j].databaseID == toAddUnits[i].id)
                                        {
                                            //Debug.Log("UNIT" + toAddUnits[i].x + "," + toAddUnits[i].y);
                                            battle.AddUnit(Player.instanse.data.units[j], toAddUnits[i].x, toAddUnits[i].y, UnitSpawnCallBack, UnitAttackCallBack, UnitDiedCallBack, UnitDamageCallBack, UnitHealCallBack, UnitTargetSelectedCallBack);
                                            Data.BattleFrameUnit bfu = new Data.BattleFrameUnit();
                                            bfu.id = Player.instanse.data.units[j].databaseID;
                                            bfu.x = toAddUnits[i].x;
                                            bfu.y = toAddUnits[i].y;
                                            battleFrame.units.Add(bfu);
                                            break;
                                        }
                                    }
                                    toAddUnits.RemoveAt(i);
                                }
                            }

                            if (toAddHeroes.Count > 0)
                            {
                                for (int i = toAddHeroes.Count - 1; i >= 0; i--)
                                {
                                    for (int j = 0; j < Player.instanse.data.heroes.Count; j++)
                                    {
                                        if (Player.instanse.data.heroes[j].databaseID == toAddHeroes[i].id)
                                        {
                                            //Debug.Log("HERO" + toAddHeroes[i].x + "," + toAddHeroes[i].y);
                                            battle.AddHero(Player.instanse.data.heroes[j], toAddHeroes[i].x, toAddHeroes[i].y, HeroSpawnCallBack, HeroAttackCallBack, HeroDiedCallBack, HeroDamageCallBack, HeroHealCallBack, HeroTargetSelectedCallBack);
                                            Data.BattleFrameHero bfh = new Data.BattleFrameHero();
                                            bfh.id = Player.instanse.data.heroes[j].databaseID;
                                            bfh.x = toAddHeroes[i].x;
                                            bfh.y = toAddHeroes[i].y;
                                            battleFrame.heroes.Add(bfh);
                                            break;
                                        }
                                    }
                                    toAddHeroes.RemoveAt(i);
                                }
                            }
                            if (toAddOpHeroes.Count > 0)
                            {
                                for (int i = toAddOpHeroes.Count - 1; i >= 0; i--)
                                {
                                    for (int j = 0; j < OpponentPlayerData.heroes.Count; j++)
                                    {
                                        // if ( Player.instanse.data.heroes[j].databaseID == toAddOpHeroes[i].id)
                                        {
                                            //Debug.Log("OPHERO" + toAddOpHeroes[i].x + "," + toAddOpHeroes[i].y);
                                            battle.AddOpHero(OpponentPlayerData.heroes[j], toAddOpHeroes[i].x, toAddOpHeroes[i].y, null, OpHeroAttackCallBack, OpHeroDiedCallBack, OpHeroDamageCallBack, null, OpHeroTargetSelectedCallBack);
                                            Data.BattleFrameHero bfh = new Data.BattleFrameHero();
                                            bfh.id = OpponentPlayerData.heroes[j].databaseID;
                                            bfh.x = toAddOpHeroes[i].x;
                                            bfh.y = toAddOpHeroes[i].y;
                                            battleFrame.opHeroes.Add(bfh);
                                            break;
                                        }
                                    }
                                    toAddOpHeroes.RemoveAt(i);
                                }
                            }

                            if (toAddSpells.Count > 0)
                            {
                                for (int i = toAddSpells.Count - 1; i >= 0; i--)
                                {
                                    for (int j = 0; j < Player.instanse.data.spells.Count; j++)
                                    {
                                        if (Player.instanse.data.spells[j].databaseID == toAddSpells[i].id)
                                        {
                                            Data.Spell spell = Player.instanse.data.spells[j];
                                            Player.instanse.AssignServerSpell(ref spell);
                                            battle.AddSpell(spell, toAddSpells[i].x, toAddSpells[i].y, SpellSpawnCallBack, SpellPalseCallBack, SpellEndCallBack);
                                            Data.BattleFrameSpell bfs = new Data.BattleFrameSpell();
                                            bfs.id = spell.databaseID;
                                            bfs.x = toAddSpells[i].x;
                                            bfs.y = toAddSpells[i].y;
                                            battleFrame.spells.Add(bfs);
                                            break;
                                        }
                                    }
                                    toAddSpells.RemoveAt(i);
                                }
                            }

                            Packet packet = new Packet();
                            packet.Write((int)Player.RequestsID.BATTLEFRAME);
                            byte[] bytes = Data.Compress(Data.Serialize<Data.BattleFrame>(battleFrame));
                            packet.Write(bytes.Length);
                            packet.Write(bytes);
                            Sender.TCP_Send(packet);
                        }
                        battle.ExecuteFrame();
                        if ((float)battle.frameCount * Data.battleFrameRate >= battle.duration || Math.Abs(battle.percentage - 1d) <= 0.0001d)
                        {
                            EndBattle(false, battle.frameCount);
                        }
                        else if (surrender || (!battle.IsAliveUnitsOnGrid() && !HaveUnitLeftToPlace() && !battle.IsAliveHeroesOnGrid() && !HaveHeroLeftToPlace()))
                        {
                            EndBattle(true, battle.frameCount);
                        }
                    }
                }
                else if (readyToStart)
                {
                    TimeSpan span = DateTime.Now - baseTime;
                    if (span.TotalSeconds >= Data.battlePrepDuration)
                    {
                        StartBattle();
                    }
                    else
                    {
                        _timerText.text = TimeSpan.FromSeconds(Data.battlePrepDuration - span.TotalSeconds).ToString(@"mm\:ss");
                    }
                }
                UpdateUnits();
                UpdateHeroes();
                UpdateOpHeroes();
                UpdateBuildings();
            }
        }

        private bool HaveUnitLeftToPlace()
        {
            if (units.Count > 0)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i].count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool HaveHeroLeftToPlace()
        {
            if (heroes.Count > 0)
            {
                for (int i = 0; i < heroes.Count; i++)
                {
                    if (heroes[i].count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public BattleUnit GetUnitPrefab(Data.UnitID id)
        {
            for (int i = 0; i < battleUnits.Length; i++)
            {
                if (battleUnits[i].id == id)
                {
                    return battleUnits[i];
                }
            }
            return null;
        }
        public Hero GetHeroPrefab(Data.HeroID id)
        {
            for (int i = 0; i < hero.Length; i++)
            {
                if (hero[i].id == id)
                {
                    return hero[i];
                }
            }
            return null;
        }

        public void ClearUnitsOnGrid()
        {
            for (int i = 0; i < unitsOnGrid.Count; i++)
            {
                if (unitsOnGrid[i])
                {
                    Destroy(unitsOnGrid[i].gameObject);
                }
            }
            unitsOnGrid.Clear();
        }
        public void ClearHeroesOnGrid()
        {
            for (int i = 0; i < heroesOnGrid.Count; i++)
            {
                if (heroesOnGrid[i])
                {
                    Destroy(heroesOnGrid[i].gameObject);
                }
            }
            heroesOnGrid.Clear();
        }
        public void ClearOpHeroesOnGrid()
        {
            for (int i = 0; i < OpheroesOnGrid.Count; i++)
            {
                if (OpheroesOnGrid[i])
                {
                    Destroy(OpheroesOnGrid[i].gameObject);
                }
            }
            OpheroesOnGrid.Clear();
        }
        public void ClearBuildingsOnGrid()
        {
            for (int i = 0; i < buildingsOnGrid.Count; i++)
            {
                if (buildingsOnGrid[i].building != null)
                {
                    Destroy(buildingsOnGrid[i].building.gameObject);
                }
            }
            buildingsOnGrid.Clear();
        }

        public static Vector3 BattlePositionToWorldPosotion(Battle.BattleVector2 position)
        {
            Vector3 result = new Vector3(position.x * UI_Main.instanse._grid.cellSize, position.y * UI_Main.instanse._grid.cellSize, 0);
            result = UI_Main.instanse._grid.xDirection * result.x + UI_Main.instanse._grid.yDirection * result.y;
            return result;
        }

        #region Events
        private void UpdateUnits()
        {
            for (int i = 0; i < unitsOnGrid.Count; i++)
            {
                if (battle._units[unitsOnGrid[i].index].health > 0)
                {
                    unitsOnGrid[i].moving = battle._units[unitsOnGrid[i].index].moving;
                    unitsOnGrid[i].positionTarget = BattlePositionToWorldPosotion(battle._units[unitsOnGrid[i].index].positionOnGrid);
                    if (battle._units[unitsOnGrid[i].index].health < battle._units[unitsOnGrid[i].index].unit.health)
                    {
                        unitsOnGrid[i].healthBar.gameObject.SetActive(true);
                        unitsOnGrid[i].healthBar.bar.fillAmount = battle._units[unitsOnGrid[i].index].health / battle._units[unitsOnGrid[i].index].unit.health;
                        Vector2 localPoint;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                              unitsOnGrid[i].healthBar.rect.parent as RectTransform, GetUnitBarPosition(unitsOnGrid[i].transform.position), null, out localPoint);
                        unitsOnGrid[i].healthBar.rect.anchoredPosition = localPoint;
                    }
                    else
                    {
                        if (unitsOnGrid[i].healthBar)
                            unitsOnGrid[i].healthBar.gameObject.SetActive(false);
                    }
                }
            }
        }
        private void UpdateHeroes()
        {
            for (int i = 0; i < heroesOnGrid.Count; i++)
            {
                if (battle._heroes[heroesOnGrid[i].index].health > 0)
                {
                    heroesOnGrid[i].moving = battle._heroes[heroesOnGrid[i].index].moving;
                    heroesOnGrid[i].positionTarget = BattlePositionToWorldPosotion(battle._heroes[heroesOnGrid[i].index].positionOnGrid);
                    if (battle._heroes[heroesOnGrid[i].index].health < battle._heroes[heroesOnGrid[i].index].hero.health)
                    {
                        heroesOnGrid[i].healthBar.gameObject.SetActive(true);
                        heroesOnGrid[i].healthBar.bar.fillAmount = battle._heroes[heroesOnGrid[i].index].health / battle._heroes[heroesOnGrid[i].index].hero.health;
                        Vector2 localPoint;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                              heroesOnGrid[i].healthBar.rect.parent as RectTransform, GetHeroBarPosition(heroesOnGrid[i].transform.position), null, out localPoint);
                        heroesOnGrid[i].healthBar.rect.anchoredPosition = localPoint;
                    }
                    else
                    {
                        heroesOnGrid[i].healthBar.gameObject.SetActive(false);
                    }
                }
            }
        }
        private void UpdateOpHeroes()
        {
            for (int i = 0; i < OpheroesOnGrid.Count; i++)
            {
                if (battle._opHeroes.Count > OpheroesOnGrid[i].index)
                {

                    if (battle._opHeroes[OpheroesOnGrid[i].index].health > 0)
                    {
                        OpheroesOnGrid[i].moving = battle._opHeroes[OpheroesOnGrid[i].index].moving;
                        OpheroesOnGrid[i].positionTarget = BattlePositionToWorldPosotion(battle._opHeroes[OpheroesOnGrid[i].index].positionOnGrid);
                        if (battle._opHeroes[OpheroesOnGrid[i].index].health < battle._opHeroes[OpheroesOnGrid[i].index].opHero.health)
                        {
                            OpheroesOnGrid[i].healthBar.gameObject.SetActive(true);
                            OpheroesOnGrid[i].healthBar.bar.fillAmount = battle._opHeroes[OpheroesOnGrid[i].index].health / battle._opHeroes[OpheroesOnGrid[i].index].opHero.health;
                            Vector2 localPoint;
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                  OpheroesOnGrid[i].healthBar.rect.parent as RectTransform, GetHeroBarPosition(OpheroesOnGrid[i].transform.position), null, out localPoint);
                            OpheroesOnGrid[i].healthBar.rect.anchoredPosition = localPoint;
                        }
                        else
                        {
                            OpheroesOnGrid[i].healthBar.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }
        private void UpdateBuildings()
        {
            for (int i = 0; i < buildingsOnGrid.Count; i++)
            {
                if (battle._buildings[buildingsOnGrid[i].index].health > 0)
                {
                    if (battle._buildings[buildingsOnGrid[i].index].health < battle._buildings[buildingsOnGrid[i].index].building.health)
                    {
                        buildingsOnGrid[i].building.healthBar.gameObject.SetActive(true);
                        buildingsOnGrid[i].building.healthBar.bar.fillAmount = battle._buildings[buildingsOnGrid[i].index].health / battle._buildings[buildingsOnGrid[i].index].building.health;
                        Vector2 localPoint;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                              buildingsOnGrid[i].building.healthBar.rect.parent as RectTransform, GetUnitBarPosition(UI_Main.instanse._grid.GetEndPosition(buildingsOnGrid[i].building)), null, out localPoint);
                        buildingsOnGrid[i].building.healthBar.rect.anchoredPosition = localPoint;
                    }
                }
            }
        }

        private Vector2 GetUnitBarPosition(Vector3 position)
        {
            Vector3 planDownLeft = CameraController.instanse.planDownLeft;
            Vector3 planTopRight = CameraController.instanse.planTopRight;

            float w = planTopRight.x - planDownLeft.x;
            float h = planTopRight.y - planDownLeft.y;

            float endW = position.x - planDownLeft.x;
            float endH = position.y - planDownLeft.y;

            return new Vector2(endW / w * Screen.width, endH / h * Screen.height);
        }

        private Vector2 GetHeroBarPosition(Vector3 position)
        {
            Vector3 planDownLeft = CameraController.instanse.planDownLeft;
            Vector3 planTopRight = CameraController.instanse.planTopRight;

            float w = planTopRight.x - planDownLeft.x;
            float h = planTopRight.y - planDownLeft.y;

            float endW = position.x - planDownLeft.x;
            float endH = position.y - planDownLeft.y;

            return new Vector2(endW / w * Screen.width, endH / h * Screen.height);
        }

        private List<UI_SpellEffect> spellEffects = new List<UI_SpellEffect>();

        public void SpellSpawnCallBack(long databaseID, Data.SpellID id, Battle.BattleVector2 target, float radius)
        {
            Vector3 position = BattlePositionToWorldPosotion(target);
            //Vector3 position = new Vector3(target.x, 0, target.y);
            position = UI_Main.instanse._grid.transform.TransformPoint(position);
            UI_SpellEffect effect = Instantiate(spellEffectPrefab, position, Quaternion.identity);
            effect.Initialize(id, databaseID, radius * UI_Main.instanse._grid.cellSize);
            spellEffects.Add(effect);
        }

        public void SpellPalseCallBack(long id)
        {
            for (int i = 0; i < spellEffects.Count; i++)
            {
                if (spellEffects[i].DatabaseID == battle._spells[i].spell.databaseID)
                {
                    spellEffects[i].Pulse();
                    break;
                }
            }
        }

        public void SpellEndCallBack(long id)
        {
            for (int i = 0; i < spellEffects.Count; i++)
            {
                if (spellEffects[i].DatabaseID == id)
                {
                    spellEffects[i].End();
                    spellEffects.RemoveAt(i);
                    break;
                }
            }
        }

        public void UnitSpawnCallBack(long id)
        {
            int u = -1;
            for (int i = 0; i < battle._units.Count; i++)
            {
                if (battle._units[i].unit.databaseID == id)
                {
                    u = i;
                    break;
                }
            }
            if (u >= 0)
            {
                //foreach(Hero i in OpheroesOnGrid)
                //{
                //   // i.waitingToAttack = false;
                //}
                BattleUnit prefab = GetUnitPrefab(battle._units[u].unit.id);
                if (prefab)
                {
                    BattleUnit unit = Instantiate(prefab, UI_Main.instanse._grid.transform);
                    unit.transform.localPosition = BattlePositionToWorldPosotion(battle._units[u].positionOnGrid);
                    //unit.transform.rotation = Quaternion.LookRotation(new Vector3(0, unit.transform.position.y, 0) - unit.transform.position);
                    unit.transform.localEulerAngles = Vector3.zero;
                    unit.positionTarget = unit.transform.localPosition;
                    unit.Initialize(u, battle._units[u].unit.databaseID, battle._units[u].unit);
                    unit.healthBar = Instantiate(healthBarPrefab, healthBarGrid);
                    unit.healthBar.bar.fillAmount = 1;
                    unit.healthBar.gameObject.SetActive(false);
                    unitsOnGrid.Add(unit);
                }
            }
        }
        public void HeroSpawnCallBack(long id)
        {
            int u = -1;
            for (int i = 0; i < battle._heroes.Count; i++)
            {
                if (battle._heroes[i].hero.databaseID == id)
                {
                    u = i;
                    break;
                }
            }
            if (u >= 0)
            {
                foreach (Hero i in OpheroesOnGrid)
                {
                    //  i.waitingToAttack = false;
                }
                Hero prefab = GetHeroPrefab(battle._heroes[u].hero.id);
                if (prefab)
                {
                    Hero hero = Instantiate(prefab, UI_Main.instanse._grid.transform);
                    hero.transform.localPosition = BattlePositionToWorldPosotion(battle._heroes[u].positionOnGrid);
                    //unit.transform.rotation = Quaternion.LookRotation(new Vector3(0, unit.transform.position.y, 0) - unit.transform.position);
                    hero.transform.localEulerAngles = Vector3.zero;
                    hero.positionTarget = hero.transform.localPosition;
                    hero.Initialize(u, battle._heroes[u].hero.databaseID, battle._heroes[u].hero);
                    hero.healthBar = Instantiate(healthBarPrefab, healthBarGrid);
                    hero.healthBar.bar.fillAmount = 1;
                    hero.healthBar.gameObject.SetActive(false);
                    heroesOnGrid.Add(hero);
                }
            }
        }
        public void OpHeroSpawnCallBack(long id)
        {
            int u = -1;
            for (int i = 0; i < battle._opHeroes.Count; i++)
            {
                if (battle._opHeroes[i].opHero.databaseID == id)
                {
                    u = i;
                    break;
                }
            }
            if (u >= 0)
            {

                Hero prefab = GetHeroPrefab(battle._opHeroes[u].opHero.id);
                if (prefab)
                {
                    Hero ophero = Instantiate(prefab, UI_Main.instanse._grid.transform);
                    ophero.transform.localPosition = BattlePositionToWorldPosotion(battle._opHeroes[u].positionOnGrid);
                    ophero.transform.localEulerAngles = Vector3.zero;
                    ophero.positionTarget = ophero.transform.localPosition;
                    ophero.Initialize(u, battle._opHeroes[u].opHero.databaseID, battle._opHeroes[u].opHero);
                    ophero.healthBar = Instantiate(healthBarPrefab, healthBarGrid);
                    ophero.healthBar.bar.fillAmount = 1;
                    ophero.healthBar.gameObject.SetActive(false);

                    foreach (Hero i in OpheroesOnGrid)
                    {
                        if (i.databaseID == ophero.databaseID)
                        {
                            Destroy(i.gameObject);
                            OpheroesOnGrid.Remove(i);
                        }
                    }
                    OpheroesOnGrid.Add(ophero);
                }
            }
        }
        public void StarGained()
        {
            if (battle.stars == 1)
            {
                _star1.SetActive(true);
            }
            else if (battle.stars == 2)
            {
                _star2.SetActive(true);
            }
            else if (battle.stars == 3)
            {
                _star3.SetActive(true);
            }
        }

        public void UnitAttackCallBack(long id, long target, bool isBuilding)
        {
            int u = -1;
            int b = -1;
            for (int i = 0; i < unitsOnGrid.Count; i++)
            {
                if (unitsOnGrid[i].databaseID == id)
                {
                    u = i;
                    break;
                }
            }
            if (u >= 0)
            {
                for (int i = 0; i < buildingsOnGrid.Count; i++)
                {
                    if (buildingsOnGrid[i].building.databaseID == target)
                    {
                        b = i;
                        isBuilding = true;
                        break;
                    }
                }
                if (!isBuilding)
                {
                    for (int i = 0; i < OpheroesOnGrid.Count; i++)
                    {
                        if (OpheroesOnGrid[i].databaseID == target)
                        {
                            b = i;
                            break;
                        }
                    }
                }
                if (b >= 0)
                {
                    if (unitsOnGrid[u].projectilePrefab && unitsOnGrid[u].shootPoint && unitsOnGrid[u].data.attackRange > 0f && unitsOnGrid[u].data.rangedSpeed > 0f)
                    {
                        UI_Projectile projectile = Instantiate(unitsOnGrid[u].projectilePrefab);
                        if (isBuilding)
                            projectile.Initialize(unitsOnGrid[u].shootPoint.position, buildingsOnGrid[b].building.shootTarget, unitsOnGrid[u].data.rangedSpeed * UI_Main.instanse._grid.cellSize);
                        else
                            projectile.Initialize(unitsOnGrid[u].shootPoint.position, OpheroesOnGrid[b].targetPoint, unitsOnGrid[u].data.rangedSpeed * UI_Main.instanse._grid.cellSize);

                    }
                    if (isBuilding)
                        unitsOnGrid[u].Attack(buildingsOnGrid[b].building.transform.position);
                    else
                        unitsOnGrid[u].Attack(OpheroesOnGrid[b].transform.position);
                }
                else
                {
                    unitsOnGrid[u].Attack();
                }
            }
        }

        public void HeroAttackCallBack(long id, long target, bool isBuilding)
        {
            int u = -1;
            int b = -1;
            for (int i = 0; i < heroesOnGrid.Count; i++)
            {
                if (heroesOnGrid[i].databaseID == id)
                {
                    u = i;
                    break;
                }
            }
            if (u >= 0)
            {
                for (int i = 0; i < buildingsOnGrid.Count; i++)
                {
                    if (buildingsOnGrid[i].building.databaseID == target)
                    {
                        b = i;
                        isBuilding = true;
                        break;
                    }
                }
                if (!isBuilding)
                {

                    for (int i = 0; i < OpheroesOnGrid.Count; i++)
                    {
                        if (OpheroesOnGrid[i].databaseID == target)
                        {
                            b = i;
                            break;
                        }
                    }
                }
                if (b >= 0)
                {
                    if (heroesOnGrid[u].projectilePrefab && heroesOnGrid[u].shootPoint && heroesOnGrid[u].data.attackRange > 0f && heroesOnGrid[u].data.rangedSpeed > 0f)
                    {
                        UI_Projectile projectile = Instantiate(heroesOnGrid[u].projectilePrefab);
                        if (isBuilding)
                            projectile.Initialize(heroesOnGrid[u].shootPoint.position, buildingsOnGrid[b].building.shootTarget, heroesOnGrid[u].data.rangedSpeed * UI_Main.instanse._grid.cellSize);
                        else
                            projectile.Initialize(heroesOnGrid[u].shootPoint.position, OpheroesOnGrid[b].targetPoint, heroesOnGrid[u].data.rangedSpeed * UI_Main.instanse._grid.cellSize);

                    }
                    if (isBuilding)
                        heroesOnGrid[u].Attack(buildingsOnGrid[b].building.transform.position);
                    else
                        heroesOnGrid[u].Attack(OpheroesOnGrid[b].transform.position);
                }
                else
                {
                    heroesOnGrid[u].Attack();
                }
            }
        }
        public void OpHeroAttackCallBack(long id, long target, bool isUnit)
        {
            int u = -1;
            int b = -1;
            for (int i = 0; i < OpheroesOnGrid.Count; i++)
            {
                if (OpheroesOnGrid[i].databaseID == id)
                {
                    u = i;
                    break;
                }
            }
            if (u >= 0)
            {
                for (int i = 0; i < unitsOnGrid.Count; i++)
                {
                    if (unitsOnGrid[i].databaseID == target)
                    {
                        b = i;
                        isUnit = true;
                        break;
                    }
                }
                if (!isUnit)
                {

                    for (int i = 0; i < heroesOnGrid.Count; i++)
                    {
                        if (heroesOnGrid[i].databaseID == target)
                        {
                            b = i;
                            break;
                        }
                    }
                }

                if (b >= 0)
                {
                    if (OpheroesOnGrid[u].projectilePrefab && OpheroesOnGrid[u].shootPoint && OpheroesOnGrid[u].data.attackRange > 0f && OpheroesOnGrid[u].data.rangedSpeed > 0f)
                    {
                        UI_Projectile projectile = Instantiate(OpheroesOnGrid[u].projectilePrefab);

                        if (isUnit)
                            projectile.Initialize(OpheroesOnGrid[u].shootPoint.position, unitsOnGrid[b].targetPoint, OpheroesOnGrid[u].data.rangedSpeed * UI_Main.instanse._grid.cellSize);
                        else
                            projectile.Initialize(OpheroesOnGrid[u].shootPoint.position, heroesOnGrid[b].targetPoint, OpheroesOnGrid[u].data.rangedSpeed * UI_Main.instanse._grid.cellSize);

                    }
                    if (isUnit)
                        OpheroesOnGrid[u].Attack(unitsOnGrid[b].transform.position);
                    else
                        OpheroesOnGrid[u].Attack(heroesOnGrid[b].transform.position);

                }
                else
                {
                    OpheroesOnGrid[u].Attack();
                }
            }
        }

        public void UnitDiedCallBack(long id)
        {

            for (int i = 0; i < unitsOnGrid.Count; i++)
            {
                if (unitsOnGrid[i].databaseID == id)
                {
                    unitsOnGrid[i].OnDeath();
                    Destroy(unitsOnGrid[i].healthBar.gameObject);
                    Destroy(unitsOnGrid[i].gameObject);
                    unitsOnGrid.RemoveAt(i);
                    break;
                }
            }
            for (int i = 0; i < buildingsOnGrid.Count; i++)
            {
                {
                    // buildingsOnGrid[i].building.setSprite();
                    buildingsOnGrid[i].building._animator.SetInteger("rotation", 1);
                }
            }
            for (int i = 0; i < OpheroesOnGrid.Count; i++)
            {
                {
                    // buildingsOnGrid[i].building.setSprite();
                    OpheroesOnGrid[i].HeroAnimator.Play("Hero_Idle_" + Direction.Down);
                }
            }

            Debug.Log("Dead");
        }

        public void HeroDiedCallBack(long id)
        {
            for (int i = 0; i < heroesOnGrid.Count; i++)
            {
                if (heroesOnGrid[i].databaseID == id)
                {
                    heroesOnGrid[i].onDeath();
                    Destroy(heroesOnGrid[i].healthBar.gameObject);
                    Destroy(heroesOnGrid[i].gameObject);
                    heroesOnGrid[i].data.battle_ready = false;

                    Packet paket = new Packet();
                    paket.Write((int)Player.RequestsID.HEROREGEN);
                    paket.Write(heroesOnGrid[i].databaseID);
                    Sender.TCP_Send(paket);

                    heroesOnGrid.RemoveAt(i);
                    break;
                }
            }
        }
        public void OpHeroDiedCallBack(long id)
        {
            for (int i = 0; i < OpheroesOnGrid.Count; i++)
            {
                if (OpheroesOnGrid[i].databaseID == id)
                {
                    OpheroesOnGrid[i].onDeath();
                    Destroy(OpheroesOnGrid[i].healthBar.gameObject);
                    Destroy(OpheroesOnGrid[i].gameObject);
                    OpheroesOnGrid.RemoveAt(i);
                    break;
                }
            }
        }

        public void UnitTargetSelectedCallBack(long id)
        {
            int targetIndex = -1;
            for (int i = 0; i < battle._units.Count; i++)
            {
                if (battle._units[i].unit.databaseID == id)
                {
                    targetIndex = battle._units[i].target;
                    break;
                }
            }

            if (targetIndex >= 0 && battle._buildings.Count > targetIndex)
            {
                long buildingID = battle._buildings[targetIndex].building.databaseID;
                for (int i = 0; i < buildingsOnGrid.Count; i++)
                {
                    if (buildingsOnGrid[i].building.databaseID == buildingID)
                    {
                        //Vector3 pos = buildingsOnGrid[i].transform.position; // This is the target
                        // You can instantiate target point here and delete it after a few seconds for example:
                        // Transform tp = Instantiate(prefab, ...)
                        // Destroy(tp.gameObject, 2f);
                        break;
                    }
                }
            }
        }
        public void HeroTargetSelectedCallBack(long id)
        {
            int targetIndex = -1;
            for (int i = 0; i < battle._heroes.Count; i++)
            {
                if (battle._heroes[i].hero.databaseID == id)
                {
                    targetIndex = battle._heroes[i].target;
                    break;
                }
            }

            if (targetIndex >= 0 && battle._buildings.Count > targetIndex)
            {
                long buildingID = battle._buildings[targetIndex].building.databaseID;
                for (int i = 0; i < buildingsOnGrid.Count; i++)
                {
                    if (buildingsOnGrid[i].building.databaseID == buildingID)
                    {
                        //Vector3 pos = buildingsOnGrid[i].transform.position; // This is the target
                        // You can instantiate target point here and delete it after a few seconds for example:
                        // Transform tp = Instantiate(prefab, ...)
                        // Destroy(tp.gameObject, 2f);
                        break;
                    }
                }
            }
        }
        public void OpHeroTargetSelectedCallBack(long id)
        {
            int targetIndex = -1;
            for (int i = 0; i < battle._opHeroes.Count; i++)
            {
                if (battle._opHeroes[i].opHero.databaseID == id)
                {
                    targetIndex = battle._opHeroes[i].target;
                    break;
                }
            }

            //if (targetIndex >= 0 && battle._units.Count > targetIndex)
            //{
            //    //long buildingID = battle._buildings[targetIndex].building.databaseID;
            //    //for (int i = 0; i < buildingsOnGrid.Count; i++)
            //    //{
            //    //    if (buildingsOnGrid[i].building.databaseID == buildingID)
            //    //    {
            //    //        //Vector3 pos = buildingsOnGrid[i].transform.position; // This is the target
            //    //        // You can instantiate target point here and delete it after a few seconds for example:
            //    //        // Transform tp = Instantiate(prefab, ...)
            //    //        // Destroy(tp.gameObject, 2f);
            //    //        break;
            //    //    }
            //    //}
            //}
        }
        public void UnitDamageCallBack(long id, float damage)
        {

        }

        public void HeroDamageCallBack(long id, float damage)
        {

        }
        public void OpHeroDamageCallBack(long id, float damage)
        {

        }

        public void UnitHealCallBack(long id, float health)
        {

        }

        public void HeroHealCallBack(long id, float health)
        {

        }
        public void OpHeroHealCallBack(long id, float health)
        {

        }

        public void BuildingAttackCallBack(long id, long target)
        {
            int u = -1;
            int b = -1;
            for (int i = 0; i < buildingsOnGrid.Count; i++)
            {
                if (buildingsOnGrid[i].id == id)
                {
                    if (buildingsOnGrid[i].building.data.radius > 0 && buildingsOnGrid[i].building.data.rangedSpeed > 0)
                    {
                        b = i;
                    }
                    break;
                }
            }
            if (b >= 0)
            {
                bool isUnitTarget = false;
                for (int i = 0; i < unitsOnGrid.Count; i++)
                {
                    if (unitsOnGrid[i].databaseID == target)
                    {
                        u = i;
                        isUnitTarget = true;
                        break;
                    }
                }

                if (!isUnitTarget)
                {
                    for (int i = 0; i < heroesOnGrid.Count; i++)
                    {
                        if (heroesOnGrid[i].databaseID == target)
                        {
                            u = i;
                            break;
                        }
                    }
                }

                if (u >= 0)
                {
                    if (isUnitTarget && unitsOnGrid[u])
                        buildingsOnGrid[b].building.LookAt(unitsOnGrid[u].transform.position);
                    else
                        buildingsOnGrid[b].building.LookAt(heroesOnGrid[u].transform.position);

                    UI_Projectile projectile = buildingsOnGrid[b].building.GetProjectile();
                    GameObject fireParticle = buildingsOnGrid[b].building.GetFireParticle();
                    Transform muzzle = buildingsOnGrid[b].building.GetMuzzle();
                    //GameObject fireParticle = buildingsOnGrid[b].building.GetFireParticlePrefab();
                    if (projectile != null && muzzle != null)
                    {
                        if (isUnitTarget)
                        {
                            projectile = Instantiate(projectile, muzzle.position, Quaternion.LookRotation(unitsOnGrid[u].transform.position - muzzle.position, Vector3.up));
                            projectile.Initialize(muzzle.position, unitsOnGrid[u].targetPoint != null ? unitsOnGrid[u].targetPoint : unitsOnGrid[u].transform, buildingsOnGrid[b].building.data.rangedSpeed * UI_Main.instanse._grid.cellSize, UI_Projectile.GetCutveHeight(buildingsOnGrid[b].building.id));
                            buildingsOnGrid[b].building.PlayFireSound();
                            if (fireParticle)
                            {
                                fireParticle = Instantiate(fireParticle, muzzle.position, Quaternion.LookRotation(unitsOnGrid[u].transform.position - muzzle.position, Vector3.up));
                                removables.Add(fireParticle);
                            }
                        }
                        else
                        {
                            projectile = Instantiate(projectile, muzzle.position, Quaternion.LookRotation(heroesOnGrid[u].transform.position - muzzle.position, Vector3.up));
                            projectile.Initialize(muzzle.position, heroesOnGrid[u].targetPoint != null ? heroesOnGrid[u].targetPoint : heroesOnGrid[u].transform, buildingsOnGrid[b].building.data.rangedSpeed * UI_Main.instanse._grid.cellSize, UI_Projectile.GetCutveHeight(buildingsOnGrid[b].building.id));
                            buildingsOnGrid[b].building.PlayFireSound();
                            if (fireParticle)
                            {
                                fireParticle = Instantiate(fireParticle, muzzle.position, Quaternion.LookRotation(heroesOnGrid[u].transform.position - muzzle.position, Vector3.up));
                                removables.Add(fireParticle);
                            }
                        }
                    }
                }
                else
                    Debug.Log("cleard");

            }
        }

        public void BuildingDamageCallBack(long id, float damage)
        {
            UpdateLoots();
            playBuildingDamageParticle(id, damage);
        }
        public void playBuildingDamageParticle(long id,float damage)
        {
            for (int i = 0; i < buildingsOnGrid.Count; i++)
            {
                if (buildingsOnGrid[i].id == id )
                {
                    buildingsOnGrid[i].building.PlayDamageParticle();
                    break;
                }
            }
        }

        public void BuildingDestroyedCallBack(long id, double percentage)
        {
            if (percentage > 0)
            {
                _percentageText.text = Mathf.RoundToInt((float)(battle.percentage * 100f)).ToString() + "%";
            }
            for (int i = 0; i < buildingsOnGrid.Count; i++)
            {
                if (buildingsOnGrid[i].id == id)
                {
                    GameObject particle = Instantiate(UI_Main.instanse._destroyParticles[buildingsOnGrid[i].building.columns - 1]);
                    particle.transform.position = buildingsOnGrid[i].building.transform.position;
                    removables.Add(particle);
                    GameObject destroyBuilding = Instantiate(UI_Main.instanse._destroyedBuildings[buildingsOnGrid[i].building.columns - 1]);
                    destroyBuilding.transform.position = buildingsOnGrid[i].building.transform.position;
                    removables.Add(destroyBuilding);
                    buildingsOnGrid[i].building.PlayDestroySound();
                    Destroy(buildingsOnGrid[i].building.gameObject);
                    buildingsOnGrid.RemoveAt(i);
                    break;
                }
            }
        }

        #endregion


        public void ShowLog(string msg)
        {
            Debug.Log(msg);
        }
    }
}