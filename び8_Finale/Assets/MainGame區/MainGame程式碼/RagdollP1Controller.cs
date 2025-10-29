using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class RagdollP1Controller : MonoBehaviour
{
    [Header("Movement Settings")]
    public Rigidbody bodyRoot;             // 主身體剛體(軀幹)
    public float moveForce = 200f;         // 推力大小
    public float turnSpeed = 6f;           // 面向旋轉速度
    public float maxSpeed = 8f;            // 最大水平速度

    [Header("Equipment")]
    public GameObject seaHeadEquip, seaBodyEquip, seaWeaponEquip;
    public GameObject desertHeadEquip, desertBodyEquip, desertWeaponEquip;
    public GameObject seaHeadPrefab, seaBodyPrefab, seaWeaponPrefab;
    public GameObject desertHeadPrefab, desertBodyPrefab, desertWeaponPrefab;

    [Header("Proximity Detector")]
    public P1ProximityDetector proximity;  // 用來知道腳邊有哪些可撿的物件

    [Header("Combat Settings")]
    public float punchForce = 300f;        // 右手往前甩的力道
    public float punchRange = 2f;          // 近戰可打到的距離 (從玩家往前)
    public float punchRadius = 1f;         // 近戰碰撞的半徑
    public float knockbackForce = 500f;    // 擊退對手的力道
    public float slowMultiplierOnHit = 0.5f;    // 被打後的移動速度倍率(50%)
    public float slowDurationOnHit = 1.5f;      // 減速多久(秒)

    public float selfAttackLockTime = 0.5f;     // 自己攻擊後的硬直秒數：這段時間不能移動/不能再次攻擊

    public Rigidbody rightArm;             // 右手剛體，揮拳用AddForce
    public Transform firePoint;            // Desert武器的開火點(射擊用)

    // 狀態
    private bool isAttacking = false;      // 攻擊動作進行中（PunchRoutine正在跑）
    private bool canControl = true;        // 是否允許讀取輸入(WASD / Space)
                                           // 攻擊後會暫時false，過一段時間再true

    // 自己吃到減速時用
    private float speedFactor = 1f;
    private Coroutine slowCR;

    void Start()
    {
        if (proximity == null)
            proximity = GetComponentInChildren<P1ProximityDetector>();
    }

    void Update()
    {
        HandleMovementInput();
        HandleEquipInput();
        HandleAttackInput();
    }

    // -------------------------
    // 移動輸入 / 移動施力
    // -------------------------

    Vector3 inputDir;

    void HandleMovementInput()
    {
        if (!canControl)
        {
            inputDir = Vector3.zero;
            return;
        }

        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.A)) h = -1;
        if (Input.GetKey(KeyCode.D)) h = 1;
        if (Input.GetKey(KeyCode.W)) v = 1;
        if (Input.GetKey(KeyCode.S)) v = -1;

        inputDir = new Vector3(h, 0, v).normalized;
    }

    void FixedUpdate()
    {
        if (bodyRoot == null) return;

        if (inputDir.sqrMagnitude > 0.01f)
        {
            // 依照輸入推動身體（Human:Fall Flat感覺）
            Vector3 horizVel = new Vector3(bodyRoot.velocity.x, 0, bodyRoot.velocity.z);
            if (horizVel.magnitude < maxSpeed * speedFactor)
            {
                bodyRoot.AddForce(inputDir * moveForce, ForceMode.Acceleration);
            }

            // 改變朝向
            Quaternion targetRot = Quaternion.LookRotation(inputDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }
    }

    // -------------------------
    // 攻擊(空白鍵)
    // -------------------------

    void HandleAttackInput()
    {
        if (!canControl) return;          // 被硬直鎖操作
        if (isAttacking) return;          // 正在攻擊流程中
        if (!Input.GetKeyDown(KeyCode.Space)) return;

        // 如果是 Desert 武器就射擊（遠程）
        if (desertWeaponEquip != null && desertWeaponEquip.activeSelf)
        {
            Shoot();
            StartCoroutine(AttackLockout(selfAttackLockTime));
        }
        else
        {
            // 近戰揮拳
            StartCoroutine(PunchRoutine());
            StartCoroutine(AttackLockout(selfAttackLockTime));
        }
    }

    // 遠程射擊
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
            bullet.Fire(transform.forward); // 讓Bullet自己處理拋物線與落地水灘
        }

        // 如果你還要加射擊動畫或音效可以在這裡觸發
    }

    // 近戰揮拳
    IEnumerator PunchRoutine()
    {
        isAttacking = true;

        // 1. 視覺上把右手往前推一下
        if (rightArm != null)
        {
            rightArm.AddForce(transform.forward * punchForce, ForceMode.Impulse);
        }

        // 2. 檢測面前是否打到對手，如果有 → 擊退 + 減速
        DoMeleeHitCheck();

        // 3. 攻擊收招冷卻
        yield return new WaitForSeconds(0.4f);

        isAttacking = false;
    }

    // 硬直 - 在攻擊後暫時鎖操作輸入
    IEnumerator AttackLockout(float lockTime)
    {
        canControl = false;

        // 在硬直期間也清除目前的移動輸入，避免殘留慣性控制
        inputDir = Vector3.zero;

        yield return new WaitForSeconds(lockTime);

        canControl = true;
    }

    // 用一個小球體在玩家前方檢查是否有打到對方
    void DoMeleeHitCheck()
    {
        Vector3 center = transform.position + transform.forward * punchRange;

        Collider[] hits = Physics.OverlapSphere(center, punchRadius);
        foreach (Collider hit in hits)
        {
            // 忽略自己
            if (hit.transform.root == this.transform.root)
                continue;

            // 嘗試抓 P2 的 ragdoll 控制器
            RagdollP2Controller enemy = hit.transform.root.GetComponent<RagdollP2Controller>();
            if (enemy != null)
            {
                // 對方收到擊退 + 減速
                enemy.TakeHitFrom(
                    sourcePos: transform.position,
                    force: knockbackForce,
                    slowMul: slowMultiplierOnHit,
                    slowTime: slowDurationOnHit
                );
                break; // 打到一個就好
            }
        }
    }

    // -------------------------
    // 撿裝備 (Y,U,I)
    // -------------------------

    void HandleEquipInput()
    {
        if (!canControl) return; // 被硬直時不允許撿裝

        if (proximity == null || proximity.nearItem == null) return;
        GameObject item = proximity.nearItem;

        if (item.CompareTag("SeaFood"))
        {
            if (Input.GetKeyDown(KeyCode.Y)) EquipItem(seaHeadEquip, seaHeadPrefab, item);
            if (Input.GetKeyDown(KeyCode.U)) EquipItem(seaBodyEquip, seaBodyPrefab, item);
            if (Input.GetKeyDown(KeyCode.I)) EquipItem(seaWeaponEquip, seaWeaponPrefab, item);
        }
        else if (item.CompareTag("Desert"))
        {
            if (Input.GetKeyDown(KeyCode.Y)) EquipItem(desertHeadEquip, desertHeadPrefab, item);
            if (Input.GetKeyDown(KeyCode.U)) EquipItem(desertBodyEquip, desertBodyPrefab, item);
            if (Input.GetKeyDown(KeyCode.I)) EquipItem(desertWeaponEquip, desertWeaponPrefab, item);
        }
    }

    void EquipItem(GameObject equipSlot, GameObject equipPrefab, GameObject groundItem)
    {
        // 關掉該部位的其他裝備
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

        // 隱藏地上物件
        if (ItemSpawner.Instance != null)
            ItemSpawner.Instance.HideItem(groundItem);

        // 撿起後就不算在範圍內了
        proximity.nearItem = null;
    }

    // -------------------------
    // 被打時：對手呼叫
    // -------------------------
    public void TakeHitFrom(Vector3 sourcePos, float force, float slowMul, float slowTime)
    {
        // 擊退：往"遠離攻擊者"方向推
        if (bodyRoot != null)
        {
            Vector3 dir = (transform.position - sourcePos);
            dir.y = 0f;
            dir.Normalize();
            bodyRoot.AddForce(dir * force, ForceMode.Impulse);
        }

        // 被打也可以吃到減速
        ApplySlow(slowMul, slowTime);
    }

    // -------------------------
    // 減速DEBUFF
    // -------------------------
    public void ApplySlow(float multiplier, float duration)
    {
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
