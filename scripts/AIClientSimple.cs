using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

[Serializable]
public class RecommendRequest 
{
    public int product_id;
    public int period_days = 7;
    public int historical_weeks = 4;
    public float safety_buffer = 0.15f;
}

[Serializable]
public class RecommendResponse 
{
    public int product_id;
    public string product_name;
    public string category;
    public float predicted_daily;
    public float predicted_daily_with_buffer;
    public float predicted_period_demand;
    public float predicted_with_buffer;
    public int period_days;
    public float ideal_stock;
    public int current_shelf;
    public int current_warehouse;
    public float recommended_transfer;
    public float recommended_order;
    public float safety_buffer;
    public string status;
    public string error;
}

[Serializable]
public class ServerUrlResponse 
{
    public string url;
    public string status;
    public string timestamp;
}

public class AIClientSimple : MonoBehaviour
{
    [Header("AI Server Settings")]
    public string bootstrapUrl = "https://unpotently-transrational-azlen.ngrok-free.dev";
    
    [Header("Testing")]
    public int testProductID = 1;
    public bool autoTestOnStart = true;
    
    // Events for UI updates
    public System.Action<string> OnAITestResult;
    public System.Action<RecommendResponse> OnAIRecommendationReceived;
    public System.Action<string> OnAIError;
    
    private string currentServerUrl = "";
    private DatabaseManager dbManager;
    
    void Start()
    {
        // Fix SSL certificate issues
        ServicePointManager.ServerCertificateValidationCallback = 
            (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => 
        {
            return true; // Accept all certificates
        };
        
        if (dbManager == null)
            dbManager = FindObjectOfType<DatabaseManager>();
        
        Debug.Log("AI Client starting...");
        
        // Start by setting up the server URL
        SetupServerUrl();
    }
    
    private void SetupServerUrl()
    {
        // Use bootstrap URL directly - no need to fetch from /server-url
        currentServerUrl = bootstrapUrl;
        Debug.Log($"Server URL set: {currentServerUrl}");
        
        // Test connection after setting URL
        if (autoTestOnStart)
        {
            Invoke("TestConnection", 2.0f);
        }
    }
    
    // ========== ADDED MISSING METHOD ==========
    public void RefreshServerUrl()
    {
        Debug.Log("Refreshing server URL...");
        SetupServerUrl();
        OnAITestResult?.Invoke("Server URL refreshed");
    }
    // ========== END OF ADDED METHOD ==========
    
    public void TestConnection()
    {
        StartCoroutine(TestServerConnection());
    }
    
    private IEnumerator TestServerConnection()
    {
        if (string.IsNullOrEmpty(currentServerUrl))
        {
            Debug.LogError("Server URL not available");
            OnAIError?.Invoke("Server URL not available");
            yield break;
        }
        
        string url = currentServerUrl + "/test";
        Debug.Log($"Testing connection to: {url}");
        
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"Cannot connect to AI server: {www.error}";
                Debug.LogError(errorMsg);
                OnAIError?.Invoke(errorMsg);
            }
            else
            {
                string successMsg = $"AI server connected! Response: {www.downloadHandler.text}";
                Debug.Log(successMsg);
                OnAITestResult?.Invoke("AI Server Connected!");
            }
        }
    }
    
    public void RequestRecommendation(int productId)
    {
        if (string.IsNullOrEmpty(currentServerUrl))
        {
            Debug.LogError("Server URL not available");
            OnAIError?.Invoke("Server URL not available");
            return;
        }
        
        StartCoroutine(PostRecommend(productId));
    }
    
    private IEnumerator PostRecommend(int productId)
    {
        Debug.Log($"Requesting AI recommendation for product {productId}");
        
        var req = new RecommendRequest { 
            product_id = productId,
            period_days = 7,
            historical_weeks = 4,
            safety_buffer = 0.15f
        };
        
        string json = JsonUtility.ToJson(req);
        string url = currentServerUrl + "/recommend";
        
        Debug.Log($"Sending request to: {url}");
        Debug.Log($"Request data: {json}");
        
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 15;
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"AI request failed: {www.error}";
                Debug.LogError(errorMsg);
                OnAIError?.Invoke(errorMsg);
                yield break;
            }
            
            string resp = www.downloadHandler.text;
            Debug.Log($"AI response received: {resp}");
            
            try 
            {
                var recommendation = JsonUtility.FromJson<RecommendResponse>(resp);
                
                if (recommendation.status != "success")
                {
                    string errorMsg = $"AI Server Error: {recommendation.error}";
                    Debug.LogError(errorMsg);
                    OnAIError?.Invoke(errorMsg);
                    yield break;
                }
                
                // Save to database
                if (dbManager != null)
                {
                    dbManager.SaveAIRecommendation(
                        recommendation.product_id,
                        recommendation.predicted_with_buffer,
                        recommendation.current_shelf,
                        recommendation.current_warehouse,
                        (int)recommendation.recommended_transfer,
                        (int)recommendation.recommended_order
                    );
                    
                    Debug.Log($"Saved AI recommendation for product {recommendation.product_id}");
                }
                
                // Trigger event for UI update
                OnAIRecommendationReceived?.Invoke(recommendation);
                
                // Update UI if available
                AIRecommendationUI aiUI = FindObjectOfType<AIRecommendationUI>();
                if (aiUI != null)
                    aiUI.RefreshRecommendationsDisplay();
                    
            }
            catch (Exception ex)
            {
                string errorMsg = $"JSON parse failed: {ex.Message}";
                Debug.LogError(errorMsg);
                OnAIError?.Invoke(errorMsg);
            }
        }
    }
    
    public void RequestRecommendationsForLowStock()
    {
        if (string.IsNullOrEmpty(currentServerUrl))
        {
            Debug.LogError("Server URL not available");
            OnAIError?.Invoke("Server URL not available");
            return;
        }
        
        if (dbManager == null) 
        {
            Debug.LogError("DatabaseManager not found!");
            return;
        }
        
        var lowStockProducts = dbManager.GetLowStockProducts();
        Debug.Log($"Requesting AI recommendations for {lowStockProducts.Count} low-stock products");
        
        foreach (var product in lowStockProducts)
        {
            RequestRecommendation(product.ProductID);
        }
        
        Invoke("RefreshAIDisplay", 3.0f);
    }
    
    public void RequestRecommendationsForAllProducts()
    {
        if (string.IsNullOrEmpty(currentServerUrl))
        {
            Debug.LogError("Server URL not available");
            OnAIError?.Invoke("Server URL not available");
            return;
        }
        
        if (dbManager == null) return;
        
        var allProducts = dbManager.GetAllStoreStock();
        Debug.Log($"Requesting AI recommendations for {allProducts.Count} products");
        
        foreach (var product in allProducts)
        {
            RequestRecommendation(product.ProductID);
        }
        
        Invoke("RefreshAIDisplay", 5.0f);
    }
    
    private void RefreshAIDisplay()
    {
        AIRecommendationUI aiUI = FindObjectOfType<AIRecommendationUI>();
        if (aiUI != null)
            aiUI.RefreshRecommendationsDisplay();
    }
    
    // Quick test method
    public void QuickAITest()
    {
        StartCoroutine(QuickTestCoroutine());
    }
    
    private IEnumerator QuickTestCoroutine()
    {
        Debug.Log("Starting Quick AI Test...");
        
        // Test connection
        yield return StartCoroutine(TestServerConnection());
        yield return new WaitForSeconds(1);
        
        // Test AI Recommendation
        RequestRecommendation(testProductID);
    }
}