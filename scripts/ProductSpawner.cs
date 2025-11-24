using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class ProductSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ProductPrefabMap
    {
        public string productName;
        public string brand;
        public string size;
        public GameObject prefab;
    }

    [System.Serializable]
    public class CategoryPrefabMapping
    {
        public string category;
        public List<GameObject> prefabs;
    }

    [Header("Prefab Mappings")]
    public List<ProductPrefabMap> productPrefabMappings = new List<ProductPrefabMap>();
    public List<CategoryPrefabMapping> categoryPrefabMappings = new List<CategoryPrefabMapping>();
    public GameObject warehouseProductPrefab;
    
    [Header("References")]
    public AnchorManager anchorManager;
    public DatabaseManager dbManager;
    public GameObject heatmapPrefab;

    [Header("Settings")]
    [Tooltip("True = store mode (A..G). False = warehouse mode (LocationA..LocationG).")]
    public bool isStore = true;

    [Header("Product Placement Settings")]
    public float spacingX = 0.15f;
    public float spacingZ = 0.15f;
    public int maxPerRow = 4;

    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private readonly List<GameObject> spawnedProducts = new List<GameObject>();

    void Awake()
    {
        Debug.Log("Initializing ProductSpawner");
        BuildPrefabCache();
    }

    private void BuildPrefabCache()
    {
        // Build prefab cache for faster lookup
        foreach (var mapping in productPrefabMappings)
        {
            if (mapping.prefab == null) continue;

            // Create key with product name, brand, and size
            string keyWithSize = $"{mapping.productName.Trim()}_{mapping.brand.Trim()}_{mapping.size.Trim()}".ToLower();
            if (!prefabCache.ContainsKey(keyWithSize))
            {
                prefabCache[keyWithSize] = mapping.prefab;
            }

            // Also create a key without size for fallback
            string keyWithoutSize = $"{mapping.productName.Trim()}_{mapping.brand.Trim()}".ToLower();
            if (!prefabCache.ContainsKey(keyWithoutSize))
            {
                prefabCache[keyWithoutSize] = mapping.prefab;
            }
        }

        Debug.Log($"Total prefabs cached: {prefabCache.Count}");
    }

    private void Start()
    {
        Debug.Log("Starting ProductSpawner");
        SpawnProducts();
        if (isStore) GenerateHeatmap();
    }

    public void SpawnProducts()
    {
        Debug.Log($"Spawning products for {(isStore ? "Store" : "Warehouse")}");

        // Cleanup existing products
        ClearSpawnedProducts();

        if (dbManager == null || anchorManager == null)
        {
            Debug.LogError("Spawner missing DatabaseManager or AnchorManager references.");
            return;
        }

        List<Product> products = isStore ? dbManager.GetAllStoreStock() : dbManager.GetAllWarehouseStock();
        Debug.Log($"Found {products.Count} products in {(isStore ? "Store" : "Warehouse")}");

        if (products.Count == 0)
        {
            Debug.LogWarning("No products found in database!");
            return;
        }

        // Group by shelf + row
        var grouped = products.GroupBy(p => new { p.ShelfName, p.RowNumber });
        Debug.Log($"Grouped into {grouped.Count()} shelf-row combinations");

        foreach (var group in grouped)
        {
            Transform anchor = anchorManager.GetAnchor(group.Key.ShelfName, group.Key.RowNumber);
            if (anchor == null)
            {
                Debug.LogWarning($"No anchor found for {group.Key.ShelfName}-{group.Key.RowNumber}");
                continue;
            }

            Debug.Log($"Processing {group.Key.ShelfName}-{group.Key.RowNumber} with {group.Count()} products");
            
            // Get the max products for this row
            int rowMaxProducts = group.First().MaxProducts;
            if (rowMaxProducts <= 0) 
            {
                rowMaxProducts = maxPerRow;
            }
            
            // Don't exceed the maximum products for this row
            var productsToPlace = group.Take(rowMaxProducts).ToList();
            int overflowCount = group.Count() - productsToPlace.Count;
            
            if (overflowCount > 0)
            {
                Debug.LogWarning($"Too many products for {group.Key.ShelfName}-{group.Key.RowNumber}. Max: {rowMaxProducts}, Found: {group.Count()}. {overflowCount} products not placed.");
            }

            SpawnProductsInRow(anchor, productsToPlace, rowMaxProducts);
        }

        Debug.Log($"Spawned {spawnedProducts.Count} products total");
    }

    private void SpawnProductsInRow(Transform anchor, List<Product> products, int rowMaxProducts)
    {
        for (int i = 0; i < products.Count; i++)
        {
            var product = products[i];
            GameObject prefabToUse = isStore ? GetSpecificPrefabForProduct(product) : warehouseProductPrefab;
            
            if (prefabToUse == null)
            {
                Debug.LogWarning($"No prefab found for {product.ProductName} - {product.Brand} - {product.Size}");
                continue;
            }

            // Calculate position within row
            Vector3 spawnPosition = CalculateSpawnPosition(anchor, i, rowMaxProducts);
            SpawnProduct(prefabToUse, spawnPosition, anchor.rotation, anchor, product);
        }
    }

    private Vector3 CalculateSpawnPosition(Transform anchor, int index, int maxProducts)
    {
        int row = index / maxPerRow;
        int col = index % maxPerRow;
        return anchor.TransformPoint(new Vector3(col * spacingX, 0, row * spacingZ));
    }

    private void SpawnProduct(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, Product product)
    {
        GameObject newProduct = Instantiate(prefab, position, rotation, parent);
        spawnedProducts.Add(newProduct);

        // Setup product label
        SetupProductLabel(newProduct, product);
        
        // Setup interactions (store only)
        SetupProductInteractions(newProduct, product);
    }

    private void SetupProductLabel(GameObject productObj, Product product)
    {
        ProductLabel label = productObj.GetComponent<ProductLabel>();
        if (label != null)
        {
            label.SetLabel(product);
        }
        else
        {
            Debug.LogWarning($"Product prefab {productObj.name} is missing ProductLabel component");
        }
    }

    private void SetupProductInteractions(GameObject productObj, Product product)
    {
        if (isStore)
        {
            ProductInteractions interactions = productObj.GetComponent<ProductInteractions>();
            if (interactions == null) 
            {
                interactions = productObj.AddComponent<ProductInteractions>();
            }
            interactions.Initialize(product, dbManager, this);
        }
        else
        {
            // Remove interaction components from warehouse products
            ProductInteractions interactions = productObj.GetComponent<ProductInteractions>();
            if (interactions != null) 
            {
                Destroy(interactions);
            }
        }
    }

    private GameObject GetSpecificPrefabForProduct(Product product)
    {
        if (product == null) return null;

        // Try exact match first
        string[] searchKeys = {
            $"{product.ProductName.Trim()}_{product.Brand.Trim()}_{product.Size.Trim()}".ToLower(),
            $"{product.ProductName.Trim()}_{product.Brand.Trim()}".ToLower(),
            product.ProductName.Trim().ToLower()
        };

        foreach (string key in searchKeys)
        {
            if (prefabCache.ContainsKey(key))
            {
                return prefabCache[key];
            }
        }

        // Try category mapping
        foreach (var mapping in categoryPrefabMappings)
        {
            if (mapping.category.Equals(product.Category, System.StringComparison.OrdinalIgnoreCase) &&
                mapping.prefabs != null && mapping.prefabs.Count > 0)
            {
                return mapping.prefabs[0];
            }
        }

        Debug.LogWarning($"No prefab found for {product.ProductName} - {product.Brand} - {product.Size} (Category: {product.Category})");
        return null;
    }

    private void ClearSpawnedProducts()
    {
        foreach (GameObject go in spawnedProducts)
        {
            if (go != null) 
            {
                Destroy(go);
            }
        }
        spawnedProducts.Clear();
    }

    public void GenerateHeatmap()
    {
        if (!isStore) return;

        ClearExistingHeatmaps();

        if (heatmapPrefab == null || anchorManager == null || dbManager == null) 
        {
            Debug.LogError("Heatmap generation missing required components");
            return;
        }

        var salesByShelf = dbManager.GetShelfSalesHeatmap();
        CreateHeatmapVisualizations(salesByShelf);
    }

    private void ClearExistingHeatmaps()
    {
        GameObject[] existingHeatmaps = GameObject.FindGameObjectsWithTag("Heatmap");
        foreach (GameObject hm in existingHeatmaps)
        {
            Destroy(hm);
        }
    }

    private void CreateHeatmapVisualizations(Dictionary<string, int> salesByShelf)
    {
        if (salesByShelf.Count == 0)
        {
            CreateDefaultHeatmap();
            return;
        }

        int maxPopularity = salesByShelf.Values.Max();
        if (maxPopularity <= 0) maxPopularity = 1;

        foreach (var kv in salesByShelf)
        {
            CreateShelfHeatmap(kv.Key, kv.Value, maxPopularity);
        }
    }

    private void CreateDefaultHeatmap()
    {
        // Create faint markers for all shelves when no sales data exists
        string[] shelves = { "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "A10", "A11", "A12" };
        
        foreach (string shelf in shelves)
        {
            for (int row = 1; row <= 5; row++)
            {
                CreateHeatmapMarker(shelf, row, new Color(0f, 0.2f, 1f, 0.15f), 0.5f);
            }
        }
    }

    private void CreateShelfHeatmap(string shelf, int salesCount, int maxPopularity)
    {
        float intensity = Mathf.Clamp01((float)salesCount / maxPopularity);
        Color heatColor = new Color(intensity, 0f, 1f - intensity, 0.35f);
        float scale = 0.5f + intensity;

        for (int row = 1; row <= 5; row++)
        {
            CreateHeatmapMarker(shelf, row, heatColor, scale);
        }
    }

    private void CreateHeatmapMarker(string shelf, int row, Color color, float scale)
    {
        Transform anchor = anchorManager.GetAnchor(shelf, row);
        if (anchor == null) return;

        GameObject heatmap = Instantiate(heatmapPrefab, anchor.position, Quaternion.identity);
        heatmap.tag = "Heatmap";
        heatmap.transform.localScale = Vector3.one * scale;

        Renderer rend = heatmap.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = color;
        }
    }

    public void RefreshAll()
    {
        SpawnProducts();
        if (isStore) GenerateHeatmap();
    }
}