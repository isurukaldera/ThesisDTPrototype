using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ProductLabel : MonoBehaviour
{
    public TextMeshPro textMesh;
    public List<Renderer> renderersToColor = new List<Renderer>();
    
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Color currentColor;

    void Start()
    {
        // Store original materials
        foreach (Renderer rend in renderersToColor)
        {
            if (rend != null)
            {
                originalMaterials[rend] = rend.materials;
            }
        }
        
        // Enable rich text to process the color tags
        if (textMesh != null)
        {
            textMesh.richText = true;
        }
    }

    public void SetLabel(Product product)
    {
        if (textMesh == null) return;
        
        // Apply different formatting for store vs warehouse products
        if (product.Location == "Store")
        {
            // Store color coding based on quantity
            if (product.Quantity >= 80)
            {
                currentColor = new Color32(0x95, 0xF5, 0x00, 255); // Green
            }
            else if (product.Quantity >= 50)
            {
                currentColor = new Color32(0xFF, 0xFF, 0x00, 255); // Yellow
            }
            else if (product.Quantity > 15)
            {
                currentColor = new Color32(0xFF, 0x96, 0x00, 255); // Orange
            }
            else
            {
                currentColor = new Color32(0xFF, 0x1A, 0x1A, 255); // Red
            }
            
            // Format the quantity with leading zero for single digits and always use white
            string quantityText = $"Qty: <color=white>{product.Quantity:D2}</color>";
            
            // Include flavor in the display if it exists
            string flavorText = string.IsNullOrEmpty(product.Flavour) ? "" : $" {product.Flavour}";
            
            // Two-line format for store products
            textMesh.text = $"{product.ProductName} {product.Brand}{flavorText} {product.Size}\n{quantityText}";
            
            // Set the main text color
            textMesh.color = currentColor;
            UpdateAllMaterials(currentColor);
        }
        else
        {
            // For warehouse products, include flavor if it exists
            string flavorText = string.IsNullOrEmpty(product.Flavour) ? "" : $"\n{product.Flavour}";
            string quantityText = $"Qty: <color=white>{product.Quantity}</color>";
            
            textMesh.text = $"{product.ProductName}\n{product.Brand}{flavorText}\n{product.Size}\n{quantityText}";
            
            // Warehouse products use neutral colors
            currentColor = Color.gray;
            textMesh.color = Color.white;
            UpdateAllMaterials(currentColor);
        }
    }

    private void UpdateAllMaterials(Color color)
    {
        foreach (Renderer rend in renderersToColor)
        {
            if (rend != null)
            {
                Material[] materials = rend.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    // Create a new material instance to avoid affecting other objects
                    materials[i] = new Material(materials[i]);
                    materials[i].color = color;
                }
                rend.materials = materials;
            }
        }
    }

    // Call this when you want to reset to original materials
    public void ResetMaterials()
    {
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
    }
}