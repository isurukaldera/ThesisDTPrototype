using UnityEngine;
using UnityEngine.EventSystems;

public class ProductInteractions : MonoBehaviour, IPointerClickHandler
{
    private Product product;
    private DatabaseManager dbManager;
    private ProductSpawner spawner;

    public void Initialize(Product product, DatabaseManager dbManager, ProductSpawner spawner)
    {
        this.product = product;
        this.dbManager = dbManager;
        this.spawner = spawner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (product == null || dbManager == null || spawner == null) 
        {
            Debug.LogError("ProductInteractions not properly initialized");
            return;
        }

        // Only allow interactions with store products
        if (product.Location == "Store")
        {
            if (dbManager.RecordSale(product.ProductID, 1))
            {
                Debug.Log($"✅ Sold 1 unit of {product.ProductName}");
                spawner.RefreshAll();
                
                // Update UI displays
                UpdateAllDisplays();
            }
            else
            {
                Debug.LogError($"❌ Failed to sell {product.ProductName}");
            }
        }
    }

    private void UpdateAllDisplays()
    {
        // Update low stock display
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.UpdateLowStockDisplay();
        }

        // Update AI recommendations if panel is open
        AIRecommendationUI aiUI = FindObjectOfType<AIRecommendationUI>();
        if (aiUI != null)
        {
            aiUI.RefreshRecommendationsDisplay();
        }
    }
}