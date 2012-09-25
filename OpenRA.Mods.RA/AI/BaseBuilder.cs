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
using OpenRA.Traits;
using OpenRA.Mods.RA.Activities;
using System.Threading;

namespace OpenRA.Mods.RA.AI
{
    class BaseBuilder
    {
        enum BuildState { ChooseItem, WaitForProduction, WaitForFeedback }

        BuildState state = BuildState.WaitForFeedback;
        string category;
        BetaAI ai;
        int lastThinkTick;
        Func<ProductionQueue, ActorInfo> chooseItem;

        public BaseBuilder(BetaAI ai, string category, Func<ProductionQueue, ActorInfo> chooseItem)
        {
            this.ai = ai;
            this.category = category;
            this.chooseItem = chooseItem;
        }

        public void Tick()
        {
            // Pick a free queue
            var queue = ai.FindQueues(category).FirstOrDefault();
            if (queue == null)
                return;

            var currentBuilding = queue.CurrentItem();
            switch (state)
            {
                case BuildState.ChooseItem:
                    {
                        var item = chooseItem(queue);
                        if (item == null)
                        {
                            state = BuildState.WaitForFeedback;
                            lastThinkTick = ai.ticks;
                        }
                        else
                        {
                            if (ai.HasAdequateNumber(item.Name, ai.p)) /* C'mon... */
                            {
                                state = BuildState.WaitForProduction;
                                ai.world.IssueOrder(Order.StartProduction(queue.self, item.Name, 1));
                            }
                        }
                    }
                    break;

                case BuildState.WaitForProduction:
                    if (currentBuilding == null) return;	/* let it happen.. */

                    else if (currentBuilding.Paused)
                        ai.world.IssueOrder(Order.PauseProduction(queue.self, currentBuilding.Item, false));
                    else if (currentBuilding.Done)
                    {
                        state = BuildState.WaitForFeedback;
                        lastThinkTick = ai.ticks;

                        /* place the building */
                        bool defense = false;
                        if (currentBuilding.Item.Equals("sam") || currentBuilding.Item.Equals("agun") || currentBuilding.Item.Equals("ftur") || currentBuilding.Item.Equals("tsla") || currentBuilding.Item.Equals("gun") || currentBuilding.Item.Contains("hbox") || currentBuilding.Item.Contains("pbox"))
                            defense = true;
                        CPos? location = ai.ChooseBuildLocation(currentBuilding.Item, defense);

                        if (location == null) /* C'mon... */
                        {
                            BetaAI.BotDebug("AI: Nowhere to place or no adequate number {0}".F(currentBuilding.Item));
                            ai.world.IssueOrder(Order.CancelProduction(queue.self, currentBuilding.Item, 1));
                        }
                        else
                            ai.world.IssueOrder(new Order("PlaceBuilding", ai.p.PlayerActor, false)
                                {
                                    TargetLocation = location.Value,
                                    TargetString = currentBuilding.Item
                                });
                    }

                    if (!ai.HasAdequateNumber(currentBuilding.Item, ai.p))
                        ai.world.IssueOrder(Order.CancelProduction(queue.self, currentBuilding.Item, 1));

                    break;

                case BuildState.WaitForFeedback:
                    if (ai.ticks - lastThinkTick > BetaAI.feedbackTime)
                        state = BuildState.ChooseItem;
                    break;
            }
        }
    }
}

