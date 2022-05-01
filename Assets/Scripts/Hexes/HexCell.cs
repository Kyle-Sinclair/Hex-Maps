using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class HexCell : MonoBehaviour{
    public HexCoordinates coordinates;
    public int Index { get; set; }

    public int SearchPhase { get; set; }
    public HexUnit Unit { get; set; }

    public HexCellShaderData ShaderData { get; set; }
    //Visibility methods
    public bool IsVisible
    {
        get {
            return visibility > 0 && Explorable;
        }
    }
    public bool isCapital = false;
    int visibility;
    public bool Explorable { get; set; }
    public bool IsExplored
    {
        get {
            return explored && Explorable;
        }
        private set {
            explored = value;
        }
    }
	bool explored;
    public void IncreaseVisibility() {
        visibility += 1;
        if(visibility == 1) {
            IsExplored = true;
            ShaderData.RefreshVisiblity(this);
        }
    }
    public void DecreaseVisibility() {
        visibility -= 1;
        if(visibility == 0) {
            ShaderData.RefreshVisiblity(this);
        }
    }
    public void ResetVisibility() {
        if (visibility > 0) {
            visibility = 0;
            ShaderData.RefreshVisiblity(this);
        }
    }

    public int Distance
    {
        get {
            return distance;
        }
        set {
            distance = value;
        }
    }
    int distance;
    public HexGridChunk chunk;
   
    public TileSet tileSet;
    public int TerrainTypeIndex{
        get {
            return terrainTypeIndex;
        }
        set {
            if(terrainTypeIndex != value) {
                terrainTypeIndex = value;

                ShaderData.RefreshTerrain(this);
            }
        }

    }
    int terrainTypeIndex = 0;
    public RectTransform uiRect;
    public int Elevation
    {
        get {
            return elevation;
        }
        set {
            if (elevation == value) {
                return;
            }
            int originalViewElevation = ViewElevation;
            elevation = value;
            if (ViewElevation != originalViewElevation) {
                ShaderData.ViewElevationChanged();
            }
            RefreshPosition();
            ValidateRivers();

            for (int i = 0; i < roads.Length; i++) {
                if (roads[i] && GetElevationDifference((HexDirection)i) > 1) {
                    SetRoad(i, false);
                }
            }
            Refresh();

        }
    }
    public bool Walled {
        get {
            return walled;
        }
        set {
            if(walled != value) {
                walled = value;
                Refresh();
            }
        }

    }
    bool walled;
    public int ViewElevation
    {
        get {
            return elevation >= waterLevel ? elevation : waterLevel;
        }
    }
    public int GetElevationDifference(HexDirection direction) {
        int difference = elevation - GetNeighbor(direction).elevation;
        return difference >= 0 ? difference : -difference;
    }
    public float StreamBedY {
        get {
            return (elevation + HexMetrics.streamBedElevationOffset) *
                    HexMetrics.elevationStep;
        }

    }
    public float RiverSurfaceY {
        get {
            return
                (elevation + HexMetrics.waterElevationOffset) *
                HexMetrics.elevationStep;
        }
    }
    public float WaterSurfaceY {
        get {
            return
                (waterLevel + HexMetrics.waterElevationOffset) *
                HexMetrics.elevationStep;
        }
    }
    public int WaterLevel{
        get {
            return waterLevel;
        }
        set {
            if (waterLevel == value) {
                return;
            }
            int originalViewElevation = ViewElevation;
            waterLevel = value;
            if (ViewElevation != originalViewElevation) {
                ShaderData.ViewElevationChanged();
            }
            ValidateRivers();

            Refresh();
        }
    }
    public int UrbanLevel
    {
        get {
            return urbanLevel;
        }
        set {
            if (urbanLevel != value) {
                urbanLevel = value;
                RefreshSelfOnly();
            }
        }
    }
    int urbanLevel;

    public int FarmLevel
    {
        get {
            return farmLevel;
        }
        set {
            if (farmLevel != value) {
                farmLevel = value;
                RefreshSelfOnly();
            }
        }
    }
    int farmLevel;
    public int PlantLevel
    {
        get {
            return plantLevel;
        }
        set {
            if (plantLevel != value) {
                plantLevel = value;
                RefreshSelfOnly();
            }
        }
    }
    int plantLevel;
    public int SpecialIndex {
        get {
            return specialIndex;
        }
        set {
            if (specialIndex != value && !HasRiver) {
                specialIndex = value;
                RemoveRoads();
                RefreshSelfOnly();
            }
        }
    }
    int specialIndex;
    public bool IsSpecial
    {
        get {
            return specialIndex > 0;
        }
    }
    void ValidateRivers() {
        if (
            hasOutgoingRiver &&
            !IsValidRiverDestination(GetNeighbor(outgoingRiver))
        ) {
            RemoveOutgoingRiver();
        }
        if (
            hasIncomingRiver &&
            !GetNeighbor(incomingRiver).IsValidRiverDestination(this)
        ) {
            RemoveIncomingRiver();
        }
    }
    int waterLevel = 0;
    public bool IsUnderwater
    {
        get {
            return waterLevel > elevation;
        }
    }
    bool IsValidRiverDestination(HexCell neighbor) {
        return neighbor && (
            elevation >= neighbor.elevation || waterLevel == neighbor.elevation
        );
    }
    public Vector3 Position{
        get {
            return transform.localPosition;
        }

    }
    int elevation = 0;
    [SerializeField]
    HexCell[] neighbors;
    bool hasIncomingRiver, hasOutgoingRiver;
    HexDirection incomingRiver, outgoingRiver;
    public HexDirection RiverBeginOrEndDirection
    {
        get {
            return hasIncomingRiver ? incomingRiver : outgoingRiver;
        }
    }
    [SerializeField]
    bool[] roads;
    
    
    public HexCell PathFrom { get; set; }

    
    public void SetLabel(string text) {
        UnityEngine.UI.Text label = uiRect.GetComponent<Text>();
        label.text = text;
    }
    void Refresh() {
        if (chunk) {
            chunk.Refresh();
            for (int i = 0; i < neighbors.Length; i++) {
                HexCell neighbor = neighbors[i];
                if (neighbor != null && neighbor.chunk != chunk) {
                    neighbor.chunk.Refresh();
                }
            }
            if (Unit) {
                Unit.ValidateLocation();
            }
        }
    }
    void RefreshSelfOnly() {
         chunk.Refresh();
        if (Unit) {
            Unit.ValidateLocation();
        }
    }
    void RefreshPosition() {
        Vector3 position = transform.localPosition;
        position.y = elevation * HexMetrics.elevationStep;
        position.y += (HexMetrics.SampleNoise(position).y * 2f - 1f) *
                      HexMetrics.elevationPerturbStrength;
        transform.localPosition = position;
        Vector3 uiPosition = uiRect.localPosition;
        uiPosition.z = -position.y;
        uiRect.localPosition = uiPosition;
    }
   
    

    public int SearchHeuristic { get; set; }
    public int SearchPriority
    {
        get {
            return distance + SearchHeuristic;
        }
    }
    public HexCell NextWithSamePriority { get; set; }

    public void DisableHighlight() {
        Image highlight = uiRect.GetChild(0).GetComponent<Image>();
        highlight.enabled = false;
    }
    public void EnableHighlight() {
        Image highlight = uiRect.GetChild(0).GetComponent<Image>();
        highlight.enabled = true;
    }
    public void EnableHighlight(Color color) {
        Image highlight = uiRect.GetChild(0).GetComponent<Image>();
        highlight.color = color;
        highlight.enabled = true;
    }


    public void SetNeighbor(HexDirection direction, HexCell cell) {
        neighbors[(int)direction] = cell;
        cell.neighbors[(int)direction.Opposite()] = this;
    }
    public HexCell GetNeighbor(HexDirection direction) {
        return neighbors[(int)direction];
    }
    public HexEdgeType GetEdgeType(HexCell otherCell) {
        return HexMetrics.GetEdgeType(
            elevation, otherCell.elevation
        );
    }
    public HexEdgeType GetEdgeType(HexDirection direction) {
        return HexMetrics.GetEdgeType(
            elevation, neighbors[(int)direction].elevation
        );
    }


   
    public bool HasRoads
    {
        get {
            for (int i = 0; i < roads.Length; i++) {
                if (roads[i]) {
                    return true;
                }
            }
            return false;
        }
    }
    public bool HasRoadThroughEdge(HexDirection direction) {
        return roads[(int)direction];
    }

    public void RemoveRoads() {
        for (int i = 0; i < roads.Length; i++) {
            if (roads[i]) {
                SetRoad(i, false);
            }
        }
    }
    public void AddRoad(HexDirection direction) {
        Debug.Log("Adding road to direction " + (int)direction);
        if (!roads[(int)direction] && !HasRiverThroughEdge(direction) && GetElevationDifference(direction) <= 1) {
            SetRoad((int)direction, true);
        }
    }
    void SetRoad(int index, bool state) {
        roads[index] = state;
        neighbors[index].roads[(int)((HexDirection)index).Opposite()] = state;
        neighbors[index].RefreshSelfOnly();
        RefreshSelfOnly();
    }


    public bool HasIncomingRiver
    {
        get {
            return hasIncomingRiver;
        }
    }
    public bool HasOutgoingRiver
    {
        get {
            return hasOutgoingRiver;
        }
    }
    public HexDirection IncomingRiver
    {
        get {
            return incomingRiver;
        }
    }
    public HexDirection OutgoingRiver
    {
        get {
            return outgoingRiver;
        }
    }
    public bool HasRiverThroughEdge(HexDirection direction) {
        return
            hasIncomingRiver && incomingRiver == direction ||
            hasOutgoingRiver && outgoingRiver == direction;
    }
    public bool HasRiverBeginOrEnd
    {
        get {
            return hasIncomingRiver != hasOutgoingRiver;
        }
    }
    public bool HasRiver
    {
        get {
            return hasIncomingRiver || hasOutgoingRiver;
        }

    }
    public void RemoveOutgoingRiver() {
        if (!hasOutgoingRiver) {
            return;
        }
        hasOutgoingRiver = false;
        RefreshSelfOnly();
        HexCell neighbour = GetNeighbor(outgoingRiver);
        neighbour.hasIncomingRiver = false;
        neighbour.RefreshSelfOnly();
    }
    public void RemoveIncomingRiver() {
        if (!hasIncomingRiver) {
            return;
        }
        hasIncomingRiver = false;
        RefreshSelfOnly();
        HexCell neighbour = GetNeighbor(incomingRiver);
        neighbour.hasOutgoingRiver = false;
        neighbour.RefreshSelfOnly();
    }
    public void RemoveRiver() {
        RemoveOutgoingRiver();
        RemoveIncomingRiver();
    }
    public void SetOutgoingRiver(HexDirection direction) {
        if (hasOutgoingRiver && outgoingRiver == direction) {
            return;
        }

        HexCell neighbor = GetNeighbor(direction);
        if (!IsValidRiverDestination(neighbor)) {
            return;
        }

        RemoveOutgoingRiver();
        if (hasIncomingRiver && incomingRiver == direction) {
            RemoveIncomingRiver();
        }
        hasOutgoingRiver = true;
        outgoingRiver = direction;
        //		RefreshSelfOnly();
        specialIndex = 0;

        neighbor.RemoveIncomingRiver();
        neighbor.hasIncomingRiver = true;
        neighbor.incomingRiver = direction.Opposite();
        neighbor.specialIndex = 0;

        //		neighbor.RefreshSelfOnly();

        SetRoad((int)direction, false);
    }

    public void SetMapData(float data) {
        ShaderData.SetMapData(this, data);
    }
    public int BordersSea() {
        int UnderwaterNeighborCount = 0;
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
            HexCell neighbor = GetNeighbor(d);
            if(neighbor == null) {
                continue;
            }
            if ( neighbor.IsUnderwater) {
                UnderwaterNeighborCount++;
            }

        }
        switch (UnderwaterNeighborCount) {
            case 0: return 0;
            case 1:
            case 2: return UnderwaterNeighborCount * 10;
            case 3: return UnderwaterNeighborCount * 15;
            case 4:
            case 5: return UnderwaterNeighborCount * 20;
            default: Debug.Log("Hexcell " + coordinates +
                                " has an innappproriate number of neighboring underwater tiles");
                     return 0;
        }
    }
    public void Save(BinaryWriter writer) {
        writer.Write((byte)terrainTypeIndex);
        writer.Write((byte)(elevation + 127));
        writer.Write((byte)waterLevel);
        writer.Write((byte)urbanLevel);
        writer.Write((byte)farmLevel);
        writer.Write((byte)plantLevel);
        writer.Write((byte)specialIndex);
        writer.Write(walled);

        if (hasIncomingRiver) {
            writer.Write((byte)(incomingRiver + 128));
        }
        else {
            writer.Write((byte)0);
        }

        if (hasOutgoingRiver) {
            writer.Write((byte)(outgoingRiver + 128));
        }
        else {
            writer.Write((byte)0);
        }
        int roadFlags = 0;
        for (int i = 0; i < roads.Length; i++) {
            //			writer.Write(roads[i]);
            if (roads[i]) {
                roadFlags |= 1 << i;
            }
        }
        writer.Write((byte)roadFlags);
        writer.Write(IsExplored);

    }
    public void Load(BinaryReader reader, int header) {
        terrainTypeIndex = reader.ReadByte();
        ShaderData.RefreshTerrain(this);

        elevation = reader.ReadByte();
        if (header >= 4) {
            elevation -= 127;
        }
        RefreshPosition();
        waterLevel = reader.ReadByte();
        urbanLevel = reader.ReadByte();
        farmLevel = reader.ReadByte();
        plantLevel = reader.ReadByte();
        specialIndex = reader.ReadByte();
        walled = reader.ReadBoolean();

        byte riverData = reader.ReadByte();
        if (riverData >= 128) {
            hasIncomingRiver = true;
            incomingRiver = (HexDirection)(riverData - 128);
        }
        else {
            hasIncomingRiver = false;
        }

       riverData = reader.ReadByte();
        if (riverData >= 128) {
            hasOutgoingRiver = true;
            outgoingRiver = (HexDirection)(riverData - 128);
        }
        else {
            hasOutgoingRiver = false;
        }

        int roadFlags = reader.ReadByte();
        for (int i = 0; i < roads.Length; i++) {
            roads[i] = (roadFlags & (1 << i)) != 0;
        }
        IsExplored = header >= 3 ? reader.ReadBoolean() : false;
        ShaderData.RefreshVisiblity(this);
    }
}
