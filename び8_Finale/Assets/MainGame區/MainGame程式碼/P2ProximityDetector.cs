using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class P2ProximityDetector : MonoBehaviour
{
    [HideInInspector] public GameObject nearItem;

    private void OnTriggerEnter(Collider other)
    {
        // 避免檢測到自己或隊友
        if (other.transform.root == transform.root) return;

        // 只偵測地上物品
        if (other.CompareTag("SeaFood") || other.CompareTag("Desert"))
        {
            nearItem = other.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (other.gameObject == nearItem)
        {
            nearItem = null;
        }
    }
}
