using System;
using System.IO;
using System.Reflection;
using BiopunkRV;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        checks++;
    }
    private static void Reload() => typeof(MetaProgression).GetField("data", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
    public static void Main()
    {
        Check(MetaProgression.Data.stones == 0, "new save starts without currency");
        Check(!MetaProgression.Upgrade(0), "insufficient funds rejected");
        RunInventory.spiritStones = 1000; RunInventory.methodFragments = 50; RunInventory.affixFragments = 50;
        MetaProgression.SettleRun(); MetaProgression.SettleRun();
        Check(MetaProgression.Data.stones == 1000 && MetaProgression.Data.methodFragments == 50, "settlement is exactly once");
        Check(MetaProgression.Upgrade(0) && MetaProgression.Data.stones == 995, "enhancement debits exact cost");
        Check(!MetaProgression.Spend(-1), "negative spending rejected");
        Check(!MetaProgression.Upgrade(-1) && !MetaProgression.Upgrade(6), "invalid upgrade indexes rejected");
        Check(!MetaProgression.ImproveTalent(0), "talents require enlightenment facility");
        Check(MetaProgression.ImproveFacility(0) && MetaProgression.ImproveTalent(0), "facility unlocks talent");
        Check(MetaProgression.UnlockMethod("orb", 0, false), "method purchase");
        int balance = MetaProgression.Data.stones;
        Check(!MetaProgression.UnlockMethod("orb", 0, false) && MetaProgression.Data.stones == balance, "duplicate purchase cannot charge");
        Check(!MetaProgression.UnlockMethod("unknown", 0, true), "unknown method rejected");
        Check(MetaProgression.UnlockMethod("orb2", 1, true) && MetaProgression.UnlockMethod("sword", 2, false), "fragment and forbidden unlocks");
        Check(MetaProgression.EquipMethod("orb", 0) && !MetaProgression.EquipMethod("orb2", 1) && MetaProgression.EquipMethod("sword", 1), "loadout prevents duplicate base skills");
        Check(ArtifactSystem.TryUpgradeTier() && ArtifactSystem.TryRefine(), "artifact uses permanent wallet");
        Check(ArtifactSystem.GetUpgradeCost() > 0, "artifact ascension has a cost");
        RealmSystem.AddCultivationPoints(310);
        Reload(); MetaProgression.EnsureLoaded();
        Check(RealmSystem.CurrentRealm == RealmSystem.Realm.金丹 && ArtifactSystem.RefineLevel == 1, "realm and artifact survive reload");
        Check(MetaProgression.Data.mainMethod == "orb" && MetaProgression.Data.supportMethod == "sword", "loadout survives reload");
        var stats = new PlayerStats(); MetaProgression.ApplyStats(stats);
        Check(stats.maxHP == 110 && stats.currentHP == 110 && stats.attack == 42, "saved upgrades apply to fresh character");
        var bonus = new MethodBonusState(); bonus.Apply(stats, .5f, 2); bonus.Apply(stats, .5f, 2);
        Check(stats.attack == 63 && stats.attackSpeed == 7, "reapplying method cannot stack bonuses");
        stats.attack += 10; bonus.Apply(stats, 0, 0);
        Check(stats.attack == 52 && stats.attackSpeed == 5, "switching method preserves unrelated upgrades");
        stats.attackSpeed = 10000;
        Check(stats.GetScaledInterval(1) >= .05f, "cooldown always positive");
        MetaProgression.Data.upgrades[0] = MetaProgression.UpgradeLimits[0];
        balance = MetaProgression.Data.stones;
        Check(!MetaProgression.Upgrade(0) && MetaProgression.Data.stones == balance, "capped enhancement cannot charge");
        MetaProgression.Save();
        File.WriteAllText(MetaProgression.SavePath, "broken json"); Reload();
        Check(MetaProgression.Data.mainMethod == "orb", "corrupt save recovers backup");
        Console.WriteLine("PASS: " + checks + " production progression checks (Unity host and JSON adapters; no visual/runtime coverage).");
    }
}
