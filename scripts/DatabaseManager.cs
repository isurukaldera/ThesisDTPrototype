using System.Collections.Generic;
using Mono.Data.Sqlite;
using UnityEngine;
using System.IO;
using System;
using System.Data;

public class DatabaseManager : MonoBehaviour
{
    private string connString;
    
    void Awake()
    {
        string dbPath = Path.Combine(Application.streamingAssetsPath, "digital_twin.db");
        Debug.Log($"Database path: {dbPath}");
        
        if (!File.Exists(dbPath))
        {
            Debug.LogError("Database file not found at: " + dbPath);
            return;
        }
        
        connString = "URI=file:" + dbPath;
        Debug.Log("Database connection string set");
    }

    public List<Product> GetAllStoreStock()
    {
        Debug.Log("Getting all store stock");
        return GetStock(true);
    }

    public List<Product> GetAllWarehouseStock()
    {
        Debug.Log("Getting all warehouse stock");
        return GetStock(false);
    }

    private List<Product> GetStock(bool inStore)
    {
        var list = new List<Product>();
        Debug.Log($"Getting stock for: {(inStore ? "Store" : "Warehouse")}");
        
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                Debug.Log("Database connection opened successfully");
                
                string tablePrefix = inStore ? "Store" : "Warehouse";
                string query;
                
                if (inStore)
                {
                    query = $@"
                        SELECT p.ProductID, p.ProductName, p.Brand, p.Flavour, p.Size, p.Category, p.ReorderThreshold,
                        s.Quantity, sh.ShelfID, sh.ShelfName, r.RowID, r.RowNumber, r.MaxProducts
                        FROM {tablePrefix}Stock s
                        JOIN {tablePrefix}Rows r ON s.RowID = r.RowID
                        JOIN {tablePrefix}Shelves sh ON r.ShelfID = sh.ShelfID
                        JOIN Products p ON s.ProductID = p.ProductID;";
                }
                else
                {
                    query = $@"
                        SELECT p.ProductID, p.ProductName, p.Brand, p.Flavour, p.Size, p.Category, p.ReorderThreshold,
                        s.Quantity, sh.ShelfID, sh.ShelfName, r.RowID, r.RowNumber, 0 as MaxProducts
                        FROM {tablePrefix}Stock s
                        JOIN {tablePrefix}Rows r ON s.RowID = r.RowID
                        JOIN {tablePrefix}Shelves sh ON r.ShelfID = sh.ShelfID
                        JOIN Products p ON s.ProductID = p.ProductID;";
                }
                
                Debug.Log($"Executing query: {query}");
                
                using (var cmd = new SqliteCommand(query, con))
                {
                    using (var rd = cmd.ExecuteReader())
                    {
                        Debug.Log("Reading data from database");
                        int count = 0;
                        
                        while (rd.Read())
                        {
                            count++;
                            var product = new Product
                            {
                                ProductID = rd.GetInt32(0),
                                ProductName = rd.GetString(1),
                                Brand = rd.IsDBNull(2) ? "" : rd.GetString(2),
                                Flavour = rd.IsDBNull(3) ? "" : rd.GetString(3),
                                Size = rd.IsDBNull(4) ? "" : rd.GetString(4),
                                Category = rd.IsDBNull(5) ? "" : rd.GetString(5),
                                ReorderThreshold = rd.IsDBNull(6) ? 20 : rd.GetInt32(6),
                                Quantity = rd.GetInt32(7),
                                ShelfID = rd.GetInt32(8),
                                ShelfName = rd.GetString(9),
                                RowID = rd.GetInt32(10),
                                RowNumber = rd.GetInt32(11),
                                MaxProducts = rd.GetInt32(12),
                                Location = inStore ? "Store" : "Warehouse"
                            };
                            
                            list.Add(product);
                            
                            // Log first few products for debugging
                            if (count <= 3)
                            {
                                Debug.Log($"Product {count}: {product.ProductName} - Qty: {product.Quantity}");
                            }
                        }
                        Debug.Log($"Total products retrieved: {count}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error retrieving stock: {ex.Message}");
            Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
        
        return list;
    }

    public bool RecordSale(int productID, int quantitySold)
    {
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                
                // Get current store stock
                int currentStock = 0;
                int rowID = 0;
                using (var cmd = new SqliteCommand("SELECT Quantity, RowID FROM StoreStock WHERE ProductID = @productID", con))
                {
                    cmd.Parameters.AddWithValue("@productID", productID);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            currentStock = rd.GetInt32(0);
                            rowID = rd.GetInt32(1);
                        }
                        else
                        {
                            Debug.LogError($"Product {productID} not found in store stock");
                            return false;
                        }
                    }
                }

                // Check if we have enough stock
                if (currentStock < quantitySold)
                {
                    Debug.LogError($"Not enough stock to complete sale. Current: {currentStock}, Requested: {quantitySold}");
                    return false;
                }

                // Update the store stock
                using (var cmd = new SqliteCommand("UPDATE StoreStock SET Quantity = Quantity - @quantity WHERE ProductID = @productID", con))
                {
                    cmd.Parameters.AddWithValue("@quantity", quantitySold);
                    cmd.Parameters.AddWithValue("@productID", productID);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    
                    if (rowsAffected == 0)
                    {
                        Debug.LogError("Failed to update store stock");
                        return false;
                    }
                }

                // Record the transaction
                using (var cmd = new SqliteCommand(@"
                    INSERT INTO StockTransactions (ProductID, Source, QuantityChanged, TransactionType) 
                    VALUES (@productID, 'store', @quantitySold, 'sale')", con))
                {
                    cmd.Parameters.AddWithValue("@productID", productID);
                    cmd.Parameters.AddWithValue("@quantitySold", quantitySold);
                    cmd.ExecuteNonQuery();
                }

                // Also record in sales history for AI
                using (var cmd = new SqliteCommand(@"
                    INSERT INTO SalesHistory (ProductID, SaleDate, QuantitySold, DayOfWeek) 
                    VALUES (@productID, date('now'), @quantitySold, @dayOfWeek)", con))
                {
                    cmd.Parameters.AddWithValue("@productID", productID);
                    cmd.Parameters.AddWithValue("@quantitySold", quantitySold);
                    cmd.Parameters.AddWithValue("@dayOfWeek", (int)DateTime.Now.DayOfWeek + 1);
                    cmd.ExecuteNonQuery();
                }

                Debug.Log($"✅ Sale recorded: {quantitySold} units of product {productID}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error recording sale: " + ex.Message);
            return false;
        }
    }

    public bool RestockStore(int productID, int quantity)
    {
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                
                // Check warehouse stock
                int warehouseStock = 0;
                int warehouseRowID = 0;
                using (var cmd = new SqliteCommand("SELECT Quantity, RowID FROM WarehouseStock WHERE ProductID = @productID", con))
                {
                    cmd.Parameters.AddWithValue("@productID", productID);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            warehouseStock = rd.GetInt32(0);
                            warehouseRowID = rd.GetInt32(1);
                        }
                        else
                        {
                            Debug.LogError($"Product {productID} not found in warehouse");
                            return false;
                        }
                    }
                }

                // Check if warehouse has enough stock
                if (warehouseStock < quantity)
                {
                    Debug.LogError($"Not enough stock in warehouse to restock. Available: {warehouseStock}, Requested: {quantity}");
                    return false;
                }

                // Update warehouse stock
                using (var cmd = new SqliteCommand("UPDATE WarehouseStock SET Quantity = Quantity - @quantity WHERE ProductID = @productID", con))
                {
                    cmd.Parameters.AddWithValue("@quantity", quantity);
                    cmd.Parameters.AddWithValue("@productID", productID);
                    cmd.ExecuteNonQuery();
                }

                // Update store stock
                using (var cmd = new SqliteCommand(@"
                    UPDATE StoreStock SET Quantity = Quantity + @quantity, LastRestocked = CURRENT_TIMESTAMP 
                    WHERE ProductID = @productID", con))
                {
                    cmd.Parameters.AddWithValue("@quantity", quantity);
                    cmd.Parameters.AddWithValue("@productID", productID);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    
                    // If no row exists, insert a new one
                    if (rowsAffected == 0)
                    {
                        // Get the first available row for this product's category
                        int firstRowID = GetAvailableStoreRow(productID);
                        
                        using (var insertCmd = new SqliteCommand(
                            "INSERT INTO StoreStock (ProductID, RowID, Quantity) VALUES (@productID, @rowID, @quantity)", con))
                        {
                            insertCmd.Parameters.AddWithValue("@productID", productID);
                            insertCmd.Parameters.AddWithValue("@rowID", firstRowID);
                            insertCmd.Parameters.AddWithValue("@quantity", quantity);
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }

                // Record the transaction
                using (var cmd = new SqliteCommand(@"
                    INSERT INTO StockTransactions (ProductID, Source, Destination, QuantityChanged, TransactionType)
                    VALUES (@productID, 'warehouse', 'store', @quantity, 'restock')", con))
                {
                    cmd.Parameters.AddWithValue("@productID", productID);
                    cmd.Parameters.AddWithValue("@quantity", quantity);
                    cmd.ExecuteNonQuery();
                }

                Debug.Log($"✅ Restocked: {quantity} units of product {productID} from warehouse to store");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error restocking: " + ex.Message);
            return false;
        }
    }

    private int GetAvailableStoreRow(int productID)
    {
        // Simple implementation - get first row with available space
        // You can enhance this with more sophisticated logic
        using (var con = new SqliteConnection(connString))
        {
            con.Open();
            using (var cmd = new SqliteCommand(@"
                SELECT r.RowID FROM StoreRows r 
                WHERE (SELECT COUNT(*) FROM StoreStock s WHERE s.RowID = r.RowID) < r.MaxProducts
                LIMIT 1", con))
            {
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }
            }
        }
        return 1; // Fallback to first row
    }

    public List<Product> GetLowStockProducts()
    {
        var list = new List<Product>();
        
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                using (var cmd = new SqliteCommand(@"
                    SELECT p.ProductID, p.ProductName, p.Brand, p.Flavour, p.Size, p.Category, p.ReorderThreshold,
                    s.Quantity, sh.ShelfID, sh.ShelfName, r.RowID, r.RowNumber, r.MaxProducts
                    FROM StoreStock s
                    JOIN StoreRows r ON s.RowID = r.RowID
                    JOIN StoreShelves sh ON r.ShelfID = sh.ShelfID
                    JOIN Products p ON s.ProductID = p.ProductID
                    WHERE s.Quantity <= p.ReorderThreshold OR s.Quantity <= 20", con))
                {
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var product = new Product
                            {
                                ProductID = rd.GetInt32(0),
                                ProductName = rd.GetString(1),
                                Brand = rd.IsDBNull(2) ? "" : rd.GetString(2),
                                Flavour = rd.IsDBNull(3) ? "" : rd.GetString(3),
                                Size = rd.IsDBNull(4) ? "" : rd.GetString(4),
                                Category = rd.IsDBNull(5) ? "" : rd.GetString(5),
                                ReorderThreshold = rd.IsDBNull(6) ? 20 : rd.GetInt32(6),
                                Quantity = rd.GetInt32(7),
                                ShelfID = rd.GetInt32(8),
                                ShelfName = rd.GetString(9),
                                RowID = rd.GetInt32(10),
                                RowNumber = rd.GetInt32(11),
                                MaxProducts = rd.GetInt32(12),
                                Location = "Store"
                            };
                            list.Add(product);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting low stock products: {ex.Message}");
        }
        
        Debug.Log($"Found {list.Count} low stock products");
        return list;
    }

    public Dictionary<string, int> GetShelfSalesHeatmap()
    {
        var data = new Dictionary<string, int>();
        
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                using (var cmd = new SqliteCommand(@"
                    SELECT sh.ShelfName, COUNT(*) AS SalesCount 
                    FROM StockTransactions t
                    JOIN StoreStock s ON t.ProductID = s.ProductID
                    JOIN StoreRows r ON s.RowID = r.RowID
                    JOIN StoreShelves sh ON r.ShelfID = sh.ShelfID
                    WHERE t.TransactionType = 'sale'
                    GROUP BY sh.ShelfName", con))
                {
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            string shelf = rd.IsDBNull(0) ? "" : rd.GetString(0);
                            int count = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                            data[shelf] = count;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting heatmap data: {ex.Message}");
        }
        
        return data;
    }

    // ========== AI RECOMMENDATION METHODS ==========
    
    public void SaveAIRecommendation(int productId, float predictedDemand, int currentShelf, int currentWarehouse, int recommendedTransfer, int recommendedOrder)
    {
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                using (var cmd = new SqliteCommand(@"
                    INSERT INTO RestockRecommendations 
                    (ProductID, PeriodStart, PeriodEnd, PredictedDemand, CurrentShelfStock, CurrentWarehouseStock, 
                     RecommendedShelfTransfer, RecommendedSupplierOrder, SafetyBuffer, Status)
                    VALUES 
                    (@productId, date('now'), date('now', '+7 days'), @predictedDemand, @currentShelf, @currentWarehouse,
                     @transfer, @order, 0.15, 'pending')", con))
                {
                    cmd.Parameters.AddWithValue("@productId", productId);
                    cmd.Parameters.AddWithValue("@predictedDemand", predictedDemand);
                    cmd.Parameters.AddWithValue("@currentShelf", currentShelf);
                    cmd.Parameters.AddWithValue("@currentWarehouse", currentWarehouse);
                    cmd.Parameters.AddWithValue("@transfer", recommendedTransfer);
                    cmd.Parameters.AddWithValue("@order", recommendedOrder);
                    
                    cmd.ExecuteNonQuery();
                }
            }
            Debug.Log($"Saved AI recommendation for product {productId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving AI recommendation: {ex.Message}");
        }
    }

    public List<AIRecommendation> GetAllAIRecommendations()
    {
        var recommendations = new List<AIRecommendation>();
        
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                using (var cmd = new SqliteCommand(@"
                    SELECT r.RecommendationID, r.ProductID, r.GeneratedDate, r.PredictedDemand,
                           r.CurrentShelfStock, r.CurrentWarehouseStock, r.RecommendedShelfTransfer,
                           r.RecommendedSupplierOrder, r.Status
                    FROM RestockRecommendations r
                    ORDER BY r.GeneratedDate DESC
                    LIMIT 50", con))
                {
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            recommendations.Add(new AIRecommendation
                            {
                                RecommendationID = rd.GetInt32(0),
                                ProductID = rd.GetInt32(1),
                                GeneratedDate = rd.GetDateTime(2),
                                PredictedDemand = Convert.ToSingle(rd.GetDouble(3)),
                                CurrentShelfStock = rd.GetInt32(4),
                                CurrentWarehouseStock = rd.GetInt32(5),
                                RecommendedTransfer = rd.GetInt32(6),
                                RecommendedOrder = rd.GetInt32(7),
                                Status = rd.GetString(8)
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting AI recommendations: {ex.Message}");
        }
        
        return recommendations;
    }

    public Product GetProductByID(int productId)
    {
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                using (var cmd = new SqliteCommand(
                    "SELECT ProductID, ProductName, Brand, Flavour, Size, Category, ReorderThreshold FROM Products WHERE ProductID = @id", con))
                {
                    cmd.Parameters.AddWithValue("@id", productId);
                    
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            return new Product
                            {
                                ProductID = rd.GetInt32(0),
                                ProductName = rd.GetString(1),
                                Brand = rd.IsDBNull(2) ? "" : rd.GetString(2),
                                Flavour = rd.IsDBNull(3) ? "" : rd.GetString(3),
                                Size = rd.IsDBNull(4) ? "" : rd.GetString(4),
                                Category = rd.IsDBNull(5) ? "" : rd.GetString(5),
                                ReorderThreshold = rd.IsDBNull(6) ? 20 : rd.GetInt32(6)
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting product by ID: {ex.Message}");
        }
        
        return null;
    }

    public void AddSampleSalesDataForAI()
    {
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                
                // Clear existing sales data
                using (var cmd = new SqliteCommand("DELETE FROM SalesHistory", con))
                {
                    cmd.ExecuteNonQuery();
                }
                
                // Add sample sales data for the last 30 days
                System.Random random = new System.Random();
                
                for (int i = 0; i < 30; i++)
                {
                    string date = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
                    int dayOfWeek = (int)DateTime.Now.AddDays(-i).DayOfWeek + 1;
                    bool isWeekend = (dayOfWeek == 6 || dayOfWeek == 7);
                    
                    // Add sales for various products (1-50)
                    for (int productId = 1; productId <= 50; productId++)
                    {
                        int baseSales = isWeekend ? 8 : 5; // More sales on weekends
                        int randomVariation = random.Next(-2, 4); // -2 to +3 variation
                        int quantitySold = Math.Max(1, baseSales + randomVariation);
                        
                        using (var cmd = new SqliteCommand(@"
                            INSERT INTO SalesHistory (ProductID, SaleDate, QuantitySold, DayOfWeek, IsHoliday)
                            VALUES (@productId, @date, @quantity, @dayOfWeek, @isHoliday)", con))
                        {
                            cmd.Parameters.AddWithValue("@productId", productId);
                            cmd.Parameters.AddWithValue("@date", date);
                            cmd.Parameters.AddWithValue("@quantity", quantitySold);
                            cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);
                            cmd.Parameters.AddWithValue("@isHoliday", isWeekend ? 1 : 0);
                            
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                
                Debug.Log("Added sample sales data for AI training (30 days of data)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error adding sample sales data: {ex.Message}");
        }
    }

    public (int shelfStock, int warehouseStock) GetStockLevels(int productId)
    {
        int shelfStock = 0;
        int warehouseStock = 0;
        
        try
        {
            using (var con = new SqliteConnection(connString))
            {
                con.Open();
                
                // Get shelf stock
                using (var cmd = new SqliteCommand("SELECT SUM(Quantity) FROM StoreStock WHERE ProductID = @productId", con))
                {
                    cmd.Parameters.AddWithValue("@productId", productId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        shelfStock = Convert.ToInt32(result);
                    }
                }
                
                // Get warehouse stock
                using (var cmd = new SqliteCommand("SELECT SUM(Quantity) FROM WarehouseStock WHERE ProductID = @productId", con))
                {
                    cmd.Parameters.AddWithValue("@productId", productId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        warehouseStock = Convert.ToInt32(result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting stock levels: {ex.Message}");
        }
        
        return (shelfStock, warehouseStock);
    }
}