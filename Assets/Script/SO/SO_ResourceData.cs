using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ResourceData
{
    public ResourceType resourceType;
    public Sprite icon;
    public ResourceCont resourceCont;
}

[CreateAssetMenu(menuName = "SO/ResourceData")]
public class SO_ResourceData : ScriptableObject
{
    public ResourceData[] resourceDatas;

    public ResourceData GetResourceData(ResourceType resourceType)
    {
        var data = Array.Find(resourceDatas, data => data.resourceType == resourceType);
        return data;
    }
}
