using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.MixedReality.WebRTC.Unity;

public class ConnectClick : MonoBehaviour
{
    // Start is called before the first frame update
    string ip;
    public NodeDssSignaler NodeDssSignaler;
    public NodeDssSignaler NodeDssSignaler1;

    public void EnterServerIP(Text enterText)
    {
        if (!string.IsNullOrEmpty(enterText.text))
        {
            ip = "http://" + enterText.text + ":3000/";

            NodeDssSignaler.HttpServerAddress = ip;
            NodeDssSignaler1.HttpServerAddress = ip;

        }




    }
}
