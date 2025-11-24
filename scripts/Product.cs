using System;

[System.Serializable]
public class Product
{
    public int ProductID;
    public string ProductName;
    public string Brand;
    public string Flavour;
    public string Size;
    public string Category;
    public int ReorderThreshold;
    public int Quantity;
    public int ShelfID;
    public string ShelfName;
    public int RowID;
    public int RowNumber;
    public int MaxProducts;
    public string Location;
    public DateTime LastRestocked;
}

[System.Serializable]
public class AIRecommendation
{
    public int RecommendationID;
    public int ProductID;
    public DateTime GeneratedDate;
    public float PredictedDemand;
    public int CurrentShelfStock;
    public int CurrentWarehouseStock;
    public int RecommendedTransfer;
    public int RecommendedOrder;
    public string Status;
    
    // NEW: Confidence tracking fields
    public float ConfidenceScore;
    public string ConfidenceLevel;
    
    // Helper properties for display
    public string ProductName { get; set; }
    public string Brand { get; set; }
    public string Size { get; set; }
}