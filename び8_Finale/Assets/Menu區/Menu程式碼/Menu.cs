using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    public GameObject First;
    public GameObject Credit;
    public GameObject Tutorial;

    public GameObject BCredit;
    public GameObject BStart;
    public GameObject BTutorial;

    public GameObject BCBack;
    public GameObject BTBack;
    public GameObject BExit;

    public GameObject Page1;
    public GameObject Page2;
    public GameObject Page3;

    public GameObject Next;
    public GameObject Prev;

    public int PageCount;

    public Animator startAnimator;
    public Animator creditAnimator;
    public Animator tutorialAnimator;
    public Animator quitAnimator;

    public void LoadMaingameScene()
    {
        SceneManager.LoadSceneAsync(1);
    }
    // Start is called before the first frame update
    void Start()
    {
        Button CreditButtonComponent = BCredit.GetComponent<Button>();
        CreditButtonComponent.onClick.AddListener(PressCredit);

        Button TutorialButtonComponent = BTutorial.GetComponent<Button>();
        TutorialButtonComponent.onClick.AddListener(PressTutorial);

        Button BCBackButtonComponent = BCBack.GetComponent<Button>();
        BCBackButtonComponent.onClick.AddListener(PressBack);

        Button BTBackButtonComponent = BTBack.GetComponent<Button>();
        BTBackButtonComponent.onClick.AddListener(PressBack);

        Button ExitButtonComponent = BExit.GetComponent<Button>();
        ExitButtonComponent.onClick.AddListener(PressExit);

        Button StartButtonComponent = BStart.GetComponent<Button>();
        StartButtonComponent.onClick.AddListener(PressStart);

        Button NextButtonComponent = Next.GetComponent<Button>();
        NextButtonComponent.onClick.AddListener(PressNext);

        Button PrevButtonComponent = Prev.GetComponent<Button>();
        PrevButtonComponent.onClick.AddListener(PressPrev);

        Credit.SetActive(false);
        Tutorial.SetActive(false);

        
    }

    // Update is called once per frame
    void Update()
    {

    }
    public void PressCredit()
    {
        creditAnimator.SetTrigger("Credit");
        StartCoroutine(WaitForCreditAnimation());
    }
    IEnumerator WaitForCreditAnimation()
    {
        // 等動畫播完（用動畫長度取代硬編碼秒數）
        float animLength = creditAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animLength);


        First.SetActive(false);
        Credit.SetActive(true);
    }
    public void PressTutorial()
    {
        tutorialAnimator.SetTrigger("Tutorial");
        StartCoroutine(WaitForTutorialAnimation());
    }
    IEnumerator WaitForTutorialAnimation()
    {
        // 等動畫播完（用動畫長度取代硬編碼秒數）
        float animLength = tutorialAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animLength);


        First.SetActive(false);
        Tutorial.SetActive(true);

        Page1.SetActive(true);
        Page2.SetActive(false);
        Page3.SetActive(false);

        Prev.SetActive(false);
        Next.SetActive(true);
        PageCount = 1;
    }

    public void PressExit()
    {
        quitAnimator.SetTrigger("Exit");
        StartCoroutine(WaitForExitAnimation());

    }
    IEnumerator WaitForExitAnimation()
    {
        // 等動畫播完（用動畫長度取代硬編碼秒數）
        float animLength = quitAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animLength);


        Application.Quit();
    }

    public void PressBack()
    {
        SceneManager.LoadSceneAsync(0);
    }

    public void PressNext()
    {
        PageCount++;
        if (PageCount == 1)
        {
            Page1.SetActive(true);
            Page2.SetActive(false);
            Page3.SetActive(false);

            Prev.SetActive(false);
            Next.SetActive(true);
        }
        else if (PageCount == 2)
        {
            Page1.SetActive(false);
            Page2.SetActive(true);
            Page3.SetActive(false);

            Prev.SetActive(true);
            Next.SetActive(true);
        }
        else if (PageCount == 3)
        {
            Page1.SetActive(false);
            Page2.SetActive(false);
            Page3.SetActive(true);

            Prev.SetActive(true);
            Next.SetActive(false);
        }
    }
    public void PressPrev()
    {
        PageCount--;
        if (PageCount == 1)
        {
            Page1.SetActive(true);
            Page2.SetActive(false);
            Page3.SetActive(false);

            Prev.SetActive(false);
            Next.SetActive(true);
        }
        else if (PageCount == 2)
        {
            Page1.SetActive(false);
            Page2.SetActive(true);
            Page3.SetActive(false);

            Prev.SetActive(true);
            Next.SetActive(true);
        }
        else if (PageCount == 3)
        {
            Page1.SetActive(false);
            Page2.SetActive(false);
            Page3.SetActive(true);

            Prev.SetActive(true);
            Next.SetActive(false);
        }
    }
    public void PressStart()
    {
        startAnimator.SetTrigger("Start");
        StartCoroutine(WaitForStartAnimation());
    }
    IEnumerator WaitForStartAnimation()
    {
        // 等動畫播完（用動畫長度取代硬編碼秒數）
        float animLength = startAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animLength);


        LoadMaingameScene();
    }
}
