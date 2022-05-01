using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class HexMapGenerator {

  
    public int highlandBudget = 21, lowlandBudget = 34, 
               riverValleyBudget = 26, coastalBudget = 28,
               totalBudget;
    List<HexCell> dryLand = new List<HexCell>();
    
    public void GenerateMightyEmpiresMap(int x, int z, int numberOfPlayers) {
        Random.State originalRandomState = Random.state;
        if (!useFixedSeed) {
            seed = Random.Range(0, int.MaxValue);
            seed ^= (int)System.DateTime.Now.Ticks;
            seed ^= (int)Time.time;
            seed &= int.MaxValue;
        }
        Random.InitState(seed);
        Debug.Log("Triggering Mighty Empires Map Generation with seed " + seed);
        cellCount = x * z;
        grid.CreateMap(x, z, numberOfPlayers);
        if (searchFrontier == null) {
            searchFrontier = new HexCellPriorityQueue();
        }
        for (int i = 0; i < cellCount; i++) {
            grid.GetCell(i).WaterLevel = waterLevel;
        }
        //Plonk down a mountain
        totalBudget = highlandBudget + lowlandBudget + riverValleyBudget + coastalBudget;
        CreateMightyEmpiresRegion();
        CreateEmpiresLand();
        ScatterMountains(highlandBudget);
        //IdentifyOcean();
        AllocateCoast(coastalBudget);
        DrawRivers(riverValleyBudget);
        AssignLowlands(lowlandBudget);
        SetEmpireTerrainType();
        grid.dryLand = this.dryLand;
        Random.state = originalRandomState;

    }
    
    void CreateMightyEmpiresRegion() {
        if (regions == null) {
            regions = new List<MapRegion>();
        }
        else {
            regions.Clear();
        }
        MapRegion region;
        region.xMin = mapBorderX;
        region.xMax = grid.cellCountX - mapBorderX;
        region.zMin = mapBorderZ;
        region.zMax = grid.cellCountZ - mapBorderZ;
        regions.Add(region);
    }


    
    void CreateEmpiresLand() {
        int landBudget = 112;
        dryLand.Clear();
        HexCell origin = grid.GetCenterCell();
        origin.Elevation = waterLevel;
        origin.TerrainTypeIndex = 1;
        landCells++;
        dryLand.Add(origin);
        for (int guard = 0; guard < 10000; guard++) {
            bool sink = Random.value < sinkProbability;
            MapRegion region = regions[0];
            int chunkSize = Random.Range(chunkSizeMin, chunkSizeMax - 1);
            if (sink) {
                landBudget = SinkTerrain(chunkSize, landBudget, region);
            }
            else {
                 landBudget = RaiseEmpireTerrain(chunkSize, landBudget, region);
                 if (landBudget == 0) {
                    return;
                 }
            }
        }
        if (landBudget > 0) {
            Debug.LogWarning("Failed to use up " + landBudget + " land budget.");

        }
    }

    void ScatterMountains(int highLandbudget) {
        int highlands = highlandBudget;
        HexCell cell;
        while(highlands > 0) {
            cell = dryLand[Random.Range(0, dryLand.Count)];
            if (cell.Elevation == waterLevel) {
                cell.Elevation = waterLevel + 2;
                cell.TerrainTypeIndex = 4;
                cell.tileSet = TileSet.HIGHLAND;
                highlands--;
            }
        }
    }
    void AssignLowlands(int budget) {
        for(int i = 0; i < dryLand.Count; i++) {
            HexCell cell = dryLand[i];
            if(cell.tileSet == TileSet.UNALLOCATED) {
                cell.tileSet = TileSet.LOWLAND;
                cell.Elevation = waterLevel + 1;
                cell.TerrainTypeIndex = 1;
            }
        }
    }

    void AllocateCoast(int coastalBudget) {
        //MORE LIKELY TO ALLOCATE COAST TO TILES
        //THAT BORDER MORE SEA OR OTHER COAST
        while (coastalBudget > 0) {

            for (int i = 0; i < dryLand.Count; i++) {
                HexCell current = dryLand[i];
                int borderSeaCount = current.BordersSea();
                if (borderSeaCount > 0 && current.tileSet == TileSet.UNALLOCATED) {
                    if (Random.Range(0, 100) < borderSeaCount) {
                        current.TerrainTypeIndex = 0;
                        current.tileSet = TileSet.COASTAL;
                        if (--coastalBudget == 0) {
                            break;
                        }
                    }
                }
            }
        }
    }

    void DrawRivers(int riverBudget) {
        int riverTileCount = riverBudget;
            List<HexCell> riverOrigins = ListPool<HexCell>.Get();
            for (int i = 0; i < dryLand.Count; i++) {
                HexCell cell = dryLand[i];
                if (cell.IsUnderwater) {
                    continue;
                }
                if(cell.tileSet == TileSet.HIGHLAND) {
                riverOrigins.Add(cell);
                }
            }
            while (riverTileCount > 0 && riverOrigins.Count > 0) {
                int index = Random.Range(0, riverOrigins.Count);
                int lastIndex = riverOrigins.Count - 1;
                HexCell origin = riverOrigins[index];
                riverOrigins[index] = riverOrigins[lastIndex];
                riverOrigins.RemoveAt(lastIndex);

                if (!origin.HasRiver) {
                    bool isValidOrigin = true;
                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
                        HexCell neighbor = origin.GetNeighbor(d);
                        if (neighbor && (neighbor.HasRiver || neighbor.IsUnderwater)) {
                            isValidOrigin = false;
                            break;
                        }
                    }
                    if (isValidOrigin) {
                        riverTileCount -= DrawRiver(origin);
                    }
                }
            }
            if (riverTileCount > 0) {
                Debug.LogWarning("Failed to use up river budget.");
            }
            ListPool<HexCell>.Add(riverOrigins);
        }
    int DrawRiver(HexCell origin) {
        int riverValleyCount = 0;
        HexCell cell = origin;
        HexDirection direction = HexDirection.NE;
        while (!cell.IsUnderwater) {
            int minNeighborElevation = int.MaxValue;
            flowDirections.Clear();
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
                HexCell neighbor = cell.GetNeighbor(d);
                if (!neighbor) {
                    continue;
                }

                if (neighbor.Elevation < minNeighborElevation) {
                    minNeighborElevation = neighbor.Elevation;
                }

                if (neighbor == origin || neighbor.HasIncomingRiver) {
                    continue;
                }
                
                int delta = neighbor.Elevation - cell.Elevation;
                if (delta > 0) {
                    continue;
                }

                if (neighbor.HasOutgoingRiver) {
                    cell.SetOutgoingRiver(d);
                    return riverValleyCount;
                }

                if (delta < 0) {
                    flowDirections.Add(d);
                    flowDirections.Add(d);
                    flowDirections.Add(d);
                }
                if (
                    riverValleyCount == 1 ||
                    (d != direction.Next2() && d != direction.Previous2())
                ) {
                    flowDirections.Add(d);
                }
                flowDirections.Add(d);
            }

            if (flowDirections.Count == 0) {
                if (riverValleyCount == 1) {
                    return 0;
                }

                if (minNeighborElevation >= cell.Elevation) {
                    cell.WaterLevel = minNeighborElevation;
                    if (minNeighborElevation == cell.Elevation) {
                        cell.Elevation = minNeighborElevation - 1;
                    }
                }
                break;
            }

            direction = flowDirections[Random.Range(0, flowDirections.Count)];
            cell.SetOutgoingRiver(direction);
            if(cell.tileSet == TileSet.UNALLOCATED) {
                cell.tileSet = TileSet.RIVERVALLEY;
                riverValleyCount += 1;
                cell.TerrainTypeIndex = 1;
            }
       

            if (
                minNeighborElevation >= cell.Elevation &&
                Random.value < extraLakeProbability
            ) {
                cell.WaterLevel = cell.Elevation;
                cell.Elevation -= 1;
            }

            cell = cell.GetNeighbor(direction);
        }
        return riverValleyCount;
    }
    int RaiseEmpireTerrain(int chunkSize, int budget, MapRegion region) {

        searchFrontierPhase += 1;
        HexCell firstCell = dryLand[Random.Range(0, dryLand.Count)];
        firstCell.SearchPhase = searchFrontierPhase;
        firstCell.Distance = 0;
        firstCell.SearchHeuristic = 0;
        searchFrontier.Enqueue(firstCell);

        HexCoordinates center = firstCell.coordinates;


        int size = 0;
        while (size < chunkSize && searchFrontier.Count > 0) {
            HexCell current = searchFrontier.Dequeue();
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
                HexCell neighbor = current.GetNeighbor(d);
                if (neighbor && neighbor.SearchPhase < searchFrontierPhase && neighbor.IsUnderwater) {
                    neighbor.SearchPhase = searchFrontierPhase;
                    //Controls randomness weighting from center here by manipulating the distance
                    //criterion
                    neighbor.Distance = neighbor.coordinates.DistanceTo(center);
                    neighbor.SearchHeuristic =
                                            Random.value < jitterProbability ? 1 : 0;
                    searchFrontier.Enqueue(neighbor);
                }
            }
            if (current.Elevation == waterLevel) {
                continue;
            }
 
            else if (current.Elevation < waterLevel) {
                current.Elevation = waterLevel;

                dryLand.Add(current);
                current.TerrainTypeIndex = 1;
                landCells++;
                if(--budget == 0) {
                    break;
                }
                size += 1;

            } 
        }
        searchFrontier.Clear();
        return budget;


    }
    void SetEmpireTerrainType() {

        for (int i = 0; i < dryLand.Count; i++) {
            HexCell cell = dryLand[i];

            if (cell.IsUnderwater) {
                Debug.Log("Part of the continent is underwater!");
            }

            


        }
     }



    
    
}
