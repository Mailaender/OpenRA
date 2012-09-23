#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Mods.RA.Buildings;
using OpenRA.Mods.RA.Move;
using OpenRA.Traits;
using XRandom = OpenRA.Thirdparty.Random;
using System.Collections;
using OpenRA.Mods.RA.Air;

/*

 * BetaAI
 * Contributors: JamesDunne, Earthpig, Mart0258, Mailaender, Chrisforbes, Valkirie
 * 
 * TODO:
 *  - Build units according to enemies behaviors.
 *  - Build and use superpowers.

 */

namespace OpenRA.Mods.RA.AI
{
    class BetaAIInfo : IBotInfo, ITraitInfo
    {
        public readonly string Name = "Unnamed Bot";
        public readonly int AssignRolesInterval = 40;
        public readonly int AssignRolesInterval2 = 400;
        public readonly string RallypointTestBuilding = "fact";         // temporary hack to maintain previous rallypoint behavior.

        public readonly string[] UnitQueues = { "Vehicle", "Infantry", "Plane", "Ship" };
        public readonly Dictionary<string, string> generality = new Dictionary<string, string>()
        {
            {"weap", "Vehicle"},
            {"tent", "Infantry"},
            {"barr", "Infantry"},
            {"spen", "Ship"},
            {"syrd", "Ship"},
            {"dome", "Plane"},
            {"hpad", "Plane"},
            {"afld", "Plane"}
        };

        public readonly bool ShouldRepairBuildings = true;

        string IBotInfo.Name { get { return this.Name; } }

        [FieldLoader.LoadUsing("LoadUnits")]
        public readonly Dictionary<string, float> UnitsToBuild = null;

        [FieldLoader.LoadUsing("LoadBuildings")]
        public readonly Dictionary<string, float> BuildingFractions = null;

        [FieldLoader.LoadUsing("LoadSquadSize")]
        public readonly Dictionary<string, int> SquadSize = null;

        [FieldLoader.LoadUsing("LoadBuildingLimits")]
        public readonly Dictionary<string, int> BuildingLimits = null;

        [FieldLoader.LoadUsing("LoadTweaks")]
        public readonly Dictionary<string, float> Tweaks = null;

        [FieldLoader.LoadUsing("LoadAffinities")]
        public readonly Dictionary<string, string[]> Affinities = null;

        [FieldLoader.LoadUsing("LoadTicketsLimits")]
        public readonly Dictionary<string, int> TicketsLimits = null;

        static object LoadActorList(MiniYaml y, string field)
        {
            return y.NodesDict[field].Nodes.ToDictionary(
                t => t.Key,
                t => FieldLoader.GetValue<float>(field, t.Value.Value));
        }

        static object LoadOtherList(MiniYaml y, string field)
        {
            return y.NodesDict[field].Nodes.ToDictionary(
                t => t.Key,
                t => FieldLoader.GetValue<int>(field, t.Value.Value));
        }

        static object LoadListList(MiniYaml y, string field)
        {
            return y.NodesDict[field].Nodes.ToDictionary(
                t => t.Key,
                t => FieldLoader.GetValue<string[]>(field, t.Value.Value));
        }

        static object LoadAffinities(MiniYaml y) { return LoadListList(y, "Affinities"); }
        static object LoadBuildingLimits(MiniYaml y) { return LoadOtherList(y, "BuildingLimits"); }
        static object LoadTweaks(MiniYaml y) { return LoadActorList(y, "Tweaks"); }
        static object LoadSquadSize(MiniYaml y) { return LoadOtherList(y, "SquadSize"); }
        static object LoadUnits(MiniYaml y) { return LoadActorList(y, "UnitsToBuild"); }
        static object LoadBuildings(MiniYaml y) { return LoadActorList(y, "BuildingFractions"); }
        static object LoadTicketsLimits(MiniYaml y) { return LoadOtherList(y, "TicketsLimits"); }

        public object Create(ActorInitializer init) { return new BetaAI(this); }
    }

    class Enemy { public int Aggro; }

    class Squad
    {

        readonly List<string> infantry = new List<string> { "dog", "e1", "e2", "e3", "e4", "e6", "spy", "e7", "medi", "c1", "c2", "shok" };
        readonly List<string> vehicles = new List<string> { "v2rl", "1tnk", "2tnk", "3tnk", "4tnk", "arty", "harv", "mcv", "jeep", "truk", "ttnk", "ftrk" };
        readonly List<string> artillery = new List<string> { "v2rl", "arty" };
        readonly List<string> aircraft = new List<string> { "yak", "mig", "heli", "hind" };

        public List<Actor> units = new List<Actor>();
        public string type;
        public World world;
        public BetaAIInfo info;

        float range;
        List<Actor> enemys = new List<Actor>();
        List<Actor> enemynearby = new List<Actor>();
        List<Actor> enemynearby_vehicle = new List<Actor>();
        List<Actor> enemynearby_infantry = new List<Actor>();
        List<Actor> enemynearby_aircraft = new List<Actor>();
        List<Actor> enemynearby_artillery = new List<Actor>();
        Actor target;

        public Squad(World w1, string s1, BetaAIInfo i1)
        {
            type = s1;
            world = w1;
            info = i1;
        }

        public bool isFull()
        {
            if (units.Count == info.SquadSize[type])
                return true;
            else
                return false;
        }

