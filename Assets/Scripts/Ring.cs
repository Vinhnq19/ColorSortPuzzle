using UnityEngine;

public class Ring : MonoBehaviour
{
    public enum RingColor { White, Red, Blue, Green, Yellow, Orange, Mystery }

    public RingColor currentColor; // Màu hiển thị hiện tại của vòng
    private RingColor _actualColorIfMystery; // Màu thực sự của vòng nếu nó là Mystery

    private Renderer ringRenderer; // Renderer để thay đổi màu

    // Các Material cho từng màu. Kéo thả các Material đã tạo vào đây trong Inspector của Prefab Ring.
    public Material mysteryMaterial;
    public Material redMaterial;
    public Material blueMaterial;
    public Material greenMaterial;
    public Material yellowMaterial;
    public Material orangeMaterial;
    public Material whiteMaterial; // Thêm material cho màu White (mặc định nếu không có màu cụ thể)

    void Awake()
    {
        ringRenderer = GetComponent<Renderer>();
        if (ringRenderer == null)
        {
            Debug.LogError("Ring object is missing a Renderer component!", this);
        }
    }

    // Hàm khởi tạo màu cho vòng
    // Nếu là Mystery, cần truyền vào màu thực sự của nó
    public void InitializeColor(RingColor color, RingColor actualColorForMystery = RingColor.White) // Mặc định là White nếu không phải Mystery
    {
        Debug.Log($"[Ring.cs] Initializing ring: Desired Color = {color}, Actual Color If Mystery = {actualColorForMystery}");
        currentColor = color;

        if (color == RingColor.Mystery)
        {
            _actualColorIfMystery = actualColorForMystery; // Lưu màu thực
            ApplyMaterial(mysteryMaterial); // Áp dụng material dấu hỏi
        }
        else
        {
            ApplyMaterialByColor(color); // Áp dụng material màu cụ thể
        }
        Debug.Log($"[Ring.cs] Ring initialized. Current (display): {currentColor}, Actual (if mystery): {_actualColorIfMystery}");
    }

    // Hàm áp dụng material dựa trên Enum RingColor
    private void ApplyMaterialByColor(RingColor color)
    {
        Material materialToApply = null;
        switch (color)
        {
            case RingColor.Red:
                materialToApply = redMaterial;
                break;
            case RingColor.Blue:
                materialToApply = blueMaterial;
                break;
            case RingColor.Green:
                materialToApply = greenMaterial;
                break;
            case RingColor.Yellow:
                materialToApply = yellowMaterial;
                break;
            case RingColor.Orange:
                materialToApply = orangeMaterial;
                break;
            case RingColor.White:
                materialToApply = whiteMaterial;
                break;
            case RingColor.Mystery: // Mystery đã được xử lý riêng
                materialToApply = mysteryMaterial;
                break;
            default:
                Debug.LogWarning($"[Ring.cs] No material configured for color: {color}. Using default white.", this);
                materialToApply = whiteMaterial; // Fallback
                break;
        }
        ApplyMaterial(materialToApply);
    }

    // Hàm áp dụng một material cụ thể
    private void ApplyMaterial(Material mat)
    {
        if (ringRenderer != null && mat != null)
        {
            ringRenderer.material = mat;
            Debug.Log($"[Ring.cs] Applied material: {mat.name}");
        }
        else if (mat == null)
        {
            Debug.LogWarning(" [Ring.cs] Attempted to apply a null material to ring!", this);
        }
    }

    // Hàm tiết lộ màu thực của vòng bí ẩn
    public void RevealMysteryColor()
    {
        if (currentColor == RingColor.Mystery && _actualColorIfMystery != RingColor.White) // Sửa None thành White cho khớp Enum
        {
            Debug.Log($"[Ring.cs] Revealing mystery ring. Actual color: {_actualColorIfMystery}");
            currentColor = _actualColorIfMystery; // Cập nhật màu hiển thị thành màu thực
            ApplyMaterialByColor(currentColor); // Áp dụng material của màu thực
            // Không xóa _actualColorIfMystery, giữ lại để GetActualColor() vẫn hoạt động đúng
            // TODO: Thêm hiệu ứng âm thanh/hình ảnh khi tiết lộ
        }
    }

    // Lấy màu hiển thị (sẽ là màu Mystery nếu chưa tiết lộ)
    public RingColor GetDisplayColor()
    {
        return currentColor;
    }

    // Lấy màu thực sự của vòng (hữu ích cho logic game khi kiểm tra màu thực của Mystery)
    public RingColor GetActualColor()
    {
        if (currentColor == RingColor.Mystery)
        {
            return _actualColorIfMystery;
        }
        return currentColor;
    }
}