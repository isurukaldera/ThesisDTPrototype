using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("Database Reference")]
    public DatabaseManager dbManager;
    
    [Header("Spawner References")]
    public ProductSpawner storeSpawner;
    public ProductSpawner warehouseSpawner;
    
    [Header("UI Elements")]
    public TextMeshProUGUI lowStockText;
    public Button simulateSaleButton;
    public Button refreshButton;
    public Button restockButton;
    public Button openAIButton;
    public Button openLowStockButton;
    public Button addSampleDataButton;
    
    [Header("Panel References")]
    public GameObject aiPanel;
    public GameObject lowStockPanel;
    
    [Header("Low Stock Panel References")]
    public TextMeshProUGUI lowStockPanelText;
    public Button closeLowStockButton;
    public ScrollRect lowStockScrollRect; // NEW: Reference to scroll rect
    public RectTransform lowStockContent; // NEW: Reference to content

    void Start()
    {
        // Setup button listeners
        if (simulateSaleButton != null)
            simulateSaleButton.onClick.AddListener(SimulateRandomSale);
            
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshDisplay);
            
        if (restockButton != null)
            restockButton.onClick.AddListener(RestockLowStockItems);
            
        if (openAIButton != null)
            openAIButton.onClick.AddListener(OpenAIPanel);
            
        if (openLowStockButton != null)
            openLowStockButton.onClick.AddListener(OpenLowStockPanel);
            
        if (addSampleDataButton != null)
            addSampleDataButton.onClick.AddListener(AddSampleSalesData);
            
        if (closeLowStockButton != null)
            closeLowStockButton.onClick.AddListener(CloseLowStockPanel);

        // Initial display update
        UpdateLowStockDisplay();
        
        // Hide panels initially
        if (lowStockPanel != null)
            lowStockPanel.SetActive(false);
        
        Debug.Log("UI Manager initialized");
    }

    public void SimulateRandomSale()
    {
        if (dbManager == null || storeSpawner == null) 
        {
            Debug.LogError("DatabaseManager or StoreSpawner not assigned!");
            return;
        }
        
        List<Product> allProducts = dbManager.GetAllStoreStock();
        if (allProducts.Count > 0)
        {
            int randomIndex = Random.Range(0, allProducts.Count);
            Product randomProduct = allProducts[randomIndex];
            
            if (dbManager.RecordSale(randomProduct.ProductID, 1))
            {
                Debug.Log($"Simulated sale of {randomProduct.ProductName}");
                storeSpawner.RefreshAll();
                UpdateLowStockDisplay();
                
                // Update heatmap
                storeSpawner.GenerateHeatmap();
            }
            else
            {
                Debug.LogError($"Failed to simulate sale for {randomProduct.ProductName}");
            }
        }
        else
        {
            Debug.LogWarning("No products found in store for simulation");
        }
    }

    public void RefreshDisplay()
    {
        Debug.Log("Refreshing all displays...");
        
        if (storeSpawner != null) 
        {
            storeSpawner.RefreshAll();
            Debug.Log("Store spawner refreshed");
        }
        else
        {
            Debug.LogError("Store spawner not assigned!");
        }
        
        if (warehouseSpawner != null) 
        {
            warehouseSpawner.RefreshAll();
            Debug.Log("Warehouse spawner refreshed");
        }
        else
        {
            Debug.LogError("Warehouse spawner not assigned!");
        }
        
        UpdateLowStockDisplay();
    }

    public void RestockLowStockItems()
    {
        if (dbManager == null) 
        {
            Debug.LogError("DatabaseManager not assigned!");
            return;
        }
        
        List<Product> lowStockProducts = dbManager.GetLowStockProducts();
        Debug.Log($"Restocking {lowStockProducts.Count} low-stock items");
        
        int successCount = 0;
        foreach (Product p in lowStockProducts)
        {
            // Restock to bring to 2x reorder threshold
            int restockAmount = Mathf.Max(p.ReorderThreshold * 2 - p.Quantity, 1);
            
            if (dbManager.RestockStore(p.ProductID, restockAmount))
            {
                successCount++;
                Debug.Log($"Restocked {p.ProductName} with {restockAmount} units");
            }
            else
            {
                Debug.LogError($"Failed to restock {p.ProductName}");
            }
        }
        
        Debug.Log($"Successfully restocked {successCount} out of {lowStockProducts.Count} items");
        RefreshDisplay();
    }

    public void UpdateLowStockDisplay()
    {
        if (dbManager == null || lowStockText == null) 
        {
            Debug.LogError("DatabaseManager or lowStockText not assigned!");
            return;
        }
        
        List<Product> lowStockProducts = dbManager.GetLowStockProducts();
        
        if (lowStockProducts.Count == 0)
        {
            lowStockText.text = "🟢 ALL STOCK LEVELS OK\nNo low stock items";
        }
        else
        {
            lowStockText.text = $"🔴 LOW STOCK ALERTS ({lowStockProducts.Count} items):\n\n";
            
            foreach (Product p in lowStockProducts)
            {
                string stockLevel = p.Quantity <= 10 ? "CRITICAL" : "LOW";
                string colorTag = p.Quantity <= 10 ? "<color=red>" : "<color=yellow>";
                
                lowStockText.text += $"{colorTag}• {p.ProductName} ({p.Brand})\n";
                lowStockText.text += $"  Qty: {p.Quantity} | Shelf: {p.ShelfName}-{p.RowNumber} | {stockLevel}</color>\n\n";
            }
        }
    }

    public void UpdateLowStockPanelDisplay()
    {
        if (dbManager == null || lowStockPanelText == null) 
        {
            Debug.LogError("DatabaseManager or lowStockPanelText not assigned!");
            return;
        }
        
        List<Product> lowStockProducts = dbManager.GetLowStockProducts();
        
        if (lowStockProducts.Count == 0)
        {
            lowStockPanelText.text = "🟢 ALL STOCK LEVELS OK\n\nNo low stock items detected.\nAll products are adequately stocked.";
        }
        else
        {
            System.Text.StringBuilder displayText = new System.Text.StringBuilder();
            
            displayText.AppendLine($"<b>🔴 LOW STOCK ALERTS ({lowStockProducts.Count} ITEMS)</b>");
            displayText.AppendLine("<size=80%>Sorted by urgency</size>");
            displayText.AppendLine("────────────────────");
            
            // Sort by quantity (most critical first)
            var sortedProducts = lowStockProducts.OrderBy(p => p.Quantity).ToList();
            
            foreach (Product p in sortedProducts)
            {
                string stockLevel = p.Quantity <= 10 ? "CRITICAL" : "LOW";
                string colorTag = p.Quantity <= 10 ? "<color=red>" : "<color=yellow>";
                
                displayText.AppendLine($"<b>{p.ProductName} {p.Brand}</b>");
                displayText.AppendLine($"<size=85%>{p.Size} | {p.Category}</size>");
                displayText.AppendLine($"{colorTag}{stockLevel}: {p.Quantity} units remaining</color>");
                displayText.AppendLine($"Location: {p.ShelfName}-{p.RowNumber}");
                displayText.AppendLine($"Reorder at: {p.ReorderThreshold} units");
                
                // Calculate needed restock
                int needed = Mathf.Max(p.ReorderThreshold * 2 - p.Quantity, 1);
                displayText.AppendLine($"Recommended restock: {needed} units");
                displayText.AppendLine("────────────────────");
            }
            
            lowStockPanelText.text = displayText.ToString();
        }
        
        // NEW: Update the scroll view content size
        UpdateLowStockScrollContent();
    }
    
    // NEW: Method to update scroll view content size
    private void UpdateLowStockScrollContent()
    {
        if (lowStockPanelText != null && lowStockContent != null)
        {
            // Force text update to calculate proper size
            Canvas.ForceUpdateCanvases();
            
            // Calculate preferred height
            float preferredHeight = lowStockPanelText.preferredHeight;
            
            // Add padding
            float padding = 50f;
            
            // Set content height
            lowStockContent.sizeDelta = new Vector2(lowStockContent.sizeDelta.x, Mathf.Max(preferredHeight + padding, 400f));
            
            // Reset scroll to top
            if (lowStockScrollRect != null)
            {
                lowStockScrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }

    public void OpenAIPanel()
    {
        if (aiPanel != null)
        {
            aiPanel.SetActive(!aiPanel.activeSelf);
            Debug.Log($"AI Panel {(aiPanel.activeSelf ? "opened" : "closed")}");
            
            if (aiPanel.activeSelf)
            {
                AIRecommendationUI aiUI = FindObjectOfType<AIRecommendationUI>();
                if (aiUI != null)
                {
                    aiUI.RefreshRecommendationsDisplay();
                }
            }
        }
        else
        {
            Debug.LogError("AI Panel not assigned in UIManager!");
        }
    }

    public void OpenLowStockPanel()
    {
        if (lowStockPanel != null)
        {
            lowStockPanel.SetActive(!lowStockPanel.activeSelf);
            Debug.Log($"Low Stock Panel {(lowStockPanel.activeSelf ? "opened" : "closed")}");
            
            if (lowStockPanel.activeSelf)
            {
                UpdateLowStockPanelDisplay();
            }
        }
        else
        {
            Debug.LogError("Low Stock Panel not assigned in UIManager!");
        }
    }

    public void CloseLowStockPanel()
    {
        if (lowStockPanel != null)
            lowStockPanel.SetActive(false);
    }

    public void AddSampleSalesData()
    {
        if (dbManager != null)
        {
            dbManager.AddSampleSalesDataForAI();
            Debug.Log("Sample sales data added for AI training");
        }
        else
        {
            Debug.LogError("DatabaseManager not assigned!");
        }
    }

    public void SimulateMultipleSales(int count)
    {
        StartCoroutine(SimulateSalesCoroutine(count));
    }

    private IEnumerator SimulateSalesCoroutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SimulateRandomSale();
            yield return new WaitForSeconds(0.1f);
        }
    }
}