        public void React()
        {
            foreach (var unit in units.Where(a => !a.Destroyed))
            {
                if (unit.Info.Name == "e6")
                {
                    var capture = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * 6).Where(a1 => !a1.Destroyed && !a1.IsDead() && a1.HasTrait<ITargetable>() && a1.HasTrait<Capturable>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy);

                    if (!capture.Any())
                        continue;

                    var t_capture = capture.ClosestTo(unit.CenterLocation);

                    if (t_capture == null)
                        continue;

                    world.IssueOrder(new Order("CaptureActor", unit, false) { TargetActor = t_capture });
                }

                else if (unit.Info.Name == "spy")
                {
                    // DISGUISE
                    var disguise = world.Actors.Where(a1 => unit.Info.Name.Equals("e6") || a1.Info.Name.Equals("e1") || a1.Info.Name.Equals("e3")).ToList();

                    if (!disguise.Any())
                        continue;

                    var t_disguise = disguise.ClosestTo(unit.CenterLocation);
                    world.IssueOrder(new Order("Disguise", unit, false) { TargetActor = t_disguise });

                    // INFILTRATE
                    var hijack = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * 6).Where(a1 => !a1.Destroyed && !a1.IsDead() && a1.HasTrait<ITargetable>() && a1.HasTrait<IAcceptSpy>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy);

                    if (!hijack.Any())
                        continue;

                    var t_hijack = hijack.ClosestTo(unit.CenterLocation);

                    if (t_hijack == null)
                        continue;

                    world.IssueOrder(new Order("SpyInfiltrate", unit, false) { TargetActor = t_hijack });
                }

                else if (unit.Info.Name == "e1" || unit.Info.Name == "e2" || unit.Info.Name == "e3")
                {
                    range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();

                    if (range == 0)
                        continue;

                    enemys = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * (int)range).Where(a1 => !a1.Destroyed && !a1.IsDead()).ToList();
                    enemynearby = enemys.Where(a1 => a1.HasTrait<ITargetable>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy).ToList();

                    if (!enemynearby.Any())
                        continue;

                    // handle afinities
                    enemynearby_vehicle = enemynearby.Where(a => vehicles.Contains(a.Info.Name)).ToList();
                    enemynearby_infantry = enemynearby.Where(a => infantry.Contains(a.Info.Name)).ToList();

                    if (unit.Info.Name == "e1" || unit.Info.Name == "e2")
                    {
                        if (enemynearby_infantry.Any())
                            target = enemynearby_infantry.ClosestTo(unit.CenterLocation);
                        else if (enemynearby_vehicle.Any())
                            target = enemynearby_vehicle.ClosestTo(unit.CenterLocation);
                    }
                    else if (unit.Info.Name == "e3")
                    {
                        if (enemynearby_vehicle.Any())
                            target = enemynearby_vehicle.ClosestTo(unit.CenterLocation);
                        else if (enemynearby_infantry.Any())
                            target = enemynearby_infantry.ClosestTo(unit.CenterLocation);
                    }

                    if (target == null)
                        continue;

