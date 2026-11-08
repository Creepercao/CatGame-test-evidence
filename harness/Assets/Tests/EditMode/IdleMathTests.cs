using NUnit.Framework;
using CatGame.Idle;
using System.Diagnostics;

namespace CatGame.EditTests
{
    public class IdleMathTests
    {
        static GameState Empty()
        {
            var s = new GameState();
            foreach (var c in CatCatalog.All) s.cats.Add(new CatCount { id=c.id, count=0 });
            foreach (var i in ItemCatalog.All) s.items.Add(new ItemLevel { id=i.id, level=0 });
            return s;
        }

        [Test] public void Catalog_has_eleven_cats_and_twenty_items()
        { Assert.AreEqual(11, CatCatalog.All.Length); Assert.AreEqual(20, ItemCatalog.All.Length); }

        [Test] public void New_game_starts_with_one_Milk_cat()
        {
            var state = GameStateFactory.CreateNewGame();
            Assert.AreEqual(GameState.CurrentVersion, state.version);
            Assert.AreEqual(CatCatalog.All.Length, state.cats.Count);
            Assert.AreEqual(1, state.cats[0].count);
            for (int i = 1; i < state.cats.Count; i++) Assert.AreEqual(0, state.cats[i].count);
            Assert.AreEqual(ItemCatalog.All.Length, state.items.Count);
        }

        [Test] public void Achievement_catalog_has_twenty_unique_entries()
        {
            Assert.AreEqual(20, AchievementCatalog.All.Length);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var achievement in AchievementCatalog.All)
                Assert.IsTrue(ids.Add(achievement.id), "Duplicate achievement id: " + achievement.id);
        }

        [Test] public void Achievement_rules_unlock_once_and_record_id()
        {
            var state = GameStateFactory.CreateNewGame();
            state.totalEarned = 10;
            var first = AchievementCatalog.EvaluateNew(state);
            Assert.IsTrue(first.Exists(achievement => achievement.id == "first_tuft"));
            Assert.Contains("first_tuft", state.achievements);
            Assert.IsFalse(AchievementCatalog.EvaluateNew(state).Exists(achievement => achievement.id == "first_tuft"));

            state.clicks = 1;
            Assert.IsTrue(AchievementCatalog.EvaluateNew(state).Exists(achievement => achievement.id == "first_click"));
            state.totalEarned = IdleBalance.Stage4Threshold;
            var wealthOnly = AchievementCatalog.EvaluateNew(state);
            Assert.IsFalse(wealthOnly.Exists(a => a.id == "stage_two" || a.id == "stage_three" || a.id == "stage_four"),
                "Old phase thresholds must not award the new cat collection achievements");
            foreach (var id in new[] { "Tuxedo", "Egypt", "Xmas" }) state.cats.Find(c => c.id == id).count = 1;
            var collected = AchievementCatalog.EvaluateNew(state);
            foreach (var id in new[] { "stage_two", "stage_three", "stage_four" })
                Assert.IsTrue(collected.Exists(a => a.id == id));
            foreach (var entry in AchievementCatalog.All)
            {
                StringAssert.DoesNotContain("阶段", entry.condition);
                StringAssert.DoesNotContain("1e4", entry.condition);
                StringAssert.DoesNotContain("1e5", entry.condition);
                StringAssert.DoesNotContain("1e6", entry.condition);
            }
        }

