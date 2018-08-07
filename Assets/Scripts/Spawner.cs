using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject Prefab;
    private List<GameObject> objList;
    public int MaxCount;
    private int Count;
    private float pretime;

    private void Start()
    {
        Count = 0;
        pretime = 0;
        objList = new List<GameObject>();
    }

    private void Update()
    {
        if (Count < MaxCount && Time.time - pretime >= 0.1)
        {
            GameObject obj = InstancingMgr.Instance.CreateInstance(Prefab);
            obj.transform.position = new Vector3(Count / 50 *50, Count % 10 * 50, 0);
            objList.Add(obj);
            Count++;
        }
    }
}