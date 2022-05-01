using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
public class HexGrid : MonoBehaviour
{
    public int cellCountX = 20;
    public int cellCountZ = 25;
    int chunkCountX, chunkCountZ;
    Transform[] columns;
    int currentCenterColumnIndex = -1;

    //private int fixedSpeed = 10;
    public HexCell cellPrefab;
    public HexGridChunk chunkPrefab;
    int searchFrontierPhase;
    HexCell currentPathFrom, currentPathTo;
    bool currentPathExists;
    public bool HasPath
    {
        get {
            return currentPathExists;
        }
    }
    HexCell[] cells;
    public List<HexCell> dryLand = new List<HexCell>();

    List<HexUnit> units = new List<HexUnit>();

    HexGridChunk[] chunks;
    public Texture2D noiseSource;
    public HexUnit unitPrefab;
    public HexCellShaderData cellShaderData;
    public int seed;
    HexCellPriorityQueue searchFrontier;

    public Text cellLabelPrefab;
    public Color[] colors;
    public Color touchedColor = Color.magenta;

    public HexGameUI GameUI;
    public int numPlayers;

    void Awake() {
        HexMetrics.noiseSource = noiseSource;
        HexMetrics.InitializeHashGrid(seed);
        HexMetrics.colors = colors;
        HexUnit.unitPrefab = unitPrefab;
        cellShaderData = gameObject.AddComponent<HexCellShaderData>();
        cellShaderData.Grid = this;
        CreateMap(cellCountX, cellCountZ, 2);
    }
    void Start() {
        HexMetrics.InitializeHashGrid(seed);
    }
    void OnEnable() {
        if (!HexMetrics.noiseSource) {
            HexMetrics.noiseSource = noiseSource;
            HexMetrics.InitializeHashGrid(seed);
            HexUnit.unitPrefab = unitPrefab;
            HexMetrics.wrapSize = 0;

            ResetVisibility();
        }
        HexMetrics.colors = colors;

    }



    


