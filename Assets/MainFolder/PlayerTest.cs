using System.Collections;
using AbsoluteZero.Core.Common;
using UnityEngine;

public class PlayerTest : MonoBehaviour
{
    Coroutine dmgCo;
    Coroutine freezeCo;
    public Animator anim;

    int count = 0;

    public GameObject hitParticle;
    public GameObject iceBreakParticle;
    public GameObject finalBreakParticle;

    public SpriteRenderer ice;

    public SpriteRenderer[] playerRen;
    public Material[] playerMat = new Material[9];

    private void Start()
    {
        for(int i = 0; i < playerRen.Length; i++)
        {
            playerMat[i] = playerRen[i].material;
        }
    }

    public void GetDamaged()
    {
        if (dmgCo != null)
            StopCoroutine(dmgCo);
        dmgCo = StartCoroutine(DamageCoroutine());
    }

    IEnumerator DamageCoroutine()
    {
        float duration = 0.15f;
        float curtime = 0f;

        anim.SetTrigger("damage");

        GameObject Particle = Instantiate(hitParticle, transform.position, transform.rotation);

        while(curtime < duration)
        {
            curtime += Time.deltaTime;

            float currentFlash = Mathf.Lerp(1f, 0f, curtime / duration);
            for(int i = 0; i < playerMat.Length; i++)
            {
                playerMat[i].SetFloat("_FlashAmount", currentFlash);
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        anim.SetTrigger("end");

        dmgCo = null;
    }

    public void GetFreeze()
    {
        count++;

        if (count > 2)
        {
            StartCoroutine(DieCoroutine());
            return;
        }


        if (freezeCo != null)
            StopCoroutine(freezeCo);
        freezeCo = StartCoroutine(FreezeCoroutine());
    }

    IEnumerator FreezeCoroutine()
    {
        anim.SetTrigger("freeze");
        ice.color = new Color(1f, 1f, 1f, 0.6f);
        yield return new WaitForSeconds(1.1f);
        GameObject Particle = Instantiate(iceBreakParticle, transform.position, transform.rotation);
        anim.SetTrigger("end");

        freezeCo = null;
    }

    IEnumerator DieCoroutine()
    {
        anim.SetTrigger("freeze");
        ice.color = new Color(1f, 1f, 1f, 1f);
        yield return new WaitForSeconds(1.1f);
        CameraShake.Instance?.Shake(0.5f, 0.3f);
        GameObject Particle = Instantiate(finalBreakParticle, transform.position, transform.rotation);
        gameObject.SetActive(false);

        freezeCo = null;
    }
}
