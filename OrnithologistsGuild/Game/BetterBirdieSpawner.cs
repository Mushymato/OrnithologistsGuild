﻿using System;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Locations;
using System.Collections.Generic;
using System.Linq;
using DynamicGameAssets.Game;
using Microsoft.Xna.Framework;

namespace OrnithologistsGuild
{
    public class BetterBirdieSpawner
    {
        private const bool DEBUG_ALWAYS_SPAWN = true;

        public static void AddBirdies(GameLocation location, double chance = 0, bool onlyIfOnScreen = false)
        {
            // No birdies past 8:00 PM (it's their bedtime), in the desert or railroad
            if (Game1.timeOfDay >= 1800 || !location.IsOutdoors || location is Desert || (location is Railroad)) return;

            ModEntry.instance.Monitor.Log("AddBirdies");

            // First, get locations of all bird feeders
            foreach (var overlaidDict in location.Objects)
            {
                foreach (var obj in overlaidDict.Values)
                {
                    if (typeof(CustomBigCraftable).IsAssignableFrom(obj.GetType()))
                    {
                        var bigCraftable = (CustomBigCraftable)obj;

                        // Only attract birds if there is food
                        if (bigCraftable.MinutesUntilReady > 0)
                        {
                            var feeder = DataManager.Feeders.FirstOrDefault(feeder => feeder.id == bigCraftable.Id);
                            if (feeder != null)
                            {
                                var food = DataManager.Foods.FirstOrDefault(food => bigCraftable.TextureOverride.EndsWith($":{food.feederAssetIndex}"));
                                if (food != null)
                                {
                                    AddBirdsNearFeeder(location, bigCraftable.TileLocation, feeder, food, onlyIfOnScreen);
                                }
                            }
                        }
                    }
                }
            }

            if (chance > 0) AddRandomBirdies(location, chance, onlyIfOnScreen);
        }

        private static void AddRandomBirdies(GameLocation location, double chance, bool onlyIfOnScreen)
        {
            ModEntry.instance.Monitor.Log("AddRandomBirdies");

            if (DEBUG_ALWAYS_SPAWN && location.critters.Count > 0) return;

            Models.BirdieModel flockSpecies = null;

            // Override chance
            if (location is Farm) chance = 0.15;

            // Chance to add another flock
            int flocksAdded = 0;
            while ((DEBUG_ALWAYS_SPAWN && flocksAdded == 0) || Game1.random.NextDouble() < chance / (flocksAdded + 1)) // Chance lowers after every flock
            {
                // Determine flock parameters
                flockSpecies = GetRandomBirdie();
                int flockSize = DEBUG_ALWAYS_SPAWN ? 1 : Game1.random.Next(1, flockSpecies.maxFlockSize + 1);

                // Try 50 times to find an empty patch within the location
                for (int trial = 0; trial < 50; trial++)
                {
                    // Get a random tile within the feeder range
                    var randomTile = location.getRandomTile();

                    if (!onlyIfOnScreen || !Utility.isOnScreen(randomTile * Game1.tileSize, Game1.tileSize))
                    {
                        // Get a 3x3 patch around the random tile
                        var randomRect = new Microsoft.Xna.Framework.Rectangle((int)randomTile.X - 1, (int)randomTile.Y - 1, 3, 3);

                        if (!location.isAreaClear(randomRect)) continue;

                        ModEntry.instance.Monitor.Log($"Found clear location at {randomRect}, adding flock of {flockSize} {flockSpecies.name} ({flockSpecies.id})");

                        // Spawn birdies
                        List<Critter> crittersToAdd = new List<Critter>();
                        for (int index = 0; index < flockSize; ++index)
                        {
                            crittersToAdd.Add((Critter)new BetterBirdie(flockSpecies, -100, -100));
                        }

                        ModEntry.instance.Helper.Reflection.GetMethod(location, "addCrittersStartingAtTile").Invoke(randomTile, crittersToAdd);

                        flocksAdded++;

                        break;
                    }
                }
            }
        }

