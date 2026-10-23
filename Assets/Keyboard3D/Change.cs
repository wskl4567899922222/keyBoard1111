using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Change : MonoBehaviour
{
    public int Index;
    public Button ChangeBtn;
    void Awake()
    {
        ChangeBtn.onClick.AddListener(() => {
            SceneManager.LoadSceneAsync(Index);
        
        });
    }
    private void OnDisable()
    {
        ChangeBtn.onClick.RemoveAllListeners();
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