        [Test] public void Offline_earnings_are_capped_and_clock_never_moves_save_backwards()
        {
            var state = GameStateFactory.CreateNewGame();
            state.lastSeenUnix = 100;
            long future = 100 + (long)IdleBalance.OfflineCapSeconds + 60;
            var result = OfflineEarnings.Apply(state, future);
            Assert.AreEqual(IdleBalance.OfflineCapSeconds, result.elapsedSeconds);
            Assert.AreEqual(IdleMath.OfflineEarnings(state, IdleBalance.OfflineCapSeconds), result.catHair, 0.001);
            Assert.AreEqual(result.catHair, state.catHair, 0.001);
            Assert.AreEqual(result.catHair, state.totalEarned, 0.001);

            var clockRollback = OfflineEarnings.Apply(state, 50);
            Assert.AreEqual(0, clockRollback.catHair);
            Assert.AreEqual(future, state.lastSeenUnix);

            state.catHair = double.MaxValue;
            state.totalEarned = double.MaxValue;
            var saturated = OfflineEarnings.Apply(state, future + 100);
            Assert.AreEqual(100, saturated.elapsedSeconds);
            Assert.AreEqual(0, saturated.catHair, "Offline result must report only the amount actually applied to the finite balances.");
            Assert.AreEqual(double.MaxValue, state.catHair);
            Assert.AreEqual(double.MaxValue, state.totalEarned);
            Assert.AreEqual(future + 100, state.lastSeenUnix);
        }

        [Test] public void Game_state_has_versioned_save_fields_and_defaults()
        {
            var state = new GameState();
            Assert.AreEqual(GameState.CurrentVersion, state.version);
            Assert.IsFalse(state.introCompleted);
            Assert.AreEqual(0, state.catHair);
            Assert.AreEqual(0, state.totalEarned);
            Assert.AreEqual(0, state.clicks);
            Assert.IsNotNull(state.cats);
            Assert.IsNotNull(state.items);
            Assert.IsNotNull(state.achievements);
            Assert.AreEqual(0L, state.lastSeenUnix);
            Assert.AreEqual(0, state.prestigeLevel);
            Assert.AreEqual(1.0, state.prestigeMultiplier);
        }

        [Test] public void Cat_prices_form_natural_progression_and_keep_growth()
        {
            foreach (var cat in CatCatalog.All)
            {
                var payback = cat.firstPrice / cat.income;
                Assert.AreEqual(IdleBalance.CatPriceGrowth, cat.priceGrowth, 1e-8, cat.id + " price growth");
                Assert.Greater(payback, 0);
                Assert.AreEqual(0, cat.unlock);
                Assert.Greater(cat.firstPrice, 2 * IdleBalance.ClickBaseValue);
            }
        }

        [Test] public void Cat_price_caps_at_forty()
        { Assert.AreEqual(CatCatalog.All[0].firstPrice * System.Math.Pow(IdleBalance.CatPriceGrowth, IdleBalance.CatMaxOwned-1), IdleMath.CatPrice("Milk", IdleBalance.CatMaxOwned-1), 1e-8); Assert.IsTrue(double.IsPositiveInfinity(IdleMath.CatPrice("Milk", IdleBalance.CatMaxOwned))); }

        [Test] public void Item_prices_and_effects_follow_generation_and_level_curves()
        {
            var first=ItemCatalog.All[0];
            Assert.AreEqual(first.firstPrice, IdleMath.ItemPrice(first.id,0));
            Assert.AreEqual(first.firstPrice*System.Math.Pow(ItemCatalog.PriceGrowthPerLevel,ItemCatalog.LevelsPerItem-1), IdleMath.ItemPrice(first.id,ItemCatalog.LevelsPerItem-1), 1e-8);
            for (var generation=1; generation<=ItemCatalog.GenerationCount; generation++)
            {
                var item=ItemCatalog.All[(generation-1)*ItemCatalog.ItemsPerGeneration];
                var clickItem=ItemCatalog.All[(generation-1)*ItemCatalog.ItemsPerGeneration+3];
                Assert.AreEqual(first.effect*System.Math.Pow(ItemCatalog.EffectGrowthPerGeneration,generation-1), item.effect, 1e-8);
                Assert.AreEqual(first.firstPrice*System.Math.Pow(ItemCatalog.PriceGrowthPerGeneration,generation-1)*IdleBalance.ItemGenerationPriceScales[generation-1], item.firstPrice, 1e-8);
                Assert.AreEqual(ItemCatalog.All[3].effect, clickItem.effect, 1e-8, "Generation " + generation + " click effect");
            }
            Assert.IsTrue(double.IsPositiveInfinity(IdleMath.ItemPrice(first.id,ItemCatalog.LevelsPerItem)));
        }

