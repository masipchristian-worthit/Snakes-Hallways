using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyAttack : MonoBehaviour
{
    [SerializeField] float windowDuration = 0.35f;

    Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;
        if (gameObject.tag != "EnemyAttack") gameObject.tag = "EnemyAttack";
    }

    public void OpenWindow()
    {
        StopAllCoroutines();
        StartCoroutine(WindowCo());
        AudioManager.Instance?.PlaySFX(SFXId.MinotaurCharge, transform.position);
    }

    IEnumerator WindowCo()
    {
        col.enabled = true;
        yield return new WaitForSeconds(windowDuration);
        col.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GameManager.Instance?.TriggerGameOver();
    }
}
