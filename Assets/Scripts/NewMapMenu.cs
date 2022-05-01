using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewMapMenu : MonoBehaviour{

    public HexGrid hexGrid;
    public HexMapGenerator mapGenerator;

    bool generateMaps = true;
    bool wrapping = true;

    public void ToggleMapGeneration(bool toggle) {
        generateMaps = toggle;
    }
    public void ToggleWrapping(bool toggle) {
        wrapping = toggle;
    }
    public void Open() {
        gameObject.SetActive(true);
        HexMapCamera.Locked = true;

    }

    public void Close() {
        gameObject.SetActive(false);
        HexMapCamera.Locked = false;

    }
    public void CreateMap(int x, int z, int numberOfPlayers) {
        if (generateMaps) {
            mapGenerator.GenerateMap(x, z, numberOfPlayers);
        }
        else {
            hexGrid.CreateMap(x, z, numberOfPlayers);
        }
        HexMapCamera.ValidatePosition();

        Close();
    }
    public void CreateSmallMap() {
        CreateMap(20, 15, 2);
    }

    public void CreateMediumMap() {
        CreateMap(40, 30, 3);
    }

    public void CreateLargeMap() {
        CreateMap(80, 60, 4);
    }
    public void CreateMightyEmpiresMap() {
        mapGenerator.GenerateMightyEmpiresMap(20,20, 4);
        Close();
    }

}