        [Test] public void Production_items_add_with_effects_matching_each_level()
        {
            var s=Empty(); s.items[0].level=1; s.items[1].level=2; s.items[2].level=ItemCatalog.LevelsPerItem;
            Assert.AreEqual(1 + ItemCatalog.All[0].effect + ItemCatalog.All[1].effect * 2 + ItemCatalog.All[2].effect * 16, IdleMath.ItemMultiplier(s), 1e-8);
            for (var level=1; level<=ItemCatalog.LevelsPerItem; level++)
            {
                s=Empty(); s.items[0].level=level;
                Assert.AreEqual(1 + ItemCatalog.All[0].effect * System.Math.Pow(2, level-1), IdleMath.ItemMultiplier(s), 1e-8, "Level " + level);
            }
        }

        [Test] public void Production_remains_finite_after_one_hundred_thousand_ticks()
        {
            var state = GameStateFactory.CreateNewGame();
            double rate = IdleMath.BasePerSecond(state) * IdleMath.ItemMultiplier(state);
            const double delta = 1.0 / 60.0;
            const int ticks = 100000;
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < ticks; i++)
            {
                double earned = IdleMath.ApplyProduction(state, rate, delta);
                Assert.IsFalse(double.IsNaN(earned) || double.IsInfinity(earned));
                if (i % 10000 == 9999)
                {
                    Assert.IsFalse(double.IsNaN(state.catHair) || double.IsInfinity(state.catHair));
                    Assert.IsFalse(double.IsNaN(state.totalEarned) || double.IsInfinity(state.totalEarned));
                    Assert.GreaterOrEqual(state.catHair, 0);
                }
            }

            stopwatch.Stop();
            double expected = rate * delta * ticks;
            Assert.AreEqual(expected, state.catHair, expected * 1e-9 + 0.001);
            Assert.AreEqual(state.catHair, state.totalEarned, 0.001);
            double simulationFinalBalance = state.catHair;
            // Purchases reduce spendable currency without reducing lifetime earnings.
            var afterPurchase = GameStateFactory.CreateNewGame();
            afterPurchase.catHair = 1;
            afterPurchase.totalEarned = 10;
            for (int i = 0; i < 600; i++)
                Assert.Greater(IdleMath.ApplyProduction(afterPurchase, rate, delta), 0,
                    "Production must continue after spending currency, tick " + i);
            Assert.AreEqual(1 + rate * 10, afterPurchase.catHair, 1e-8);
            Assert.AreEqual(10 + rate * 10, afterPurchase.totalEarned, 1e-8);
            double balanceBeforeInvalidInputs = state.catHair;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => IdleMath.ApplyProduction(state, rate, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => IdleMath.ApplyProduction(state, double.NaN, delta));
            Assert.AreEqual(balanceBeforeInvalidInputs, state.catHair, 0.001, "invalid ticks must not partially mutate the balance");
            state.catHair = double.MaxValue / 2.0;
            state.totalEarned = double.MaxValue / 2.0;
            Assert.AreEqual(double.MaxValue / 2.0, IdleMath.ApplyProduction(state, double.MaxValue, 2));
            Assert.AreEqual(double.MaxValue, state.catHair);
            Assert.AreEqual(double.MaxValue, state.totalEarned);
            Assert.IsFalse(double.IsInfinity(state.catHair) || double.IsNaN(state.catHair));
            bool saturatedOverflowFinite = state.catHair == double.MaxValue && state.totalEarned == double.MaxValue
                && !double.IsInfinity(state.catHair) && !double.IsNaN(state.totalEarned);

