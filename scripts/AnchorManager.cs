using UnityEngine;
using System.Collections.Generic;

public class AnchorManager : MonoBehaviour
{
    [System.Serializable]
    public class AnchorEntry
    {
        public string shelfName;
        public int rowNumber;
        public Transform anchor;
    }
    public List<AnchorEntry> anchors = new List<AnchorEntry>();
    public Transform GetAnchor(string shelf, int row)
    {
        if (string.IsNullOrEmpty(shelf)) return null;
        for (int i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i];
            if (a != null && a.anchor != null && a.shelfName == shelf && a.rowNumber == row) 
                return a.anchor;
        }
        return null;
    }
}