using System.Collections;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Flight Settings")]
    public float launchSpeed = 20f;          // 初速
    public float arcUpward = 0.35f;          // 拋物線上拋比例
    public float maxLifeTime = 6f;           // 最長存活時間（安全保險）

    [Header("Impact Settings")]
    public float knockbackForce = 500f;      // 擊退力
    public float slowMultiplier = 0.5f;      // 被擊中後的移動速度倍率（50%）
    public float slowDuration = 2.0f;        // 減速時間（秒）

    [Header("Puddle Settings")]
    public float puddleDuration = 4f;        // 水灘持續時間

    [Header("References")]
    public Rigidbody rb;
    public GameObject bulletModel;           // 飛行狀態外觀
    public GameObject puddleModel;           // 落地後外觀（液體）
    public Collider bulletCollider;          // 飛行時碰撞
    public Collider puddleTrigger;           // 水灘Trigger（需勾IsTrigger）

    [HideInInspector] public GameObject shooter; // 發射者（避免打到自己）

    private bool inPuddle = false;
    private float lifeTimer = 0f;

    // --------------------------------------------------------
    // 啟用：重置為飛行狀態
    // --------------------------------------------------------
    void OnEnable()
    {
        inPuddle = false;
        lifeTimer = 0f;

        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (bulletModel) bulletModel.SetActive(true);
        if (puddleModel) puddleModel.SetActive(false);

        if (bulletCollider) bulletCollider.enabled = true;
        if (puddleTrigger) puddleTrigger.enabled = false;
    }

    // --------------------------------------------------------
    // 飛行壽命檢查
    // --------------------------------------------------------
    void Update()
    {
        lifeTimer += Time.deltaTime;
        if (!inPuddle && lifeTimer > maxLifeTime)
        {
            ReturnToPool();
        }
    }

    // --------------------------------------------------------
    // 發射：dir = 玩家面向方向
    // --------------------------------------------------------
    public void Fire(Vector3 dir)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        dir.y = 0f;
        dir.Normalize();

        Vector3 launchDir = (dir + Vector3.up * arcUpward).normalized;
        rb.velocity = launchDir * launchSpeed;
    }

    // --------------------------------------------------------
    // 碰撞事件：處理玩家、牆、地板
    // --------------------------------------------------------
    void OnCollisionEnter(Collision collision)
    {
        if (inPuddle) return;

        GameObject other = collision.gameObject;

        // 避免打到自己
        if (other == shooter) return;

        if (other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            // 擊中玩家 → 擊退＋緩速 → 回收
            TryKnockAndSlow(other);
            ReturnToPool();
            return;
        }

        if (other.CompareTag("Ground"))
        {
            // 落地 → 生成液體形態
            Vector3 hitPoint = collision.GetContact(0).point;
            Vector3 hitNormal = collision.GetContact(0).normal;
            BecomePuddle(hitPoint, hitNormal);
            return;
        }

        if (other.CompareTag("Wall"))
        {
            // 撞牆 → 消失
            ReturnToPool();
            return;
        }

        // 其他情況：直接回收
        ReturnToPool();
    }

    // --------------------------------------------------------
    // 變成液體形態
    // --------------------------------------------------------
    void BecomePuddle(Vector3 hitPoint, Vector3 hitNormal)
    {
        inPuddle = true;

        rb.velocity = Vector3.zero;
        rb.isKinematic = true;

        if (bulletModel) bulletModel.SetActive(false);
        if (puddleModel) puddleModel.SetActive(true);

        transform.position = hitPoint + hitNormal * 0.01f;
        if (hitNormal != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, hitNormal),
                hitNormal
            );
        }

        if (bulletCollider) bulletCollider.enabled = false;
        if (puddleTrigger) puddleTrigger.enabled = true;

        StopAllCoroutines();
        StartCoroutine(PuddleLife());
    }

    // --------------------------------------------------------
    // 液體存在時間
    // --------------------------------------------------------
    IEnumerator PuddleLife()
    {
        yield return new WaitForSeconds(puddleDuration);
        ReturnToPool();
    }

    // --------------------------------------------------------
    // 玩家踩到液體：緩速效果
    // --------------------------------------------------------
    void OnTriggerEnter(Collider other)
    {
        if (!inPuddle) return;

        if (other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            ApplySlowOnly(other.gameObject);
        }
    }

    // --------------------------------------------------------
    // 擊退＋緩速
    // --------------------------------------------------------
    void TryKnockAndSlow(GameObject player)
    {
        // 呼叫新版 TakeHitFrom()，整合擊退與減速
        if (player.TryGetComponent<RagdollP1Controller>(out var p1))
        {
            p1.TakeHitFrom(transform.position, knockbackForce, slowMultiplier, slowDuration);
        }
        else if (player.TryGetComponent<RagdollP2Controller>(out var p2))
        {
            p2.TakeHitFrom(transform.position, knockbackForce, slowMultiplier, slowDuration);
        }
    }

    // --------------------------------------------------------
    // 僅緩速（踩到水灘）
    // --------------------------------------------------------
    void ApplySlowOnly(GameObject player)
    {
        if (player.TryGetComponent<RagdollP1Controller>(out var p1))
        {
            p1.ApplySlow(slowMultiplier, slowDuration);
        }
        else if (player.TryGetComponent<RagdollP2Controller>(out var p2))
        {
            p2.ApplySlow(slowMultiplier, slowDuration);
        }
    }

    // --------------------------------------------------------
    // 回收：隱藏物件回物件池
    // --------------------------------------------------------
    void ReturnToPool()
    {
        gameObject.SetActive(false);
    }
}
