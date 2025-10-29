using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class RagdollP2Controller : MonoBehaviour
{
    [Header("Movement Settings")]
    public Rigidbody bodyRoot;             // 主身體剛體(軀幹/胸腹那塊)
    public float moveForce = 200f;         // 推力大小
    public float turnSpeed = 6f;           // 面向旋轉速度
    public float maxSpeed = 8f;            // 最大水平速度

    [Header("Equipment")]
    public GameObject seaHeadEquip, seaBodyEquip, seaWeaponEquip;
    public GameObject desertHeadEquip, desertBodyEquip, desertWeaponEquip;
    public GameObject seaHeadPrefab, seaBodyPrefab, seaWeaponPrefab;
    public GameObject desertHeadPrefab, desertBodyPrefab, desertWeaponPrefab;

    [Header("Proximity Detector")]
    public P2ProximityDetector proximity;  // P2 自己的近物偵測器（跟 P1 版本類似）

    [Header("Combat Settings")]
    public float punchForce = 300f;        // 右手往前甩的力道（近戰視覺）
    public float punchRange = 2f;          // 近戰打擊距離（角色往前）
    public float punchRadius = 1f;         // 近戰打擊半徑
    public float knockbackForce = 500f;    // 擊退力
    public float slowMultiplierOnHit = 0.5f;   // 被打到的減速倍率（50%）
    public float slowDurationOnHit = 1.5f;     // 減速多久

    public float selfAttackLockTime = 0.5f;    // 自己攻擊後的硬直時間（攻擊者不能馬上連打 & 暫時不能移動）

    public Rigidbody rightArm;             // 右手剛體（揮拳用）
    public Transform firePoint;            // 若配 Desert 武器時，子彈發射點

    // 狀態
    private bool isAttacking = false;      // 正在做 PunchRoutine()
    private bool canControl = true;        // 可否讀輸入（攻擊硬直時會false）

    // 自己吃到減速的狀態
    private float speedFactor = 1f;
    private Coroutine slowCR;

    void Start()
    {
        if (proximity == null)
            proximity = GetComponentInChildren<P2ProximityDetector>();
    }

    void Update()
    {
        HandleMovementInput();
        HandleEquipInput();
        HandleAttackInput();
    }

    // ============================
    //       Movement
    // ============================
    Vector3 inputDir;

    void HandleMovementInput()
    {
        if (!canControl)
        {
            inputDir = Vector3.zero;
            return;
        }

        float h = 0f, v = 0f;

        // P2 用方向鍵控制
        if (Input.GetKey(KeyCode.LeftArrow)) h = -1;
        if (Input.GetKey(KeyCode.RightArrow)) h = 1;
        if (Input.GetKey(KeyCode.UpArrow)) v = 1;
        if (Input.GetKey(KeyCode.DownArrow)) v = -1;

        inputDir = new Vector3(h, 0f, v).normalized;
    }

    void FixedUpdate()
    {
        if (bodyRoot == null) return;

        if (inputDir.sqrMagnitude > 0.01f)
        {
            // 推動剛體 → ragdoll式移動
            Vector3 horizVel = new Vector3(bodyRoot.velocity.x, 0, bodyRoot.velocity.z);
            if (horizVel.magnitude < maxSpeed * speedFactor)
            {
                bodyRoot.AddForce(inputDir * moveForce, ForceMode.Acceleration);
            }

            // 角色身體朝移動方向慢慢轉過去
            Quaternion targetRot = Quaternion.LookRotation(inputDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }
    }

    // ============================
    //        Attack
    // ============================
    void HandleAttackInput()
    {
        if (!canControl) return;     // 攻擊硬直時不能輸入
        if (isAttacking) return;     // 正在PunchRoutine中
        if (!Input.GetKeyDown(KeyCode.RightShift)) return;

        // Desert 武器 => 射擊
        if (desertWeaponEquip != null && desertWeaponEquip.activeSelf)
        {
            Shoot();
            StartCoroutine(AttackLockout(selfAttackLockTime));
        }
        else
        {
            // 沒 Desert 武器 => 近戰
            StartCoroutine(PunchRoutine());
            StartCoroutine(AttackLockout(selfAttackLockTime));
        }
    }

    // 遠程射擊: 從 firePoint 發射子彈
    void Shoot()
    {
        if (firePoint == null) return;
        if (BulletPool.Instance == null) return;

        GameObject bulletObj = BulletPool.Instance.GetBullet();
        bulletObj.transform.position = firePoint.position;
        bulletObj.transform.rotation = Quaternion.LookRotation(transform.forward);
        bulletObj.SetActive(true);

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.shooter = this.gameObject;
            bullet.Fire(transform.forward); // Bullet 會自己處理拋物線＆落地成水灘
        }

        // 可以在這裡觸發射擊動畫/音效
    }

    // 近戰攻擊：右手往前甩＆檢測打到對手
    IEnumerator PunchRoutine()
    {
        isAttacking = true;

        // 1. 手往前甩 (視覺 + 物理)
        if (rightArm != null)
        {
            rightArm.AddForce(transform.forward * punchForce, ForceMode.Impulse);
        }

        // 2. 檢測正前方是否有玩家1
        DoMeleeHitCheck();

        // 3. 簡單冷卻
        yield return new WaitForSeconds(0.4f);

        isAttacking = false;
    }

    // 攻擊後的硬直：短時間不能動 / 不能再次攻擊或撿裝
    IEnumerator AttackLockout(float lockTime)
    {
        canControl = false;
        inputDir = Vector3.zero; // 防止上一次輸入殘留推力
        yield return new WaitForSeconds(lockTime);
        canControl = true;
    }

    // 在玩家面前畫一個小球去找對手，打到就擊退+減速
    void DoMeleeHitCheck()
    {
        Vector3 center = transform.position + transform.forward * punchRange;

        Collider[] hits = Physics.OverlapSphere(center, punchRadius);
        foreach (Collider hit in hits)
        {
            // 忽略自己
            if (hit.transform.root == this.transform.root)
                continue;

            // 嘗試抓 P1
            RagdollP1Controller enemy = hit.transform.root.GetComponent<RagdollP1Controller>();
            if (enemy != null)
            {
                enemy.TakeHitFrom(
                    sourcePos: transform.position,
                    force: knockbackForce,
                    slowMul: slowMultiplierOnHit,
                    slowTime: slowDurationOnHit
                );
                break; // 打到一個就結束
            }
        }

        // 除錯可視化時可以 Debug.DrawLine(transform.position, center, Color.blue, 0.2f);
    }

    // ============================
    //       Equipment Pickup
    // ============================
    void HandleEquipInput()
    {
        if (!canControl) return; // 被硬直時不允許撿裝

        if (proximity == null || proximity.nearItem == null) return;
        GameObject item = proximity.nearItem;

        // P2 用數字鍵盤 (Keypad1/2/3)
        if (item.CompareTag("SeaFood"))
        {
            if (Input.GetKeyDown(KeyCode.Keypad1)) EquipItem(seaHeadEquip, seaHeadPrefab, item);
            if (Input.GetKeyDown(KeyCode.Keypad2)) EquipItem(seaBodyEquip, seaBodyPrefab, item);
            if (Input.GetKeyDown(KeyCode.Keypad3)) EquipItem(seaWeaponEquip, seaWeaponPrefab, item);
        }
        else if (item.CompareTag("Desert"))
        {
            if (Input.GetKeyDown(KeyCode.Keypad1)) EquipItem(desertHeadEquip, desertHeadPrefab, item);
            if (Input.GetKeyDown(KeyCode.Keypad2)) EquipItem(desertBodyEquip, desertBodyPrefab, item);
            if (Input.GetKeyDown(KeyCode.Keypad3)) EquipItem(desertWeaponEquip, desertWeaponPrefab, item);
        }
    }

    void EquipItem(GameObject equipSlot, GameObject equipPrefab, GameObject groundItem)
    {
        // 關閉同部位的其他裝備
        if (equipSlot == seaHeadEquip || equipSlot == desertHeadEquip)
        {
            if (seaHeadEquip) seaHeadEquip.SetActive(false);
            if (desertHeadEquip) desertHeadEquip.SetActive(false);
        }
        else if (equipSlot == seaBodyEquip || equipSlot == desertBodyEquip)
        {
            if (seaBodyEquip) seaBodyEquip.SetActive(false);
            if (desertBodyEquip) desertBodyEquip.SetActive(false);
        }
        else if (equipSlot == seaWeaponEquip || equipSlot == desertWeaponEquip)
        {
            if (seaWeaponEquip) seaWeaponEquip.SetActive(false);
            if (desertWeaponEquip) desertWeaponEquip.SetActive(false);
        }

        // 啟用新裝
        if (equipPrefab) equipPrefab.SetActive(true);

        // 把地上的物件隱藏（不Destroy，交回物件池邏輯）
        if (ItemSpawner.Instance != null)
            ItemSpawner.Instance.HideItem(groundItem);

        proximity.nearItem = null;
    }

    // ============================
    //   被攻擊時：被對手呼叫
    // ============================
    public void TakeHitFrom(Vector3 sourcePos, float force, float slowMul, float slowTime)
    {
        // 擊退往外噴：AddForce Impulse
        if (bodyRoot != null)
        {
            Vector3 dir = (transform.position - sourcePos);
            dir.y = 0f;
            dir.Normalize();
            bodyRoot.AddForce(dir * force, ForceMode.Impulse);
        }

        // 也吃到緩速
        ApplySlow(slowMul, slowTime);
    }

    // ============================
    //   被子彈踩水灘等情況的緩速
    // ============================
    public void ApplySlow(float multiplier, float duration)
    {
        // 限制在0.1~1之間，避免負值或>1
        multiplier = Mathf.Clamp(multiplier, 0.1f, 1f);

        if (slowCR != null) StopCoroutine(slowCR);
        slowCR = StartCoroutine(SlowRoutine(multiplier, duration));
    }

    IEnumerator SlowRoutine(float mul, float dur)
    {
        speedFactor = mul;
        yield return new WaitForSeconds(dur);
        speedFactor = 1f;
        slowCR = null;
    }
}
