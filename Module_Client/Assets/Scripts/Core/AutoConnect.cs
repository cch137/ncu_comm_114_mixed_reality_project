using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.MixedReality.WebRTC.Unity;

public class AutoConnect : MonoBehaviour
{
    [SerializeField] GameObject nodeDssSignalerControlsSend;
    NodeDssSignalerUI nodeUI;
    private float waitForConnectSec = 0f;

    private void Start()
    {
        StartCoroutine(tryConnect());
    }
    IEnumerator tryConnect()
    {
        yield return new WaitForSeconds(waitForConnectSec);
        // Try to get the component safely
        nodeUI = nodeDssSignalerControlsSend != null ? nodeDssSignalerControlsSend.GetComponent<NodeDssSignalerUI>() : null;
        if (nodeUI == null)
        {
            Debug.LogWarning("AutoConnect: NodeDssSignalerUI component not found on the provided GameObject. Aborting connection attempt.");
            yield break;
        }

        // Try calling StartConnection but guard against uninitialized peer by catching exceptions and retrying a few times
        const int maxAttempts = 5;
        int attempt = 0;
        while (attempt < maxAttempts)
        {
            try
            {
                nodeUI.StartConnection();
                yield break; // success
            }
            catch (InvalidOperationException ex)
            {
                attempt++;
                Debug.LogWarning($"AutoConnect: StartConnection attempt {attempt} failed: {ex.Message}. Retrying in 1s...");
            }
            // yield must be outside catch/finally blocks
            yield return new WaitForSeconds(1f);
        }
        Debug.LogError("AutoConnect: Failed to start connection after multiple attempts.");
    }
}