using UnityEngine;
using UnityEngine.UI;

public class BannerItem : MonoBehaviour
{
    public BannerMenu menu;


    public string UnitName
    {
        get {
            return unitName;
        }
        set {
            unitName = value;
            transform.GetChild(0).GetComponent<Text>().text = value;
        }
    }

    string unitName;

    public void Select() {
        menu.SelectItem(unitName);
    }
}
