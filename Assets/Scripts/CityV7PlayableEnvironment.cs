using UnityEngine;

// City presentation follows the existing segment pool.
// All collision, route generation, controls and formal character code are retained.
public static class CityV7PlayableEnvironment
{
    public static Material CreateSky()
    {
        var material=new Material(Resources.Load<Shader>("CityV7/CityAfterimageQuietSky"));
        material.SetColor("_Zenith",Color32ToColor(0x84A7CC));material.SetColor("_Horizon",Color32ToColor(0xC1D4DD));material.SetColor("_Ground",Color32ToColor(0xAEB9BA));return material;
    }
    static Color Color32ToColor(int rgb){return new Color((rgb>>16&255)/255f,(rgb>>8&255)/255f,(rgb&255)/255f);}
    public static void ApplyAtmosphere(Light key=null,Light fill=null)
    {
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=128;RenderSettings.fogEndDistance=140;
        RenderSettings.fogColor=Color32ToColor(0xBFCAD0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=Color32ToColor(0x8FABD6);RenderSettings.ambientEquatorColor=Color32ToColor(0x6485B0)*.85f;RenderSettings.ambientGroundColor=Color32ToColor(0x415C82)*.65f;
        if(key!=null){key.color=Color32ToColor(0xFFF1DB);key.intensity=1.22f;key.transform.rotation=Quaternion.Euler(53,-40,0);key.shadowStrength=.86f;key.shadowBias=.035f;key.shadowNormalBias=.10f;}
        if(fill!=null)fill.intensity=0;
    }
    public static bool Decorate(GameObject segment,TrackSegmentType type)
    {
        if(type!=TrackSegmentType.Straight){RefreshClearance();return false;}
        var data=segment.GetComponent<TrackSegmentData>();
        if(data==null)return false;
        int index=((Mathf.RoundToInt(data.routeDistance/20f)%9)+9)%9;
        var prefab=Resources.Load<GameObject>("CityV7/Chunk"+index);
        if(prefab==null)return false;
        // The authored prefab disabled its fallback surface because its old
        // environment supplied the visible road. City chunks contain buildings
        // only, so restore that independent surface before hiding the old group.
        foreach(Transform child in segment.transform)
        {
            bool ground=child.name=="GroundPlane";
            if(!ground&&!child.name.StartsWith("LaneLine_"))continue;
            var renderer=child.GetComponent<Renderer>();
            if(renderer==null)continue;
            renderer.enabled=true;
            if(ground)EchoRoadVisualController.Instance.ApplyTo(renderer,RoadSurfaceRole.Main);
        }
        var existing=segment.transform.Find("CityV7Environment");
        if(existing!=null&&existing.GetComponent<CityV7ChunkIdentity>().index==index){RefreshClearance();return true;}
        if(existing!=null){existing.gameObject.SetActive(false);Object.Destroy(existing.gameObject);}
        var legacy=segment.transform.Find("EchoEnvironment");if(legacy!=null)legacy.gameObject.SetActive(false);
        var visual=Object.Instantiate(prefab,segment.transform,false);visual.name="CityV7Environment";
        visual.AddComponent<CityV7ChunkIdentity>().index=index;
        RefreshClearance();
        return true;
    }

    // Re-evaluate when the route pool changes, before a newly planned bend is
    // visible. Adjacent straight chunks can otherwise extend into its exit arm
    // or the outside arc of the unchanged gameplay camera.
    public static void RefreshClearance()
    {
        var route=Object.FindObjectsOfType<TrackSegmentData>();
        foreach(var city in Object.FindObjectsOfType<CityV7ChunkIdentity>())
        foreach(Transform building in city.transform)
        {
            var renderers=building.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)continue;
            Bounds world=WorldBounds(renderers[0]);
            for(int i=1;i<renderers.Length;i++)world.Encapsulate(WorldBounds(renderers[i]));
            bool blocked=false;
            foreach(var segment in route)
            {
                Bounds local=TransformBounds(world,segment.transform.worldToLocalMatrix);
                bool turn=segment.segmentType!=TrackSegmentType.Straight;
                Bounds corridor=turn
                    ? new Bounds(new Vector3(0,3,10),new Vector3(28,6.2f,28))
                    : new Bounds(new Vector3(0,3,0),new Vector3(11.2f,6.2f,20));
                if(!corridor.Intersects(local))continue;
                blocked=true;break;
            }
            building.gameObject.SetActive(!blocked);
        }
    }
    static Bounds WorldBounds(Renderer renderer)
    {
        return TransformBounds(renderer.localBounds,renderer.transform.localToWorldMatrix);
    }
    static Bounds TransformBounds(Bounds bounds,Matrix4x4 matrix)
    {
        Bounds result=new Bounds(matrix.MultiplyPoint3x4(bounds.min),Vector3.zero);
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
            result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z)));
        return result;
    }
}
public sealed class CityV7ChunkIdentity:MonoBehaviour{public int index;}
