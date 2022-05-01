using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class BannerMenu : MonoBehaviour {
    public TextAsset forceList;
    private string theWholeFileAsOneLongString;
    private List<string> eachLine;

    public BannerItem itemPrefab;

    void Awake() {
        PopulateList();
    }

    void OnEnable() {
        PopulateList();
    }

    void OnValidate() {
        PopulateList();
    }

    private void PopulateList() {
        theWholeFileAsOneLongString = forceList.text;

        eachLine = new List<string>();
        eachLine.AddRange(
                    theWholeFileAsOneLongString.Split("\n"[0]));


       
        for (int i = 0; i < eachLine.Count; i++) {
            BannerItem item = Instantiate(itemPrefab);
            item.menu = this;
            item.UnitName = "Example name";
        }


    }
    public void SelectItem(string name) {

    }
}
