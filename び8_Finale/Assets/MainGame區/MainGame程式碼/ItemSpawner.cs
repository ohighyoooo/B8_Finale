using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour
{
    public static ItemSpawner Instance;

    [Header("Item Prefabs (Pool Seeds)")]
    public List<GameObject> itemPrefabs; // 預先配置好的地上物件 prefab

    // 物件池 (每個 prefab 會各實例化一份並重複使用)
    private List<GameObject> pooledItems = new List<GameObject>();

    [Header("Spawn Area")]
    // 物件生成圓心位置（XZ 平面上的中心點）
    // ※ 你原本用 (-29,1,0)；現在我們假設一樣的 XZ，只是 Y 改成 6.5 做掉落高度
    [SerializeField] private Vector3 centerXZ = new Vector3(0f, -1.5f, 0f);

    // 半徑現在改成 13
    [SerializeField] private float spawnRadius = 13f;

    // 生成時的高度 (物品一開始會被放在這個高度，然後靠重力掉下去)
    [SerializeField] private float spawnHeight = 6.5f;

    // 自動隱藏 / 重生機制參數
    private const float autoHideDelay = 10f;          // 物件出現後10秒沒撿就隱藏
    private const float respawnMinDelay = 10f;        // 隱藏後再過 10~20 秒隨機重生
    private const float respawnMaxDelay = 20f;

    private void Awake()
    {
        // 單例模式（保留你的做法：不 Destroy）
        if (Instance == null) Instance = this;
        else Instance = this;
    }

    private void Start()
    {
        // 初始化物件池：把所有 prefab 先各生一個
        foreach (var prefab in itemPrefabs)
        {
            GameObject item = Instantiate(prefab);
            item.SetActive(false);
            pooledItems.Add(item);
        }

        // 場上先生成 2 個
        SpawnRandomItem();
        SpawnRandomItem();

        // 啟動持續檢查流程：
        // 如果場上完全沒有物件，就等一個隨機時間再生一個
        StartCoroutine(RespawnCycle());
    }

    IEnumerator RespawnCycle()
    {
        while (true)
        {
            // 等到「場上沒有任何物品是啟用中」
            yield return new WaitUntil(() => !IsAnyItemActive());
            // 然後等一段隨機時間，再生新物件
            yield return new WaitForSeconds(Random.Range(respawnMinDelay, respawnMaxDelay));
            SpawnRandomItem();
        }
    }

    // 生成一個隨機物件（如果池裡有可用的）
    void SpawnRandomItem()
    {
        GameObject item = GetInactiveItem();
        if (item != null)
        {
            item.transform.position = GetRandomSpawnPosition(); // 會用半徑8.5 + 高度6.5
            item.SetActive(true);

            // 10 秒內沒撿就隱藏
            StartCoroutine(AutoHide(item, autoHideDelay));
        }
    }

    // 在場上啟用的那個物件，10 秒後如果還在就隱藏，並排定重生
    IEnumerator AutoHide(GameObject item, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (item != null && item.activeSelf)
        {
            item.SetActive(false);
            // 物件隱藏後，過 10~20 秒從天上再掉下來
            StartCoroutine(RespawnAfterDelay(item, Random.Range(respawnMinDelay, respawnMaxDelay)));
        }
    }

    // 指定一個已存在於池中的物件，隔一段時間後重生
    IEnumerator RespawnAfterDelay(GameObject item, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (item != null && !item.activeSelf)
        {
            item.transform.position = GetRandomSpawnPosition();
            item.SetActive(true);

            // 重生後一樣啟動10秒自動隱藏邏輯
            StartCoroutine(AutoHide(item, autoHideDelay));
        }
    }

    // 場上是否至少有一個 item 還是啟用狀態
    bool IsAnyItemActive()
    {
        // 清掉不小心被 Destroy 的
        pooledItems.RemoveAll(item => item == null);

        foreach (var item in pooledItems)
        {
            if (item != null && item.activeSelf)
                return true;
        }
        return false;
    }

    // 從池裡找一個沒啟用的
    GameObject GetInactiveItem()
    {
        for (int i = 0; i < pooledItems.Count; i++)
        {
            GameObject item = pooledItems[i];
            if (item != null && !item.activeSelf)
            {
                return item;
            }
        }
        return null;
    }

    // 隨機一個位置：XZ 在半徑8.5的圓裡，Y = spawnHeight (6.5)
    Vector3 GetRandomSpawnPosition()
    {
        // Random.insideUnitCircle gives a point in a radius-1 circle on X/Y.
        Vector2 rand = Random.insideUnitCircle * spawnRadius;

        // 我們的世界座標：
        // X = centerXZ.x + rand.x
        // Z = centerXZ.z + rand.y
        // Y = spawnHeight (從天上掉下)
        return new Vector3(
            centerXZ.x + rand.x,
            spawnHeight,
            centerXZ.z + rand.y
        );
    }

    // 由玩家掉裝備時呼叫
    // 物件會出現在「距離中心最近的合法位置」，同樣從天上掉
    public void DropItem(GameObject prefab, Vector3 fromPosition)
    {
        GameObject item = GetInactiveItem();
        if (item != null)
        {
            item.transform.position = FindClosestDropPosition(fromPosition);
            item.SetActive(true);

            // 一樣 10 秒後沒撿就隱藏
            StartCoroutine(AutoHide(item, autoHideDelay));
        }
    }

    // 掉落時，用fromPosition決定方向，但仍限制在半徑8.5的邊界內
    // 並且讓 Y 一樣是 spawnHeight，確保「從天上掉下來」
    Vector3 FindClosestDropPosition(Vector3 origin)
    {
        // 向量：玩家位置相對於圓心
        Vector2 dir = new Vector2(origin.x - centerXZ.x, origin.z - centerXZ.z);

        // ClampMagnitude：如果玩家在圓外，壓到圓邊
        dir = Vector2.ClampMagnitude(dir, spawnRadius);

        return new Vector3(
            centerXZ.x + dir.x,
            spawnHeight,
            centerXZ.z + dir.y
        );
    }

    // 把場上物件「撿走」時呼叫：隱藏並安排重生
    public void HideItem(GameObject item)
    {
        if (item != null && item.activeSelf)
        {
            item.SetActive(false);
            StartCoroutine(RespawnAfterDelay(item, Random.Range(respawnMinDelay, respawnMaxDelay)));
        }
    }
}
