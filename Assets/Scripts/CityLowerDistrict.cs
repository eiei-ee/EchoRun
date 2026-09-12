using UnityEngine;

// Visual-only authored blocks remain fixed in world space until recycled
// beyond the fog. Unlike the horizon, nearby rooftops must retain parallax.
public sealed class CityLowerDistrict : MonoBehaviour
{
    public const float BlockSize=96f;
    const int Width=5;
    readonly Transform[,] blocks=new Transform[Width,Width];
    Transform viewer;
    Vector2Int center=new Vector2Int(int.MinValue,int.MinValue);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if(FindObjectOfType<CityLowerDistrict>()!=null||Resources.Load<GameObject>("CityV7/LowerBlock0")==null)return;
        new GameObject("CityLowerDistrict").AddComponent<CityLowerDistrict>();
    }
    void Start()
    {
        for(int x=0;x<Width;x++)for(int z=0;z<Width;z++)
            blocks[x,z]=Instantiate(Resources.Load<GameObject>("CityV7/LowerBlock"+((x+z*3)%4)),transform).transform;
        LateUpdate();
    }
    void LateUpdate()
    {
        if(viewer==null&&Camera.main!=null)viewer=Camera.main.transform;
        if(viewer==null)return;
        var next=new Vector2Int(Mathf.FloorToInt(viewer.position.x/BlockSize),Mathf.FloorToInt(viewer.position.z/BlockSize));
        if(next==center)return;
        center=next;
        for(int x=next.x-2;x<=next.x+2;x++)for(int z=next.y-2;z<=next.y+2;z++)
        {
            Transform block=blocks[((x%Width)+Width)%Width,((z%Width)+Width)%Width];
            if(block!=null)block.position=new Vector3((x+.5f)*BlockSize,0,(z+.5f)*BlockSize);
        }
    }
}
