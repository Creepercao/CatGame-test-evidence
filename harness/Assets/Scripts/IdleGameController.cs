using System;
using CatGame.Idle;
using UnityEngine;

namespace CatGame
{
    /// <summary>Runtime economy loop and the only entry point for cat/item purchases.</summary>
    public sealed class IdleGameController : MonoBehaviour
    {
        public event Action StateChanged;
        public event Action<AchievementDefinition> AchievementUnlocked;

        public GameState State { get; private set; }
        public CatRoster Roster { get; private set; }
        public double ProductionPerSecond { get; private set; }
        public double CurrentClickValue { get { return State == null ? 0 : IdleMath.ClickValue(State); } }
        bool ModalBlocked { get { return CatWorld.I != null && ReferenceEquals(CatWorld.I.Economy,this) && CatWorld.I.Hud != null && CatWorld.I.Hud.Modals.InputBlocked; } }

        float _uiRefresh;
        bool _dirty;

        public void Initialize(GameState state, CatRoster roster)
        {
            State = state ?? GameStateFactory.CreateNewGame();
            Roster = roster;
            Recalculate();
            RaiseChanged();
        }

        void Update()
        {
            if (State == null) return;
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                State.playSeconds += Time.unscaledDeltaTime;
                _dirty = true;
                double rate = ProductionPerSecond;
                if (rate > 0)
                {
                    IdleMath.ApplyProduction(State, rate, dt);
                    _dirty = true;
                }
            }

            _uiRefresh += Time.unscaledDeltaTime;
            if (_uiRefresh >= 0.2f)
            {
                _uiRefresh = 0f;
                var unlocked = AchievementCatalog.EvaluateNew(State);
                // Persist the achievement and its pending memory together; no gap on scene exit.
                bool queuedStory = StoryCatalog.EnqueueUnlocked(State);
                if (unlocked.Count > 0)
                {
                    if (AchievementUnlocked != null)
                        foreach (var achievement in unlocked) AchievementUnlocked(achievement);
                    if (SaveSystem.Instance != null && SaveSystem.Instance.State == State)
                        SaveSystem.Instance.OnAchievementUnlocked();
                    else
                        _dirty = true;
                }
                if (queuedStory) MarkDirty();
                if (_dirty && SaveSystem.Instance != null && SaveSystem.Instance.State == State)
                {
                    SaveSystem.Instance.MarkDirty();
                    _dirty = false;
                }
                RaiseChanged();
            }
        }

        public bool BuyCat(string breedId)
        {
            if (ModalBlocked) return false;
            if (State == null) return false;
            CatCount row = FindCat(breedId);
            CatDefinition definition = FindCatDefinition(breedId);
            if (row == null || definition == null || row.count >= definition.maxOwned)
                return false;
            double price = IdleMath.CatPrice(breedId, row.count);
            if (price > State.catHair) return false;

            State.catHair -= price;
            row.count++;
            if (Roster != null) Roster.Rebuild(State, breedId);
            PersistPurchase();
            Recalculate();
            RaiseChanged();
            return true;
        }

        public bool BuyItem(string itemId)
        {
            if (ModalBlocked) return false;
            if (State == null) return false;
            ItemLevel row = FindItem(itemId);
            ItemDefinition definition = FindItemDefinition(itemId);
            if (row == null || definition == null || row.level >= ItemCatalog.LevelsPerItem)
                return false;
            double price = IdleMath.ItemPrice(itemId, row.level);
            if (price > State.catHair) return false;

            State.catHair -= price;
            row.level++;
            PersistPurchase();
            Recalculate();
            RaiseChanged();
            return true;
        }

        public bool BuyRocket()
        {
            if (ModalBlocked || !EndingRules.TryPurchase(State)) return false;
            PersistPurchase();
            RaiseChanged();
            return true;
        }

        public int OwnedCats { get { return Roster != null ? Roster.TotalOwned : 0; } }

        void Recalculate()
        {
            if (State == null) return;
            ProductionPerSecond = IdleMath.BasePerSecond(State) * IdleMath.ItemMultiplier(State);
        }

        void MarkDirty()
        {
            _dirty = true;
            if (SaveSystem.Instance != null && SaveSystem.Instance.State == State)
            {
                SaveSystem.Instance.MarkDirty();
                _dirty = false;
            }
        }

        void PersistPurchase()
        {
            MarkDirty();
            // Android may stop the process before Unity receives OnApplicationPause.
            // Commit purchases before reporting completion, as for the ending rocket.
            var save = SaveSystem.Instance;
            if (save != null && ReferenceEquals(save.State, State)) save.SaveNow();
        }

        void RaiseChanged() { if (StateChanged != null) StateChanged(); }

        CatCount FindCat(string id)
        {
            foreach (var row in State.cats) if (row.id == id) return row;
            return null;
        }

        ItemLevel FindItem(string id)
        {
            foreach (var row in State.items) if (row.id == id) return row;
            return null;
        }

        static CatDefinition FindCatDefinition(string id)
        {
            foreach (var definition in CatCatalog.All) if (definition.id == id) return definition;
            return null;
        }

        static ItemDefinition FindItemDefinition(string id)
        {
            foreach (var definition in ItemCatalog.All) if (definition.id == id) return definition;
            return null;
        }
    }
}
