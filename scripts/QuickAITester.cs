using UnityEngine;

public class QuickAITester : MonoBehaviour
{
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 100, 200, 200));
        
        if (GUILayout.Button("🧪 TEST AI", GUILayout.Height(30)))
        {
            FindObjectOfType<AIClientSimple>().TestConnection();
        }
        
        if (GUILayout.Button("🤖 GET RECS", GUILayout.Height(30)))
        {
            FindObjectOfType<AIClientSimple>().RequestRecommendationsForLowStock();
        }
        
        GUILayout.EndArea();
    }
}