        private static void AddBirdsNearFeeder(GameLocation location, Vector2 feederTile, Models.FeederModel feeder, Models.FoodModel food, bool onlyIfOnScreen)
        {
            ModEntry.instance.Monitor.Log("AddBirdsNearFeeder");

            // Build a rectangle around the feeder based on the range
            var feederRect = Utility.getRectangleCenteredAt(feederTile, (feeder.range * 2) + 1);

            ModEntry.instance.Monitor.Log(feederRect.ToString() + " / " + feederRect.ToString());

            Models.BirdieModel flockSpecies = null;

            // Chance to add another flock
            int flocksAdded = 0;
            while (flocksAdded < feeder.maxFlocks && Game1.random.NextDouble() < 0.4)
            {
                ModEntry.instance.Monitor.Log("Trying to spawn flock within " + feederRect.ToString());

                // Determine flock parameters
                flockSpecies = GetRandomFeederBirdie(feeder, food);
                int flockSize = Game1.random.Next(1, flockSpecies.maxFlockSize + 1);

                var shouldAddBirdToFeeder = flocksAdded == 0 && Game1.random.NextDouble() < 0.65 && (!onlyIfOnScreen || !Utility.isOnScreen(feederTile * Game1.tileSize, Game1.tileSize));
                if (shouldAddBirdToFeeder) flockSize -= 1;

                // Try 50 times to find an empty patch within the feeder range
                for (int trial = 0; trial < 50; trial++)
                {
                    // Get a random tile within the feeder range
                    var randomTile = Utility.getRandomPositionInThisRectangle(feederRect, Game1.random);

                    if (location.isTileOnMap(randomTile) && (!onlyIfOnScreen || !Utility.isOnScreen(randomTile * Game1.tileSize, Game1.tileSize)))
                    {
                        // Get a 3x3 patch around the random tile 
                        // var randomRect = new Microsoft.Xna.Framework.Rectangle((int)randomTile.X - 2, (int)randomTile.Y - 2, 5, 5); // TODO revert to 5x5 if needed
                        var randomRect = new Microsoft.Xna.Framework.Rectangle((int)randomTile.X - 1, (int)randomTile.Y - 1, 3, 3);

                        if (!location.isAreaClear(randomRect)) continue;

                        ModEntry.instance.Monitor.Log($"Found clear location at {randomRect}, adding flock of {flockSize} {flockSpecies.name} ({flockSpecies.id})");

                        // Spawn birdies
                        List<Critter> crittersToAdd = new List<Critter>();

                        for (int index = 0; index < flockSize; ++index)
                        {
                            crittersToAdd.Add((Critter)new BetterBirdie(flockSpecies, -100, -100));
                        }

                        ModEntry.instance.Helper.Reflection.GetMethod(location, "addCrittersStartingAtTile").Invoke(randomTile, crittersToAdd);

                        flocksAdded++;
                        break;
                    }
                }

                if (shouldAddBirdToFeeder)
                {
                    location.addCritter((Critter)new BetterBirdie(flockSpecies, (int)feederTile.X, (int)feederTile.Y, feeder));
                }
            }
        }

        private static Models.BirdieModel GetRandomBirdie()
        {
            if (DEBUG_ALWAYS_SPAWN) return DataManager.Birdies.First(b => b.id == "Chickadee");

            return Utilities.WeightedRandom<Models.BirdieModel>(DataManager.Birdies, birdie => birdie.weightedRandom * birdie.seasonalMultiplier[Game1.currentSeason]);
        }

        private static Models.BirdieModel GetRandomFeederBirdie(Models.FeederModel feeder, Models.FoodModel food)
        {
            var usualSuspects = DataManager.Birdies.Where(b => b.weightedFeeders.ContainsKey(feeder.type) && b.weightedFoods.ContainsKey(food.id));

            return Utilities.WeightedRandom<Models.BirdieModel>(usualSuspects, birdie => (birdie.weightedFeeders[feeder.type] + birdie.weightedFoods[food.id]) * birdie.seasonalMultiplier[Game1.currentSeason]);
        }
    }
}