                    world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target });
                }

                else if (unit.Info.Name == "1tnk" || unit.Info.Name == "2tnk" || unit.Info.Name == "3tnk" || unit.Info.Name == "4tnk" || unit.Info.Name == "ttnk")
                {
                    range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();

                    if (range == 0)
                        continue;

                    enemys = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * (int)range).Where(a1 => !a1.Destroyed && !a1.IsDead()).ToList();
                    enemynearby = enemys.Where(a1 => a1.HasTrait<ITargetable>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy).ToList();

                    if (!enemynearby.Any())
                        continue;

                    // handle afinities
                    enemynearby_vehicle = enemynearby.Where(a => vehicles.Contains(a.Info.Name)).ToList();
                    enemynearby_infantry = enemynearby.Where(a => infantry.Contains(a.Info.Name)).ToList();

                    if (enemynearby_vehicle.Any())
                        target = enemynearby_vehicle.ClosestTo(unit.CenterLocation);
                    else if (enemynearby_infantry.Any())
                        target = enemynearby_infantry.ClosestTo(unit.CenterLocation);

                    if (target == null)
                        continue;

                    if (target.HasTrait<CrushableInfantry>())
                        world.IssueOrder(new Order("Move", unit, false) { TargetLocation = target.CenterLocation.ToCPos() });
                    else
                        world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target });
                }

                else if (unit.Info.Name == "ftrk")
                {
                    range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();

                    if (range == 0)
                        continue;

                    enemys = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * (int)range).Where(a1 => !a1.Destroyed && !a1.IsDead()).ToList();
                    enemynearby = enemys.Where(a1 => a1.HasTrait<ITargetable>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy).ToList();

                    if (!enemynearby.Any())
                        continue;

                    // handle afinities
                    enemynearby_vehicle = enemynearby.Where(a => vehicles.Contains(a.Info.Name)).ToList();
                    enemynearby_infantry = enemynearby.Where(a => infantry.Contains(a.Info.Name)).ToList();
                    enemynearby_aircraft = enemynearby.Where(a => aircraft.Contains(a.Info.Name)).ToList();

                    if (enemynearby_aircraft.Any())
                        target = enemynearby_aircraft.ClosestTo(unit.CenterLocation);
                    else if (enemynearby_vehicle.Any())
                        target = enemynearby_vehicle.ClosestTo(unit.CenterLocation);
                    else if (enemynearby_infantry.Any())
                        target = enemynearby_infantry.ClosestTo(unit.CenterLocation);

                    if (target == null)
                        continue;

                    if (target.HasTrait<CrushableInfantry>())
                        world.IssueOrder(new Order("Move", unit, false) { TargetLocation = target.CenterLocation.ToCPos() });
                    else
                        world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target });
                }

                else if (unit.Info.Name == "jeep" || unit.Info.Name == "apc")
                {
                    range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();

                    if (range == 0)
                        continue;

                    enemys = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * (int)range).Where(a1 => !a1.Destroyed && !a1.IsDead()).ToList();
                    enemynearby = enemys.Where(a1 => a1.HasTrait<ITargetable>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy).ToList();

                    if (!enemynearby.Any())
                        continue;

                    // handle afinities
                    enemynearby_vehicle = enemynearby.Where(a => vehicles.Contains(a.Info.Name)).ToList();
                    enemynearby_infantry = enemynearby.Where(a => infantry.Contains(a.Info.Name)).ToList();

                    if (enemynearby_infantry.Any())
                        target = enemynearby_infantry.ClosestTo(unit.CenterLocation);
                    else if (enemynearby_vehicle.Any())
                        target = enemynearby_vehicle.ClosestTo(unit.CenterLocation);

                    if (target == null)
                        continue;

                    world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target });
                }

                else if (unit.Info.Name == "hind" || unit.Info.Name == "yak" || unit.Info.Name == "mig" || unit.Info.Name == "heli")
                {
                    if (unit.TraitOrDefault<LimitedAmmo>().HasAmmo())
                    {
                        range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();
                        if (range == 0)
                            continue;

                        enemynearby = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * (int)range).Where(a1 => !a1.Destroyed && !a1.IsDead() && a1.HasTrait<ITargetable>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy).ToList();
                        if (!enemynearby.Any())
                            continue;

                        // handle afinities
                        enemynearby_vehicle = enemynearby.Where(a => vehicles.Contains(a.Info.Name)).ToList();
                        enemynearby_infantry = enemynearby.Where(a => infantry.Contains(a.Info.Name)).ToList();
                        enemynearby_artillery = enemynearby.Where(a => vehicles.Contains(a.Info.Name) && artillery.Contains(a.Info.Name)).ToList();

                        if (unit.Info.Name == "hind" || unit.Info.Name == "yak")
                        {
                            if (enemynearby_artillery.Any())
                                target = enemynearby_artillery.ClosestTo(unit.CenterLocation);
                            else if (enemynearby_infantry.Any())
                                target = enemynearby_infantry.ClosestTo(unit.CenterLocation);
                            else if (enemynearby_vehicle.Any())
                                target = enemynearby_vehicle.ClosestTo(unit.CenterLocation);
                        }

                        if (unit.Info.Name == "heli" || unit.Info.Name == "mig")
                        {
                            if (enemynearby_artillery.Any())
                                target = enemynearby_artillery.ClosestTo(unit.CenterLocation);
                            else if (enemynearby_vehicle.Any())
                                target = enemynearby_vehicle.ClosestTo(unit.CenterLocation);
                            else if (enemynearby_infantry.Any())
                                target = enemynearby_infantry.ClosestTo(unit.CenterLocation);
                        }

                        if (target == null)
                            continue;

                        world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target });
                    }
                    else
                        if (!unit.GetCurrentActivity().ToString().Contains("Land") && unit.HasTrait<Helicopter>())
                            world.IssueOrder(new Order("ReturnToBase", unit, false));
                        else if (unit.HasTrait<Plane>())
                            world.IssueOrder(new Order("ReturnToBase", unit, false));
                }

                else if (unit.Info.Name == "harv")
                {
                    var harv = unit.TraitOrDefault<Harvester>();
                    if (!unit.IsIdle)
                    {
                        Activity act = unit.GetCurrentActivity();
                        if ((act.GetType() != typeof(OpenRA.Mods.RA.Activities.Wait)) &&
                            (act.NextActivity == null || act.NextActivity.GetType() != typeof(OpenRA.Mods.RA.Activities.FindResources)))
                            continue;
                    }
                    if (!harv.IsEmpty) continue;

                    world.IssueOrder(new Order("Harvest", unit, false));
                }

                else
                {
                    range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();

                    if (range == 0)
                        continue;

                    enemys = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * (int)range).Where(a1 => !a1.Destroyed && !a1.IsDead()).ToList();
                    enemynearby = enemys.Where(a1 => a1.HasTrait<ITargetable>() && unit.Owner.Stances[a1.Owner] == Stance.Enemy).ToList();

                    if (!enemynearby.Any())
                        continue;

                    target = enemynearby.ClosestTo(unit.CenterLocation);

                    if (target == null)
                        continue;

                    world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target });
                }
            }
        }

        public void MoveShip(CPos targetloc, Actor enemy)
        {
            if (targetloc == null)
                return;

            CPos nearestloc = targetloc;

            foreach (Actor unit in units)
            {
                if (!world.GetTerrainType(nearestloc).Equals("Water")) /* If land unit */
                {
                    float range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();
                    var tiles = world.FindTilesInCircle(nearestloc, (int)range);

                    var tiless = tiles.OrderBy(a => (new PPos(a.X, a.Y) - enemy.CenterLocation).LengthSquared);

                    foreach (var t in tiless)
                        if (world.GetTerrainType(t).Contains("Water"))
                        {
                            nearestloc = t;
                            break;
                        }
                }

                if (nearestloc == null)
                    break;

                world.IssueOrder(new Order("Move", unit, false) { TargetLocation = nearestloc });
                world.IssueOrder(new Order("Attack", unit, true) { TargetActor = enemy });
            }
        }

        public void MoveAir(CPos target, Actor enemy, bool queue, int dist)
        {
            if (target == null)
                return;
            if (enemy == null)
                return;
            if (!enemy.HasTrait<ITargetable>()) /* ummm useless ? It's like a double check... */
                return;
            Actor leader = units.ClosestTo(target.ToPPos());
            if (leader == null)
                return;
            var ownUnits = world.FindUnitsInCircle(leader.CenterLocation, Game.CellSize * dist).Where(a => a.Owner == units.FirstOrDefault().Owner && this.units.Contains(a)).ToList();
            if (ownUnits.Count < units.Count)
            {
                world.IssueOrder(new Order("Stop", leader, false));
                foreach (var unit in units.Where(a => a != leader))
                    if (unit.TraitOrDefault<LimitedAmmo>().HasAmmo())
                        world.IssueOrder(new Order("Attack", unit, false) { TargetActor = enemy });
                    else
                        if (!unit.GetCurrentActivity().ToString().Contains("Land") && unit.HasTrait<Helicopter>())
                            world.IssueOrder(new Order("ReturnToBase", unit, false));
                        else if (unit.HasTrait<Plane>())
                            world.IssueOrder(new Order("ReturnToBase", unit, false));
            }
            else
                foreach (Actor unit in units)
                    if (unit.TraitOrDefault<LimitedAmmo>().HasAmmo())
                        world.IssueOrder(new Order("Attack", unit, queue) { TargetActor = enemy });
                    else
                        if (!unit.GetCurrentActivity().ToString().Contains("Land") && unit.HasTrait<Helicopter>())
                            world.IssueOrder(new Order("ReturnToBase", unit, false));
                        else if (unit.HasTrait<Plane>())
                            world.IssueOrder(new Order("ReturnToBase", unit, false));
        }

        public void Move(CPos target, bool queue, int dist)
        {
            if (target == null)
                return;
            Actor leader = units.First(); // units.ClosestTo(target.ToPPos());
            if (leader == null)
                return;
            var ownUnits = world.FindUnitsInCircle(leader.CenterLocation, Game.CellSize * dist).Where(a => a.Owner == units.FirstOrDefault().Owner && this.units.Contains(a)).ToList();
            if (ownUnits.Count < units.Count)
            {
                world.IssueOrder(new Order("Stop", leader, false));
                foreach (var unit in units.Where(a => !ownUnits.Contains(a)))
                    world.IssueOrder(new Order("Move", unit, false) { TargetLocation = leader.CenterLocation.ToCPos() });
            }
            else
            {
                world.IssueOrder(new Order("Move", leader, queue) { TargetLocation = target });
                foreach (Actor unit in units.Where(a => a != leader))
                    world.IssueOrder(new Order("Move", unit, queue) { TargetLocation = leader.CenterLocation.ToCPos() });
            }
        }
    };

    class BetaAI : ITick, IBot, INotifyDamage
    {
        bool enabled;
        public int ticks;
        public int assignRolesTicks = 0;
        public int assignRolesTicks2 = 0;
        public Player p;
        PowerManager playerPower;
        SupportPowerManager playerSupport;
        PlayerResources playerResource;
        readonly BuildingInfo rallypointTestBuilding;
        readonly BetaAIInfo Info;

        Cache<Player, Enemy> aggro = new Cache<Player, Enemy>(_ => new Enemy());

        CPos baseCenter;

        XRandom random;
        BaseBuilder[] builders;

        const int MaxBaseDistance = 40;
        public const int feedbackTime = 60;

        public string general;

        public World world { get { return p.PlayerActor.World; } }
        IBotInfo IBot.Info { get { return this.Info; } }

        public BetaAI(BetaAIInfo Info)
        {
            this.Info = Info;
            this.rallypointTestBuilding = Rules.Info["silo"].Traits.Get<BuildingInfo>(); /* so wrong */
        }

        public static void BotDebug(string s, params object[] args)
        {
            if (Game.Settings.Debug.BotDebug)
                Game.Debug(s, args);
        }

        public void Activate(Player p)
        {
            this.p = p;
            enabled = true;
            playerSupport = p.PlayerActor.Trait<SupportPowerManager>();
            playerPower = p.PlayerActor.Trait<PowerManager>();
            playerResource = p.PlayerActor.Trait<PlayerResources>();
            builders = new BaseBuilder[] {
                                new BaseBuilder( this, "Building", q => ChooseBuildingToBuild(q, true) ),
                                new BaseBuilder( this, "Defense", q => ChooseBuildingToBuild(q, false) ) };

            assignRolesTicks = Info.AssignRolesInterval;
            assignRolesTicks2 = Info.AssignRolesInterval2;

            random = new XRandom((int)p.PlayerActor.ActorID + 100 * (int)p.PlayerActor.ActorID);

            general = Info.UnitQueues[random.Next(0, Info.UnitQueues.Length - 1)];

            // p.World.IssueOrder(Order.Chat(false, "BetaAI: " + p.PlayerName + ", General:" + general));
        }

        int GetPowerProvidedBy(ActorInfo building)
        {
            var bi = building.Traits.GetOrDefault<BuildingInfo>();
            if (bi == null) return 0;
            return bi.Power;
        }

        ActorInfo ChooseRandomUnitToBuild(ProductionQueue queue)
        {
            float value = 0.0F;

            var buildableThings = queue.BuildableItems();
            if (!buildableThings.Any()) return null;

            var myUnits = world.ActorsWithTrait<IMove>().Where(a => a.Actor.Owner == p).Select(a => a.Actor).ToList();
            foreach (var frac in Info.UnitsToBuild)
            {
                float tweak = (float)random.NextDouble() * Info.Tweaks["value"];
                if (Info.generality.ContainsKey(frac.Key) && Info.generality[frac.Key] == general)
                    value = tweak;
                else if (Info.generality.ContainsKey(frac.Key))
                    value = -tweak;
                else
                    value = 0.0F;

                if (buildableThings.Any(b => b.Name == frac.Key))
                    if (myUnits.Count(a => a.Info.Name == frac.Key) < (frac.Value + value) * myUnits.Count())
                        return buildableThings.Where(b => b.Name == frac.Key).FirstOrDefault();
            }
            return null;
        }

        bool HasAdequatePower()
        {
            return playerPower.PowerProvided > 50 &&
                playerPower.PowerProvided > playerPower.PowerDrained * 1.2;
        }

        int countBuilding(string frac, Player owner)
        {
            return world.ActorsWithTrait<Building>().Where(a => a.Actor.Owner == owner && a.Actor.Info.Name == frac).Count();
        }

        public bool HasAdequateNumber(string frac, Player owner)
        {
            int count = countBuilding(frac, owner);

            if (frac == "fix" && count >= Info.BuildingLimits[frac] * countBuilding("weap", owner))
                return false;
            if (frac == "hpad" && count > 0)
                if (count != world.ActorsWithTrait<Helicopter>().Where(a => a.Actor.Owner == owner).Count())
                    return false;
            if (frac == "afld" && count > 0)
                if (count != world.ActorsWithTrait<Plane>().Where(a => a.Actor.Owner == owner).Count())
                    return false;

            if (Info.BuildingLimits.ContainsKey(frac))
                if (count < Info.BuildingLimits[frac] && ticks >= Info.TicketsLimits["iteration" + count])
                    return true;
                else
                    return false;

            return true;
        }

        bool shouldSell(Actor a, int money)
        {
            var h = a.TraitOrDefault<Health>();
            var si = a.Info.Traits.Get<SellableInfo>();
            var cost = a.GetSellValue();
            var refund = (cost * si.RefundPercent * (h == null ? 1 : h.HP)) / (100 * (h == null ? 1 : h.MaxHP));

            if (playerResource.Cash >= money)
                return false;
            return true;
        }

        public bool HasAdequateProc()
        {
            if (countBuilding("proc", p) == 0 && (countBuilding("powr", p) > 0 || countBuilding("apwr", p) > 0))
                return false;
            return true;
        }

        public bool HasAdequateFact()
        {
            if (countBuilding("fact", p) == 0 && countBuilding("weap", p) > 0)
                return false;
            return true;
        }

        void BuildRandom(string category)
        {
            if (!HasAdequateProc()) /* Stop building until economy is back on */
                return;

            var queue = FindQueues(category).FirstOrDefault(q => q.CurrentItem() == null);
            if (queue == null)
                return;

            var unit = ChooseRandomUnitToBuild(queue);

            if (!HasAdequateFact())
                unit = Rules.Info["mcv"];

            if (unit != null)
            {
                if (unit.Name == "mig" || unit.Name == "yak")
                {
                    int count = countBuilding("afld", p);
                    var myUnits_plane = world.ActorsWithTrait<Plane>().Where(a => a.Actor.Owner == p).Count();
                    if (myUnits_plane >= count)
                        return;
                }
                else if (unit.Name == "hind" || unit.Name == "heli" || unit.Name == "tran")
                {
                    int count = countBuilding("hpad", p);
                    var myUnits_copter = world.ActorsWithTrait<Helicopter>().Where(a => a.Actor.Owner == p).Count();
                    if (myUnits_copter >= count)
                        return;
                }
                else if (unit.Name == "harv")
                {
                    var myUnits_harv = world.ActorsWithTrait<Harvester>().Where(a => a.Actor.Owner == p).Count();
                    int count = countBuilding("proc", p);
                    if (myUnits_harv >= count * 2)
                        return;
                }
                else if (unit.Name == "ss")
                {
                    var enemyUnits = world.ActorsWithTrait<Mobile>().Where(a => a.Actor.Owner != p && p.Stances[a.Actor.Owner] == Stance.Enemy);
                    var enemyUnits_ship = enemyUnits.Where(a => a.Actor.Info.Name.Equals("spen") || a.Actor.Info.Name.Equals("syrd")).Count();
                    if (enemyUnits_ship == 0)
                        return;
                }
                world.IssueOrder(Order.StartProduction(queue.self, unit.Name, 1));
            }
        }

        ActorInfo ChooseBuildingToBuild(ProductionQueue queue, bool buildPower)
        {
            float value = 0.0F;

            var buildableThings = queue.BuildableItems();

            if (!HasAdequatePower())    /* try to maintain 20% excess power */
            {
                if (!buildPower) return null;

                /* find the best thing we can build which produces power */
                return buildableThings.Where(a => GetPowerProvidedBy(a) > 0)
                    .OrderByDescending(a => GetPowerProvidedBy(a)).FirstOrDefault();
            }

            if (playerResource.AlertSilo)
                return Rules.Info["silo"]; /* Force silo construction on Alert */

            var myBuildings = world.ActorsWithTrait<Building>().Where(a => a.Actor.Owner == p).ToArray();

            foreach (var frac in Info.BuildingFractions)
            {
                float tweak = (float)random.NextDouble() * Info.Tweaks["value"];
                if (Info.generality.ContainsKey(frac.Key) && Info.generality[frac.Key] == general)
                    value = tweak;
                else if (Info.generality.ContainsKey(frac.Key))
                    value = -tweak;
                else
                    value = 0.0F;

                if (buildableThings.Any(b => b.Name == frac.Key))
                    if (myBuildings.Count(a => a.Actor.Info.Name == frac.Key) < (frac.Value + value) * myBuildings.Length && playerPower.ExcessPower >= Rules.Info[frac.Key].Traits.Get<BuildingInfo>().Power)
                        if (HasAdequateNumber(frac.Key, p)) /* C'mon... */
                            return Rules.Info[frac.Key];
            }

            return null;
        }

        bool NoBuildingsUnder(IEnumerable<CPos> cells)
        {
            var bi = world.WorldActor.Trait<BuildingInfluence>();
            return cells.All(c => bi.GetBuildingAt(c) == null);
        }

        // AI improvement, should reduce lag
        List<string> tried = new List<string>();
        bool firstbuild = true;
        CPos defenseCenter;

        public CPos? ChooseBuildLocation(string actorType, bool defense)
        {
            if (tried.Contains(Rules.Info[actorType].Name))
                return null;

            var bi = Rules.Info[actorType].Traits.Get<BuildingInfo>();

            if (bi == null)
                return null;

            if (defense)
            {
                Actor owner = ChooseEnemyTarget("base");
                for (var k = MaxBaseDistance; k >= 0; k--)
                {
                    if (owner != null)
                    {
                        var tlist = world.FindTilesInCircle(defenseCenter, k).OrderBy(a => (new PPos(a.ToPPos().X, a.ToPPos().Y) - owner.CenterLocation).LengthSquared);
                        foreach (var t in tlist)
                            if (world.CanPlaceBuilding(actorType, bi, t, null))
                                if (bi.IsCloseEnoughToBase(world, p, actorType, t))
                                    if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(actorType, bi, t), false)))
                                        return t;
                    }
                }
            }
            else
            {
                for (var k = 0; k < MaxBaseDistance; k++)
                    foreach (var t in world.FindTilesInCircle(baseCenter, k))
                        if (world.CanPlaceBuilding(actorType, bi, t, null))
                            if (bi.IsCloseEnoughToBase(world, p, actorType, t) || firstbuild)
                                if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(actorType, bi, t), false)))
                                {
                                    firstbuild = false;
                                    return t;
                                }
            }

            tried.Add(Rules.Info[actorType].Name);

            return null;
        }

        public void HandleSuperpowers(Actor self)
        {
            foreach (var pow in p.PlayerActor.Trait<SupportPowerManager>().Powers)
            {
                switch (pow.Key)
                {
                    case "NukePowerInfoOrder":
                        Actor nuke = world.Actors.Where(a => a.HasTrait<NukePower>() && a.Owner == p).FirstOrDefault();
                        if (nuke == null)
                            break;
                        var timer_n = pow.Value.RemainingTime;
                        var weapon_n = nuke.Trait<NukePower>();
                        if (timer_n == 0)
                        {
                            weapon_n.Activate(nuke, new Order() { TargetLocation = ChooseEnemyTarget("nuke").CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_n.Info.ChargeTime * 25;
                        }
                        break;
                    case "GpsPowerInfoOrder": /* useless ? */
                        Actor gps = world.Actors.Where(a => a.HasTrait<GpsPower>() && a.Owner == p).FirstOrDefault();
                        if (gps == null)
                            break;
                        var timer_g = pow.Value.RemainingTime;
                        var weapon_g = gps.Trait<GpsPower>();
                        if (timer_g == 0)
                        {
                            weapon_g.Activate(gps, new Order());
                            pow.Value.RemainingTime = weapon_g.Info.ChargeTime * 25;
                        }
                        break;
                    case "AirstrikePowerInfoOrder":
                        Actor airstrike = world.Actors.Where(a => a.HasTrait<AirstrikePower>() && a.Owner == p).FirstOrDefault();
                        if (airstrike == null)
                            break;
                        var timer_a = pow.Value.RemainingTime;
                        var weapon_a = airstrike.Trait<AirstrikePower>();
                        if (timer_a == 0)
                        {
                            weapon_a.Activate(airstrike, new Order() { TargetLocation = ChooseEnemyTarget(null).CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_a.Info.ChargeTime * 25;
                        }
                        break;
                    case "ParatroopersPowerInfoOrder":
                        Actor para = world.Actors.Where(a => a.HasTrait<ParatroopersPower>() && a.Owner == p).FirstOrDefault();
                        if (para == null)
                            break;
                        var timer_p = pow.Value.RemainingTime;
                        var weapon_p = para.Trait<ParatroopersPower>();
                        if (timer_p == 0)
                        {
                            weapon_p.Activate(para, new Order() { TargetLocation = ChooseEnemyTarget(null).CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_p.Info.ChargeTime * 25;
                        }
                        break;
                    case "SpyPlanePowerInfoOrder":
                        Actor spy = world.Actors.Where(a => a.HasTrait<SpyPlanePower>() && a.Owner == p).FirstOrDefault();
                        if (spy == null)
                            break;
                        var timer_s = pow.Value.RemainingTime;
                        var weapon_s = spy.Trait<SpyPlanePower>();
                        if (timer_s == 0)
                        {
                            weapon_s.Activate(spy, new Order() { TargetLocation = ChooseEnemyTarget("nuke").CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_s.Info.ChargeTime * 25;
                        }
                        break;
                    case "IronCurtainPowerInfoOrder":
                        Actor iron = world.Actors.Where(a => a.HasTrait<IronCurtainPower>() && a.Owner == p).FirstOrDefault();
                        if (iron == null)
                            break;
                        var timer_i = pow.Value.RemainingTime;
                        var weapon_i = iron.Trait<IronCurtainPower>();
                        if (timer_i == 0)
                        {
                            Squad actorsi = squads.Where(a => a.isFull()).Random(random);

                            if (actorsi == null)
                                break;

                            Actor actori = actorsi.units.FirstOrDefault();

                            if (actori == null)
                                break;

                            weapon_i.Activate(iron, new Order() { TargetLocation = actori.CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_i.Info.ChargeTime * 25;
                        }
                        break;
                    case "ChronoshiftPowerInfoOrder": /* :'( */
                        Actor chrono = world.Actors.Where(a => a.HasTrait<ChronoshiftPower>() && a.Owner == p).FirstOrDefault();
                        if (chrono == null)
                            break;
                        var timer_c = pow.Value.RemainingTime;
                        var weapon_c = chrono.Trait<ChronoshiftPower>();
                        if (timer_c == 0)
                        {
                            Squad actors = null;

                            foreach (var squad in squads.Where(a => a.type == "Assault" && a.isFull()))
                                actors = squad;

                            if (actors == null)
                                break;

                            var target_c = ChooseEnemyTarget("nuke");

                            List<Pair<Actor, CPos>> location = new List<Pair<Actor, CPos>>();

                            var bi = Rules.Info["silo"].Traits.Get<BuildingInfo>();

                            foreach (var unit in actors.units)
                            {
                                for (var k = 0; k < 10; k++)
                                    foreach (var t in world.FindTilesInCircle(target_c.CenterLocation.ToCPos(), k))
                                        if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles("silo", bi, t), false)))
                                        {
                                            location.Add(new Pair<Actor, CPos>(unit, t));
                                            world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target_c });
                                        }
                            }

                            Scripting.RASpecialPowers.Chronoshift(self.World, location, chrono, -1, false);
                            pow.Value.RemainingTime = weapon_c.Info.ChargeTime * 25;
                        }
                        break;
                }
            }
        }

        public void Tick(Actor self)
        {

            // 9000 ticks = 6min
            if (!enabled)
                return;

            ticks++;

            if (ticks == 1)
                DeployMcv(self);

            if (ticks % feedbackTime == 0)
                foreach (var q in Info.UnitQueues)
                    BuildRandom(q);

            AssignRolesToUnits(self);
            SetRallyPointsForNewProductionBuildings(self);
            HandleSuperpowers(self);

            foreach (var b in builders)
                b.Tick();
        }

        Actor attackTarget;
        CPos attackTargetLocation;
        Actor repairTarget;

        Actor ChooseEnemyTarget(string type)
        {
            var liveEnemies = world.Players
                .Where(q => p != q && p.Stances[q] == Stance.Enemy)
                .Where(q => p.WinState == WinState.Undefined && q.WinState == WinState.Undefined);

            if (!liveEnemies.Any())
                return null;

            var leastLikedEnemies = liveEnemies
                .GroupBy(e => aggro[e].Aggro)
                .OrderByDescending(g => g.Key)
                .FirstOrDefault();

            Player enemy;
            if (leastLikedEnemies == null)
                enemy = liveEnemies.FirstOrDefault();
            else
                enemy = leastLikedEnemies.Random(random);

            /* pick something worth attacking owned by that player */
            List<Actor> targets = null;

            switch (type)
            {
                case "base":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && a.Info.Name == "fact").ToList();
                    break;

                case "nuke":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && a.HasTrait<RepairableBuilding>()).ToList();
                    break;

                case "air":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && !(a.HasTrait<Helicopter>() || a.HasTrait<Plane>())).ToList();
                    break;

                case "sub":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && (a.Info.Name.Equals("ca") || a.Info.Name.Equals("dd") || a.Info.Name.Equals("msub") || a.Info.Name.Equals("pt"))).ToList();
                    break;

                case "long":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed).ToList();
                    break;

                default:
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && !a.HasTrait<RepairableBuilding>() && !(a.Info.Name.Equals("ca") || a.Info.Name.Equals("dd") || a.Info.Name.Equals("msub") || a.Info.Name.Equals("pt"))).ToList();
                    break;
            }

            Actor target = null;

            if (targets.Any() && baseCenter != null)
                target = targets.ClosestTo(baseCenter.ToPPos());

            if (target == null)
            {
                /* Assume that "enemy" has nothing. Cool off on attacks. */
                aggro[enemy].Aggro = aggro[enemy].Aggro / 2 - 1;
                Log.Write("debug", "Bot {0} couldn't find target for player {1}", this.p.ClientIndex, enemy.ClientIndex);
            }

            /* bump the aggro slightly to avoid changing our mind */
            if (leastLikedEnemies.Count() > 1)
                aggro[enemy].Aggro++;

            return target;
        }

        List<Actor> zombies = new List<Actor>();
        List<Squad> squads = new List<Squad>();

        bool IsInSquad(Actor unit)
        {
            foreach (Squad squad in squads)
                if (squad.units.Contains(unit))
                    return true;
            return false;
        }

        bool existeCompatibleSquad(Actor unit)
        {
            foreach (Squad squad in squads.Where(a => !a.isFull()))
                if (Info.Affinities[unit.Info.Name].Contains(squad.type))
                    return true;
            return false;
        }

        void cleanSquads()
        {
            foreach (Squad squad in squads)
                squad.units.RemoveAll(a => a.Destroyed || a.IsDead());
        }

        void AssignRolesToUnits(Actor playeractor)
        {
            cleanSquads();

            if (--assignRolesTicks > 0)
                return;
            else
                assignRolesTicks = Info.AssignRolesInterval;

            zombies = world.ActorsWithTrait<IMove>().Where(a => a.Actor.Owner == p && !a.Actor.HasTrait<BaseBuilding>() && !IsInSquad(a.Actor)).Select(a => a.Actor).ToList(); /* It's Alive ! Alive ! */

            /* Squad research and assignement */
            foreach (Actor zombie in zombies.Where(a => Info.Affinities.ContainsKey(a.Info.Name)))
            {
                if (existeCompatibleSquad(zombie))
                    foreach (Squad squad in squads.Where(a => !a.isFull() && Info.Affinities[zombie.Info.Name].Contains(a.type)))
                        squad.units.Add(zombie);
                else
                {
                    Squad temp = new Squad(world, Info.Affinities[zombie.Info.Name].FirstOrDefault(), Info);
                    temp.units.Add(zombie);
                    squads.Add(temp);
                }
            }

            foreach (Squad squad in squads.Where(a => a.type != "sub" || a.type != "ship"))
                squad.React();

            foreach (Squad squad in squads.Where(a => a.isFull()))
            {
                attackTarget = ChooseEnemyTarget(squad.type);
                if (attackTarget == null)
                    continue;
                attackTargetLocation = attackTarget.CenterLocation.ToCPos();
                if (attackTargetLocation == null)
                    continue;

                switch (squad.type)
                {
                    case "infiltrate":
                    case "infantry":
                        squad.Move(attackTargetLocation, true, 2);
                        break;

                    case "rush":
                    case "assault":
                    case "long":
                        squad.Move(attackTargetLocation, true, 4);
                        break;

                    case "air":
                        squad.MoveAir(attackTargetLocation, attackTarget, true, 10);
                        break;

                    case "ship":
                    case "sub":
                        squad.MoveShip(attackTargetLocation, attackTarget);
                        break;
                }
            }
        }

        bool IsRallyPointValid(CPos x)
        {
            // this is actually WRONG as soon as BetaAI is building units with a variety of
            // movement capabilities. (has always been wrong)
            return world.IsCellBuildable(x, rallypointTestBuilding);
        }

        void SetRallyPointsForNewProductionBuildings(Actor self)
        {
            var buildings = world.ActorsWithTrait<RallyPoint>()
                .Where(rp => rp.Actor.Owner == p &&
                    !IsRallyPointValid(rp.Trait.rallyPoint)).ToArray();

            foreach (var a in buildings)
            {
                CPos newRallyPoint = ChooseRallyLocationNear(self, a.Actor.Location);
                world.IssueOrder(new Order("SetRallyPoint", a.Actor, false) { TargetLocation = newRallyPoint });
            }
        }

        CPos ChooseRallyLocationNear(Actor self, CPos startPos)
        {
            List<CPos> possibleRallyPoints = new List<CPos>();

            if (self.Info.Name.Equals("spen") || self.Info.Name.Equals("syrd"))
                possibleRallyPoints = world.FindTilesInCircle(startPos, 8).Where(a => world.GetTerrainType(new CPos(a.X, a.Y)).Equals("Water")).ToList();
            else
                possibleRallyPoints = world.FindTilesInCircle(startPos, 8).Where(IsRallyPointValid).ToList();

            if (possibleRallyPoints.Count == 0)
                return startPos;

            return possibleRallyPoints.Random(random);
        }

        void DeployMcv(Actor self)
        {
            /* find our mcv and deploy it */
            var mcv = self.World.Actors
                .FirstOrDefault(a => a.Owner == p && a.HasTrait<BaseBuilding>());

            if (mcv != null)
            {
                baseCenter = mcv.Location;
                defenseCenter = baseCenter;
                //Don't transform the mcv if it is a fact
                if (mcv.HasTrait<Mobile>())
                {
                    world.IssueOrder(new Order("DeployTransform", mcv, false));
                }
            }
            else
                BotDebug("AI: Can't find BaseBuildUnit.");
        }

        internal IEnumerable<ProductionQueue> FindQueues(string category)
        {
            return world.ActorsWithTrait<ProductionQueue>()
                .Where(a => a.Actor.Owner == p && a.Trait.Info.Type == category)
                .Select(a => a.Trait);
        }

        public void Damaged(Actor self, AttackInfo e)
        {
            if (!enabled) return;
            if (e.Attacker.Destroyed) return;
            if (!e.Attacker.HasTrait<ITargetable>()) return;

            if (Info.ShouldRepairBuildings && self.HasTrait<RepairableBuilding>())
            {
                if (e.DamageState > DamageState.Light && e.PreviousDamageState <= DamageState.Light)
                {
                    if (e.Attacker.HasTrait<IHasLocation>())
                    {
                        defenseCenter = e.Attacker.CenterLocation.ToCPos(); /* may be used for counter attack */
                        foreach (Squad squad in squads.Where(a => !a.isFull() && a.units.Count > 0))
                            squad.Move(defenseCenter, true, 4);
                    }
                    repairTarget = self; /* may be used by engineers */
                    world.IssueOrder(new Order("RepairBuilding", self.Owner.PlayerActor, false) { TargetActor = self });
                }
            }

            if (e.Attacker != null && e.Damage > 0 && p.Stances[e.Attacker.Owner] == Stance.Enemy)
                aggro[e.Attacker.Owner].Aggro += e.Damage;
        }
    }
}