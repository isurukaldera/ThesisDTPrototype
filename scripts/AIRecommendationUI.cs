using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class AIRecommendationUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject aiPanel;
    public TextMeshProUGUI recommendationsText;
    public TextMeshProUGUI statusText;
    public Button testAIServerButton;
    public Button generateAIRecommendationsButton;
    public Button generateAllRecommendationsButton;
    public Button closeAIButton;
    public Button refreshServerUrlButton;
    
    [Header("Scroll View Components")]
    public ScrollRect recommendationsScrollRect;
    public RectTransform recommendationsContent;
    
    [Header("Settings")]
    public int maxDisplayRecommendations = 10;
    
    private DatabaseManager dbManager;
    private AIClientSimple aiClient;
    
    void Start()
    {
        dbManager = FindObjectOfType<DatabaseManager>();
        aiClient = FindObjectOfType<AIClientSimple>();
        
        // Setup button listeners
        if (testAIServerButton != null)
            testAIServerButton.onClick.AddListener(TestAIServer);
            
        if (generateAIRecommendationsButton != null)
            generateAIRecommendationsButton.onClick.AddListener(GenerateAIRecommendations);
            
        if (generateAllRecommendationsButton != null)
            generateAllRecommendationsButton.onClick.AddListener(GenerateAllRecommendations);
            
        if (closeAIButton != null)
            closeAIButton.onClick.AddListener(CloseAIPanel);
        
        if (refreshServerUrlButton != null)
            refreshServerUrlButton.onClick.AddListener(RefreshServerUrl);
        
        // Subscribe to AI events
        if (aiClient != null)
        {
            aiClient.OnAITestResult += OnAITestResult;
            aiClient.OnAIRecommendationReceived += OnAIRecommendationReceived;
            aiClient.OnAIError += OnAIError;
        }
        
        // Hide panel initially
        if (aiPanel != null)
            aiPanel.SetActive(false);
            
        // Initialize scroll view
        InitializeScrollView();
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (aiClient != null)
        {
            aiClient.OnAITestResult -= OnAITestResult;
            aiClient.OnAIRecommendationReceived -= OnAIRecommendationReceived;
            aiClient.OnAIError -= OnAIError;
        }
    }
    
    private void InitializeScrollView()
    {
        if (recommendationsScrollRect != null)
        {
            recommendationsScrollRect.vertical = true;
            recommendationsScrollRect.horizontal = false;
            recommendationsScrollRect.movementType = ScrollRect.MovementType.Clamped;
            recommendationsScrollRect.inertia = false;
            recommendationsScrollRect.scrollSensitivity = 25f;
        }
        
        if (recommendationsContent != null)
        {
            recommendationsContent.anchorMin = new Vector2(0, 1);
            recommendationsContent.anchorMax = new Vector2(1, 1);
            recommendationsContent.pivot = new Vector2(0.5f, 1);
            recommendationsContent.sizeDelta = new Vector2(0, 0);
        }
    }
    
    public void ToggleAIPanel()
    {
        if (aiPanel != null)
        {
            aiPanel.SetActive(!aiPanel.activeSelf);
            if (aiPanel.activeSelf)
            {
                RefreshRecommendationsDisplay();
            }
        }
    }
    
    public void CloseAIPanel()
    {
        if (aiPanel != null)
            aiPanel.SetActive(false);
    }
    
    public void TestAIServer()
    {
        if (aiClient != null)
        {
            UpdateStatus("Testing AI server connection...");
            aiClient.TestConnection();
        }
    }
    
    public void RefreshServerUrl()
    {
        if (aiClient != null)
        {
            UpdateStatus("Refreshing server URL...");
            aiClient.RefreshServerUrl();
        }
    }
    
    public void GenerateAIRecommendations()
    {
        if (aiClient != null)
        {
            UpdateStatus("Generating AI recommendations for low-stock products...");
            aiClient.RequestRecommendationsForLowStock();
        }
    }
    
    public void GenerateAllRecommendations()
    {
        if (aiClient != null)
        {
            UpdateStatus("Generating AI recommendations for all products...");
            aiClient.RequestRecommendationsForAllProducts();
        }
    }
    
    public void RefreshRecommendationsDisplay()
    {
        if (dbManager == null || recommendationsText == null) return;
        
        var recommendations = dbManager.GetAllAIRecommendations();
        
        if (recommendations.Count == 0)
        {
            recommendationsText.text = "No AI recommendations yet.\n\nClick 'Generate Recommendations' to get AI-powered restocking suggestions.";
            UpdateContentSize();
            return;
        }
        
        // Sort by priority (recommended transfer + order)
        var sortedRecs = recommendations
            .OrderByDescending(r => r.RecommendedTransfer + r.RecommendedOrder)
            .Take(maxDisplayRecommendations)
            .ToList();
        
        System.Text.StringBuilder displayText = new System.Text.StringBuilder();
        
        displayText.AppendLine($"<b>AI RECOMMENDATIONS ({sortedRecs.Count} shown)</b>");
        displayText.AppendLine("<size=80%>Sorted by priority</size>");
        displayText.AppendLine("────────────────────");
        
        foreach (var rec in sortedRecs)
        {
            var product = dbManager.GetProductByID(rec.ProductID);
            if (product != null)
            {
                string productName = $"{product.ProductName} {product.Brand}".Trim();
                if (productName.Length > 25)
                    productName = productName.Substring(0, 25) + "...";
                
                displayText.AppendLine($"<b>{productName}</b>");
                displayText.AppendLine($"<size=85%>{product.Size} |  {GetProductLocation(product)}</size>");
                
                // Stock information
                displayText.AppendLine($"Current: Shelf {rec.CurrentShelfStock} | Warehouse {rec.CurrentWarehouseStock}");
                
                // Recommendations with icons and colors
                bool hasAction = false;
                
                if (rec.RecommendedTransfer > 0)
                {
                    displayText.AppendLine($"<color=orange>Transfer: {rec.RecommendedTransfer} from warehouse</color>");
                    hasAction = true;
                }
                
                if (rec.RecommendedOrder > 0)
                {
                    displayText.AppendLine($"<color=red>Order: {rec.RecommendedOrder} from supplier</color>");
                    hasAction = true;
                }
                
                if (!hasAction)
                {
                    displayText.AppendLine($"<color=green>Stock levels optimal</color>");
                }
                
                displayText.AppendLine("────────────────────");
            }
        }
        
        recommendationsText.text = displayText.ToString();
        UpdateContentSize();
    }
    
    private void UpdateContentSize()
    {
        if (recommendationsText != null && recommendationsContent != null)
        {
            // Force update the text mesh to calculate proper size
            Canvas.ForceUpdateCanvases();
            
            // Calculate the preferred height of the text
            float preferredHeight = recommendationsText.preferredHeight;
            
            // Add some padding
            float padding = 40f;
            
            // Set the content height
            recommendationsContent.sizeDelta = new Vector2(recommendationsContent.sizeDelta.x, Mathf.Max(preferredHeight + padding, recommendationsScrollRect.GetComponent<RectTransform>().rect.height));
            
            // Reset scroll position to top
            if (recommendationsScrollRect != null)
            {
                recommendationsScrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }
    
    private string GetProductLocation(Product product)
    {
        if (product == null) return "Unknown";
        return $"{product.ShelfName}-{product.RowNumber}";
    }
    
    private void OnAITestResult(string message)
    {
        UpdateStatus(message);
        Debug.Log(message);
    }
    
    private void OnAIRecommendationReceived(RecommendResponse recommendation)
    {
        UpdateStatus($"AI recommendation received for Product {recommendation.product_id}");
        RefreshRecommendationsDisplay();
    }
    
    private void OnAIError(string error)
    {
        UpdateStatus(error);
        Debug.LogError(error);
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            
            // Auto-clear status after 5 seconds
            CancelInvoke("ClearStatus");
            Invoke("ClearStatus", 5f);
        }
    }
    
    private void ClearStatus()
    {
        if (statusText != null)
            statusText.text = "";
    }
}