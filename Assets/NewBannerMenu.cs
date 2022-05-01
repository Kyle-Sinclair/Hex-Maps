using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

public class NewBannerMenu : MonoBehaviour {
    public Text menuLabel, actionButtonLabel;

    bool newBanner;


    public void Open(bool newBanner) {
        this.newBanner = newBanner;
        if (this.newBanner) {
            menuLabel.text = "Create New Banner";
            actionButtonLabel.text = "Create";
        }
        else {
            menuLabel.text = "Replenish";
            actionButtonLabel.text = "Replenish";
        }
        gameObject.SetActive(true);
        HexMapCamera.Locked = true;

    }
}