            state.catHair = System.Math.Pow(2, 53);
            state.totalEarned = System.Math.Pow(2, 54);
            double representableBoundaryIncome = IdleMath.ApplyProduction(state, 2, 1);
            Assert.AreEqual(0, representableBoundaryIncome, "Do not report income when the total-earned balance cannot represent the same increment.");
            Assert.AreEqual(System.Math.Pow(2, 53), state.catHair);
            Assert.AreEqual(System.Math.Pow(2, 54), state.totalEarned);
            System.Console.WriteLine(string.Format(
                "STR-03 ticks={0}, simulatedSeconds={1:0.###}, finalBalance={2:0.###}, saturatedOverflowFinite={3}, elapsedMs={4}",
                ticks, delta * ticks, simulationFinalBalance, saturatedOverflowFinite,
                stopwatch.ElapsedMilliseconds));
        }

        [Test] public void Price_progression_reaches_goal_without_clicks_or_stage_gates()
        {
            var result=CurveSimulator.Run(IdleBalance.CurveClickRate);
            Assert.IsTrue(result.reachedGoal, "Simulation must reach the win threshold before its safety limit.");
            Assert.That(result.iterations, Is.InRange(1, 1999999));
            Assert.Greater(result.secondCatSeconds, 2.0);
            Assert.Greater(result.firstFourBreedsSeconds, result.secondCatSeconds);
            Assert.IsFalse(double.IsNaN(result.totalSeconds) || double.IsInfinity(result.totalSeconds));
            Assert.IsTrue(CurveSimulator.Run(0).reachedGoal, "Passive-only progression must remain possible");
            Assert.Greater(result.purchasedLevels, 0);
            Assert.GreaterOrEqual(result.completion, 0.90, "Retain the non-conflicting completion-at-win requirement");
            Assert.IsTrue(double.IsPositiveInfinity(result.completionSeconds) || result.totalSeconds - result.completionSeconds <= 3,
                "If every item level is purchased first, the win must follow within three seconds");
            Assert.Greater(result.nextCatBatchSeconds, result.firstFourBreedsSeconds);
        }

        [Test] public void Click_value_ignores_cumulative_thresholds_and_offline_is_capped()
        {
            var s=Empty(); Assert.AreEqual(0.5, IdleMath.ClickValue(s));
            var clickState = GameStateFactory.CreateNewGame();
            Assert.AreEqual(0.5, IdleMath.ApplyClick(clickState));
            Assert.AreEqual(0.5, clickState.catHair);
            Assert.AreEqual(0.5, clickState.totalEarned);
            Assert.AreEqual(1, clickState.clicks);
            clickState.clicks = int.MaxValue;
            Assert.AreEqual(0.5, IdleMath.ApplyClick(clickState));
            Assert.AreEqual(int.MaxValue, clickState.clicks, "Click counter must not wrap into an invalid negative save value.");
            var boundaries = new[] { (1e5, 2), (1e7, 3), (1e9, 4), (1e11, 5) };
            foreach (var boundary in boundaries)
            {
                s.totalEarned=boundary.Item1-1; Assert.AreEqual(boundary.Item2-1, IdleMath.Stage(s));
                s.totalEarned=boundary.Item1; Assert.AreEqual(boundary.Item2, IdleMath.Stage(s));
                Assert.AreEqual(0.5, IdleMath.ClickValue(s));
            }
            s.items[3].level=2; Assert.AreEqual(1, IdleMath.ClickValue(s));
            s.cats[0].count=1;
            Assert.AreEqual(CatCatalog.All[0].income*28800*.5, IdleMath.OfflineEarnings(s, 50000), 1e-8);
            Assert.AreEqual(0, IdleMath.OfflineEarnings(s, -10));
        }

        [Test] public void Curve_simulator_rejects_invalid_click_rates()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CurveSimulator.Run(-0.1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CurveSimulator.Run(double.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CurveSimulator.Run(double.PositiveInfinity));
        }
    }
}