    public bool CreateMap(int x, int z, int numberOfPlayers) {

        if (x <= 0 || x % HexMetrics.chunkSizeX != 0 ||
            z <= 0 || z % HexMetrics.chunkSizeZ != 0
        ) {
            Debug.LogError("Unsupported map size.");
            return false;
        }
        ClearPath();
        ClearUnits();
        if (columns != null) {
            for (int i = 0; i < columns.Length; i++) {
                Destroy(columns[i].gameObject);
            }
        }
        cellCountX = x;
        cellCountZ = z;
        currentCenterColumnIndex = -1;

        HexMetrics.wrapSize = 0;

        chunkCountX = cellCountX / HexMetrics.chunkSizeX;
        chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;
        cellShaderData.Initialize(cellCountX, cellCountZ);

        CreateChunks();
        CreateCells();
        //GameUI.SpawnPlayers(numberOfPlayers);

        GameUI.enabled = false;
        return true;
    }
    void CreateCells() {
        cells = new HexCell[cellCountZ * cellCountX];

        for (int z = 0, i = 0; z < cellCountZ; z++) {
            for (int x = 0; x < cellCountX; x++) {
                CreateCell(x, z, i++);
            }

        }
    }
    void CreateCell(int x, int z, int i) {
        Vector3 position;
        position.x = (x + z * 0.5f - z / 2) * (HexMetrics.innerDiameter);
        position.y = 0f;
        position.z = z * (HexMetrics.outerRadius * 1.5f);

        HexCell cell = cells[i] = Instantiate<HexCell>(cellPrefab);
        //cell.transform.SetParent(transform, false);
        cell.transform.localPosition = position;
        cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
        cell.Index = i;
        cell.tileSet = TileSet.UNALLOCATED;
        cell.ShaderData = cellShaderData;
        cell.Explorable =
            x > 0 && z > 0 && x < cellCountX - 1 && z < cellCountZ - 1;
        if (x > 0) {
            cell.SetNeighbor(HexDirection.W, cells[i - 1]);
        }
        if (z > 0) {
            if ((z & 1) == 0) {//the bitwise operator here returns eveness for us
                cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX]);

                if (x > 0) {
                    cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX - 1]);
                }
            }
            else {
                cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX]);
                if (x < cellCountX - 1) {
                    cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX + 1]);
                }
            }
            cell.Elevation = 0;
        }
        Text label = Instantiate<Text>(cellLabelPrefab);
        //label.rectTransform.SetParent(gridCanvas.transform, false);
        label.rectTransform.anchoredPosition =
            new Vector2(position.x, position.z);
        cell.uiRect = label.rectTransform;

        AddCellToChunk(x, z, cell);
    }
    void CreateChunks() {
        columns = new Transform[chunkCountX];
        for (int x = 0; x < chunkCountX; x++) {
            columns[x] = new GameObject("Column").transform;
            columns[x].SetParent(transform, false);
        }
        chunks = new HexGridChunk[chunkCountX * chunkCountZ];

        for (int z = 0, i = 0; z < chunkCountZ; z++) {
            for (int x = 0; x < chunkCountX; x++) {
                HexGridChunk chunk = chunks[i++] = Instantiate(chunkPrefab);
                chunk.transform.SetParent(columns[x], false);

            }

        }
    }
    void AddCellToChunk(int x, int z, HexCell cell) {
        int chunkX = x / HexMetrics.chunkSizeX;
        int chunkZ = z / HexMetrics.chunkSizeZ;
        HexGridChunk chunk = chunks[chunkX + chunkZ * chunkCountX];

        int localX = x - chunkX * HexMetrics.chunkSizeX;
        int localZ = z - chunkZ * HexMetrics.chunkSizeZ;
        chunk.AddCell(localX + localZ * HexMetrics.chunkSizeX, cell);

    }


    public void CenterMap(float xPosition) {
        int centerColumnIndex = (int)
            (xPosition / (HexMetrics.innerDiameter * HexMetrics.chunkSizeX));

        if (centerColumnIndex == currentCenterColumnIndex) {
            return;
        }
        currentCenterColumnIndex = centerColumnIndex;
        int minColumnIndex = centerColumnIndex - chunkCountX / 2;
        int maxColumnIndex = centerColumnIndex + chunkCountX / 2;

        Vector3 position;
        position.y = position.z = 0f;
        for (int i = 0; i < columns.Length; i++) {
            if (i < minColumnIndex) {
                position.x = chunkCountX *
                    (HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
            }
            else if (i > maxColumnIndex) {
                position.x = chunkCountX *
                    -(HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
            }
            else {
                position.x = 0f;
            }
            columns[i].localPosition = position;
        }
    }
    public HexCell GetCell(HexCoordinates coordinates) {
        int z = coordinates.Z;
        if (z < 0 || z >= cellCountZ) {
            return null;
        }
        int x = coordinates.X + z / 2;
        if (x < 0 || x >= cellCountX) {
            return null;
        }
        //Debug.Log("returning cell at index  " + (x + z * cellCountX));
        return cells[x + z * cellCountX];
    }
    public HexCell GetCell(Vector3 position) {
        position = transform.InverseTransformPoint(position);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);
        int index = coordinates.X + coordinates.Z * cellCountX + coordinates.Z / 2;
       // Debug.Log("returning cell with coordinates  " + cells[index].coordinates.ToString());
        return cells[index];
    }
    public HexCell GetCell(Ray ray) {
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit)) {
            return GetCell(hit.point);
        }
        return null;
    }
    public HexCell GetCell(int xOffset, int zOffset) {
        return cells[xOffset + zOffset * cellCountX];
    }
    public HexCell GetCenterCell() {
        return GetCell(cellCountX / 2, cellCountZ / 2);
    }
    public HexCell GetCell(int cellIndex) {
        return cells[cellIndex];
    }
    public void ShowUI(bool visible) {
        for (int i = 0; i < chunks.Length; i++) {
            chunks[i].ShowUI(visible);
        }
    }


    void ShowPath(int speed) {
        if (currentPathExists) {
            HexCell current = currentPathTo;
            while (current != currentPathFrom) {
                int turn = (current.Distance - 1)/ speed;
                current.SetLabel(turn.ToString());
                current.EnableHighlight(Color.white);
                current = current.PathFrom;
            }
        }
        currentPathFrom.EnableHighlight(Color.blue);
        currentPathTo.EnableHighlight(Color.red);
    }
    public List<HexCell> GetPath() {
        if (!currentPathExists) {
            return null;
        }
        List<HexCell> path = ListPool<HexCell>.Get();
        for (HexCell c = currentPathTo; c != currentPathFrom; c = c.PathFrom) {
            path.Add(c);
        }
        path.Add(currentPathFrom);
        path.Reverse();
        return path;
    }
    public void ClearPath() {
        if (currentPathExists) {
            HexCell current = currentPathTo;
            while (current != currentPathFrom) {
                current.SetLabel(null);
                current.DisableHighlight();
                current = current.PathFrom;
            }
            current.DisableHighlight();
            currentPathExists = false;
        }
        else if (currentPathFrom) {
            currentPathFrom.DisableHighlight();
            currentPathTo.DisableHighlight();
        }
        currentPathFrom = currentPathTo = null;
    }
    public void FindPath(HexCell fromCell, HexCell toCell, HexUnit unit) {
        //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        //sw.Start();
        ClearPath();
        currentPathFrom = fromCell;
        currentPathTo = toCell;
        currentPathExists = Search(fromCell, toCell, unit);
        //sw.Stop();
        ShowPath(unit.Speed);

        //Debug.Log(sw.ElapsedMilliseconds);

    }
    bool Search(HexCell fromCell, HexCell toCell, HexUnit unit) {
        int speed = unit.Speed;
        searchFrontierPhase += 2;

        if (searchFrontier == null) {
            searchFrontier = new HexCellPriorityQueue();
        }
        else {
            searchFrontier.Clear();
        }

        

        fromCell.Distance = 0;
        fromCell.SearchPhase = searchFrontierPhase;

        searchFrontier.Enqueue(fromCell);
        while (searchFrontier.Count > 0) {
            HexCell current = searchFrontier.Dequeue();
            current.SearchPhase += 1;

            if (current == toCell) {
                return true;
            }
            int currentTurn = (current.Distance - 1)/ speed;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
                HexCell neighbor = current.GetNeighbor(d);
                if (neighbor == null || neighbor.SearchPhase > searchFrontierPhase) {
                    continue;
                }
                if (!unit.IsValidDestination(neighbor)) {
                    continue;
                }
                int moveCost = unit.GetMoveCost(current, neighbor, d);
                if (moveCost < 0) {
                    continue;
                }
                int distance = current.Distance + moveCost;

                int turn = (distance - 1 )/ speed;
                if (turn > currentTurn) {
                    distance = turn * speed + moveCost;
                }
                if (neighbor.SearchPhase < searchFrontierPhase) {
                    neighbor.Distance = distance;
                    neighbor.PathFrom = current;
                    neighbor.SearchHeuristic =
                        neighbor.coordinates.DistanceTo(toCell.coordinates);
                    //Debug.Log("Trying to add a cell to priority queue with search priority; " + neighbor.SearchPriority);
                    neighbor.SearchPhase = searchFrontierPhase;

                    searchFrontier.Enqueue(neighbor);
                }
                else if (distance < neighbor.Distance) {
                    int oldPriority = neighbor.SearchPriority;

                    neighbor.Distance = distance;
                    neighbor.PathFrom = current;
                    searchFrontier.Change(neighbor, oldPriority);
                }
            }
        }
        return false;

    }


    //Visibility methods
    List<HexCell> GetVisibleCells(HexCell fromCell, int range) {
        List<HexCell> visibleCells = ListPool<HexCell>.Get();

        searchFrontierPhase += 2;
        if (searchFrontier == null) {
            searchFrontier = new HexCellPriorityQueue();
        }
        else {
            searchFrontier.Clear();
        }
        range += fromCell.ViewElevation;

        fromCell.SearchPhase = searchFrontierPhase;
        fromCell.Distance = 0;
        searchFrontier.Enqueue(fromCell);
        HexCoordinates fromCoordinates = fromCell.coordinates;

        while (searchFrontier.Count > 0) {
            HexCell current = searchFrontier.Dequeue();
            current.SearchPhase += 1;
            visibleCells.Add(current);
          
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
                HexCell neighbor = current.GetNeighbor(d);
                if (
                    neighbor == null ||
                    neighbor.SearchPhase > searchFrontierPhase ||
                    !neighbor.Explorable
                ) {
                    continue;
                }

                int distance = current.Distance + 1;
                if (distance + neighbor.ViewElevation > range ||
                    distance > fromCoordinates.DistanceTo(neighbor.coordinates)) {
                    continue;
                }
              
              

                if (neighbor.SearchPhase < searchFrontierPhase) {
                    neighbor.SearchPhase = searchFrontierPhase;
                    neighbor.Distance = distance;
                    neighbor.SearchHeuristic = 0;
                    searchFrontier.Enqueue(neighbor);
                }
                else if (distance < neighbor.Distance) {
                    int oldPriority = neighbor.SearchPriority;
                    neighbor.Distance = distance;
                    searchFrontier.Change(neighbor, oldPriority);
                }
            }
        }
        return visibleCells;
    }
    public void IncreaseVisibility(HexCell fromCell, int range) {
        List<HexCell> cells = GetVisibleCells(fromCell, range);
        for (int i = 0; i < cells.Count; i++) {
            cells[i].IncreaseVisibility();
        }
        ListPool<HexCell>.Add(cells);
    }
    public void DecreaseVisibility(HexCell fromCell, int range) {
        List<HexCell> cells = GetVisibleCells(fromCell, range);
        for (int i = 0; i < cells.Count; i++) {
            cells[i].DecreaseVisibility();
        }
        ListPool<HexCell>.Add(cells);
    }
    public void ResetVisibility() {
        for (int i = 0; i < cells.Length; i++) {
            cells[i].ResetVisibility();
        }
        for (int i = 0; i < units.Count; i++) {
            HexUnit unit = units[i];
            IncreaseVisibility(unit.Location, unit.VisionRange);
        }
    }


    public void AddUnit(HexUnit unit, HexCell location, float orientation) {
        units.Add(unit);
        unit.Grid = this;
        unit.transform.SetParent(transform, false);
        unit.Location = location;
        unit.Orientation = orientation;
    }
    public void RemoveUnit(HexUnit unit) {
        units.Remove(unit);
        unit.Die();
    }
    void ClearUnits() {
        for (int i = 0; i < units.Count; i++) {
            units[i].Die();
        }
        units.Clear();
    }

    public void Save(BinaryWriter writer) {
        writer.Write(cellCountX);
        writer.Write(cellCountZ);
        writer.Write(numPlayers);
        for (int i = 0; i < cells.Length; i++) {
            cells[i].Save(writer);
        }
        writer.Write(units.Count);
        for (int i = 0; i < units.Count; i++) {
            units[i].Save(writer);
        }
    }
    public void Load(BinaryReader reader, int header) {
        ClearPath();
        ClearUnits();

        int x = 20, z = 15;
        int numberOfPlayers = 2;
        if (header >= 1) {
            x = reader.ReadInt32();
            z = reader.ReadInt32();
        }
        if(header >= 5) {
            numberOfPlayers = reader.ReadInt32();
        }
        if (x != cellCountX || z != cellCountZ ) {
            if (!CreateMap(x, z, numberOfPlayers)) {
                return;
            }
        }
        bool originalImmediateMode = cellShaderData.ImmediateMode;
        cellShaderData.ImmediateMode = true;
        for (int i = 0; i < cells.Length; i++) {
            cells[i].Load(reader, header);
        }
        for (int i = 0; i < chunks.Length; i++) {
            chunks[i].Refresh();
        }
        if (header >= 2) {
            int unitCount = reader.ReadInt32();
            for (int i = 0; i < unitCount; i++) {
                HexUnit.Load(reader, this);
            }
        }
        cellShaderData.ImmediateMode = originalImmediateMode;

    }
}
