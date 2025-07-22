using UnityEngine;
using System.Collections.Generic;
// using static Ring; // Không cần thiết nếu bạn dùng Ring.RingColor

public class Peg : MonoBehaviour
{
    //Stack
    public Stack<Ring> rings = new Stack<Ring>();
    [SerializeField] private Transform basePosition;
    [SerializeField] private float ringHeightOffset = 0.5f;
    private int maxRingsAllowed; // Biến này sẽ được GameManager gán giá trị

    void Awake()
    {
        if(basePosition == null)
        {
            // Tạo một GameObject con làm basePosition nếu chưa có
            GameObject baseObj = new GameObject("BasePosition");
            baseObj.transform.SetParent(transform);
            baseObj.transform.localPosition = new Vector3(0, 0, 0);
            basePosition = baseObj.transform;
        }
        // maxRingsAllowed sẽ được set từ GameManager
    }

    public void SetMaxRingsAllowed(int max)
    {
        maxRingsAllowed = max;
        Debug.Log($"[Peg.cs] Peg '{name}' max rings set to: {maxRingsAllowed}");
    }

    // Hàm thêm vòng vào trụ
    public void AddRing(Ring ring)
    {
        if (rings.Count >= maxRingsAllowed)
        {
            Debug.LogWarning($"[Peg.cs] Peg '{name}' is full! Cannot add more rings (max: {maxRingsAllowed}).");
            return;
        }

        rings.Push(ring);
        // Đặt vòng vào vị trí trên cùng của trụ
        Vector3 targetPosition = basePosition.position + Vector3.up * (rings.Count - 1) * ringHeightOffset;
        ring.transform.position = targetPosition;
        ring.transform.SetParent(transform); // Gán vòng làm con của trụ để dễ quản lý
        Debug.Log($"[Peg.cs] Ring '{ring.name}' added to peg '{name}'. Current rings on peg: {rings.Count}");
    }

    // Hàm lấy vòng từ trụ (vòng trên cùng)
    public Ring RemoveRing()
    {
        if (rings.Count > 0)
        {
            Ring topRing = rings.Pop();
            topRing.transform.SetParent(null); // Bỏ gán vòng khỏi trụ khi di chuyển
            Debug.Log($"[Peg.cs] Ring '{topRing.name}' removed from peg '{name}'. Remaining rings: {rings.Count}");
            return topRing;
        }
        Debug.LogWarning($"[Peg.cs] Attempted to remove ring from empty peg '{name}'!");
        return null;
    }

    // Hàm xem vòng trên cùng mà không lấy ra
    public Ring GetTopRing()
    {
        if (rings.Count > 0)
        {
            return rings.Peek();
        }
        return null;
    }

    // Hàm kiểm tra xem trụ có thể nhận một vòng cụ thể hay không
    // Logic này sẽ được đơn giản hóa và kiểm tra màu thực của vòng
    public bool CanAddRing(Ring ringToAdd)
    {
        if (rings.Count >= maxRingsAllowed)
        {
            Debug.Log($"[Peg.cs] CanAddRing: Peg '{name}' is full. Cannot add ring '{ringToAdd.name}'.");
            return false;
        }

        if (rings.Count == 0)
        {
            Debug.Log($"[Peg.cs] CanAddRing: Peg '{name}' is empty. Can add any ring.");
            return true; // Trụ trống thì có thể thêm bất kỳ vòng nào
        }

        // Vòng trên cùng của trụ hiện tại
        Ring topRingOnPeg = GetTopRing();
        Debug.Log($"[Peg.cs] CanAddRing: Top ring on '{name}' is '{topRingOnPeg.name}' (Actual Color: {topRingOnPeg.GetActualColor()}). Ring to add is '{ringToAdd.name}' (Actual Color: {ringToAdd.GetActualColor()}).");

        // Quy tắc: Chỉ có thể thêm vòng nếu màu thực sự của nó trùng với màu thực sự của vòng trên cùng của trụ đích
        // (Hoặc nếu trụ đích trống, đã kiểm tra ở trên)
        return ringToAdd.GetActualColor() == topRingOnPeg.GetActualColor();
    }

    // Hàm kiểm tra xem trụ đã hoàn thành (chỉ chứa một màu duy nhất và không phải màu bí ẩn)
    public bool IsSorted()
    {
        if (rings.Count == 0)
        {
            return true; // Trụ rỗng được coi là đã sắp xếp (không có gì để sắp xếp)
        }

        Ring.RingColor firstRingActualColor = rings.Peek().GetActualColor(); // Lấy màu thực của vòng trên cùng

        if (firstRingActualColor == Ring.RingColor.White || firstRingActualColor == Ring.RingColor.Mystery) // 'White' ở đây có thể dùng thay cho 'None' nếu bạn không có màu 'None' rõ ràng
        {
            // Trụ chứa vòng chưa có màu cụ thể hoặc vẫn là mystery, không coi là đã sắp xếp hoàn chỉnh
            Debug.Log($"[Peg.cs] Peg '{name}' is not sorted: Top ring is {firstRingActualColor}.");
            return false;
        }

        foreach (Ring ring in rings)
        {
            if (ring.GetActualColor() != firstRingActualColor)
            {
                Debug.Log($"[Peg.cs] Peg '{name}' is not sorted: Contains mixed colors (found {ring.GetActualColor()} different from {firstRingActualColor}).");
                return false; // Có vòng khác màu thực
            }
        }
        Debug.Log($"[Peg.cs] Peg '{name}' is sorted with color {firstRingActualColor}.");
        return true; // Tất cả vòng đều cùng màu thực
    }
}