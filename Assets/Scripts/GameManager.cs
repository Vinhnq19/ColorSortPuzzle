using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject pegPrefab;
    [SerializeField] private GameObject ringPrefab;

    // Danh sách các trụ trong cấp độ hiện tại
    [SerializeField] private List<Peg> currentPegs = new List<Peg>();

    // Vòng đang được chọn bởi người chơi
    private Ring selectedRing = null;
    // Trụ mà vòng đang được chọn từ đó
    private Peg sourcePeg = null;

    // Const cho số vòng tối đa trên mỗi trụ. Đây là giới hạn cứng của trò chơi.
    public const int MAX_RINGS_PER_PEG = 4; // Mỗi cột chỉ chứa tối đa 4 vòng

    // Định nghĩa cấu trúc cho mỗi cấp độ với cấu hình chi tiết
    [System.Serializable]
    public class LevelData
    {
        public int numPegs; // Tổng số trụ trong cấp độ này
        // Cấu hình ban đầu của các vòng trên mỗi trụ
        // Mỗi List<Ring.RingColor> con đại diện cho một trụ
        // Các vòng được sắp xếp từ dưới lên trên trong List này.
        public List<PegConfig> initialPegConfigs; // Sử dụng một class riêng để có thể hiển thị trong Inspector

        [System.Serializable]
        public class PegConfig
        {
            public List<Ring.RingColor> ringsOnPeg; // Các vòng trên trụ này, từ dưới lên
        }

        // Cấu hình cho vòng bí ẩn
        public List<MysteryRingConfig> mysteryConfigs;

        [System.Serializable]
        public class MysteryRingConfig
        {
            public int pegIndex; // Index của trụ (0-based)
            public int ringIndex; // Index của vòng trên trụ đó (0-based từ đáy)
            public Ring.RingColor actualColor; // Màu thực của vòng bí ẩn
        }

        // Helper để tính toán số màu và số vòng tổng cộng
        public int GetTotalColorsInLevel()
        {
            HashSet<Ring.RingColor> uniqueColors = new HashSet<Ring.RingColor>();
            foreach (var pegConfig in initialPegConfigs)
            {
                foreach (var ringColor in pegConfig.ringsOnPeg)
                {
                    if (ringColor != Ring.RingColor.Mystery && ringColor != Ring.RingColor.White)
                    {
                        uniqueColors.Add(ringColor);
                    }
                }
            }
            // Thêm các màu thực từ mysteryConfigs
            foreach (var mystery in mysteryConfigs)
            {
                if (mystery.actualColor != Ring.RingColor.White && mystery.actualColor != Ring.RingColor.Mystery)
                {
                    uniqueColors.Add(mystery.actualColor);
                }
            }
            return uniqueColors.Count;
        }

        public int GetTotalRingsInLevel()
        {
            int total = 0;
            foreach (var pegConfig in initialPegConfigs)
            {
                total += pegConfig.ringsOnPeg.Count;
            }
            return total;
        }
    }

    public List<LevelData> levels = new List<LevelData>();
    public int currentLevelIndex = 0;

    void Start()
    {
        LoadLevel(currentLevelIndex);
    }

    void Update()
{
    // Xử lý đầu vào người chơi (chạm/nhấp chuột)
    // Nếu có chạm trên màn hình (dành cho thiết bị di động)
    if (Input.touchCount > 0) 
    {
        Touch firstTouch = Input.GetTouch(0); 
        if (firstTouch.phase == TouchPhase.Began) 
        {
            HandleInputRaycast(firstTouch.position);
        }
    }
    // Nếu có nhấp chuột trái (dành cho PC/Editor)
    else if (Input.GetMouseButtonDown(0)) 
    {
        HandleInputRaycast(Input.mousePosition); 
    }
}

void HandleInputRaycast(Vector3 screenPosition)
{
    Ray ray = Camera.main.ScreenPointToRay(screenPosition);
    RaycastHit hit;

    if (Physics.Raycast(ray, out hit))
    {
        Peg hitPeg = hit.collider.GetComponentInParent<Peg>(); 

        if (hitPeg != null)
        {
            HandlePegClick(hitPeg);
        }
        else
        {
            Debug.Log($"[GameManager.cs] Clicked on an object ({hit.collider.name}) that is not a Peg or child of a Peg.");
        }
    }
    else
    {
        Debug.Log("[GameManager.cs] No object clicked by Raycast.");
    }
}
    void LoadLevel(int levelIndex)
    {
        Debug.Log($"[GameManager.cs] Attempting to load level: {levelIndex + 1}");
        if (levelIndex >= levels.Count || levelIndex < 0)
        {
            Debug.LogWarning("[GameManager.cs] Level index out of bounds or no more levels! Displaying game over / win screen.");
            // TODO: Hiển thị màn hình thắng toàn bộ game
            return;
        }

        ClearCurrentLevel();

        LevelData levelData = levels[levelIndex];

        // Kiểm tra tính hợp lệ của cấu hình cấp độ
        if (levelData.initialPegConfigs.Count != levelData.numPegs)
        {
            Debug.LogError($"[GameManager.cs] Level {levelIndex + 1} configuration error: numPegs ({levelData.numPegs}) does not match initialPegConfigs count ({levelData.initialPegConfigs.Count}). Please fix in Inspector.");
            return;
        }

        // Khởi tạo các trụ
        float pegSpacing = 2.0f; // Khoảng cách giữa các trụ
        for (int i = 0; i < levelData.numPegs; i++)
        {
            Vector3 pegPosition = new Vector3(i * pegSpacing - (levelData.numPegs - 1) * pegSpacing / 2f, 0, 0);
            GameObject newPegObj = Instantiate(pegPrefab, pegPosition, Quaternion.identity);
            newPegObj.name = $"Peg_{i + 1}"; // Đặt tên để dễ debug
            Peg newPeg = newPegObj.GetComponent<Peg>();
            newPeg.SetMaxRingsAllowed(MAX_RINGS_PER_PEG); // Đặt giới hạn cho trụ
            currentPegs.Add(newPeg);
            Debug.Log($"[GameManager.cs] Created Peg {newPegObj.name} at {pegPosition}");

            // Đặt cấu hình ban đầu cho các vòng trên trụ này
            if (i < levelData.initialPegConfigs.Count)
            {
                List<Ring.RingColor> ringsConfigForThisPeg = levelData.initialPegConfigs[i].ringsOnPeg;

                // Lặp lại từ dưới lên (theo thứ tự trong List để đảm bảo Stack hoạt động đúng)
                for (int j = 0; j < ringsConfigForThisPeg.Count; j++)
                {
                    if (newPeg.rings.Count >= MAX_RINGS_PER_PEG)
                    {
                        Debug.LogWarning($"[GameManager.cs] Peg {newPeg.name} already reached max rings ({MAX_RINGS_PER_PEG}) during initialization. Skipping remaining rings for this peg from config.");
                        break; // Dừng nếu trụ đã đầy
                    }

                    Ring.RingColor ringConfigColor = ringsConfigForThisPeg[j];
                    GameObject newRingObj = Instantiate(ringPrefab, Vector3.zero, Quaternion.identity); // Vị trí tạm thời
                    newRingObj.name = $"Ring_Peg{i + 1}_Pos{j + 1}_{ringConfigColor}"; // Đặt tên

                    Ring newRing = newRingObj.GetComponent<Ring>();

                    Ring.RingColor actualColor = Ring.RingColor.White; // Mặc định nếu không phải Mystery
                    if (ringConfigColor == Ring.RingColor.Mystery)
                    {
                        // Tìm màu thực cho vòng bí ẩn này từ mysteryConfigs
                        bool foundMysteryConfig = false;
                        foreach (var mysteryConfig in levelData.mysteryConfigs)
                        {
                            if (mysteryConfig.pegIndex == i && mysteryConfig.ringIndex == j)
                            {
                                actualColor = mysteryConfig.actualColor;
                                foundMysteryConfig = true;
                                Debug.Log($"[GameManager.cs] Assigned mystery config: Peg {i}, Ring Index {j}, Actual Color {actualColor}");
                                break;
                            }
                        }
                        if (!foundMysteryConfig)
                        {
                            Debug.LogWarning($"[GameManager.cs] Mystery ring at Peg {i}, Ring Index {j} has no corresponding actual color in mysteryConfigs! Defaulting to White.");
                            actualColor = Ring.RingColor.White; // Fallback nếu không tìm thấy cấu hình
                        }
                    }

                    newRing.InitializeColor(ringConfigColor, actualColor); // Khởi tạo màu cho vòng
                    currentPegs[i].AddRing(newRing); // Thêm vòng vào trụ
                    Debug.Log($"[GameManager.cs] Added ring {newRing.name} (Display: {newRing.GetDisplayColor()}, Actual: {newRing.GetActualColor()}) to Peg {currentPegs[i].name}");
                }
            }
            else
            {
                Debug.LogWarning($"[GameManager.cs] Peg {i+1} has no initial configuration defined in levelData.initialPegConfigs. It will be empty.");
            }
        }
        Debug.Log($"[GameManager.cs] Level {currentLevelIndex + 1} loaded successfully with {levelData.GetTotalRingsInLevel()} rings on {levelData.numPegs} pegs.");
        // TODO: Cập nhật UI (level number, etc.)
    }

    // Hàm xóa tất cả các đối tượng của cấp độ hiện tại
    void ClearCurrentLevel()
    {
        Debug.Log("[GameManager.cs] Clearing current level objects.");
        foreach (Peg peg in currentPegs)
        {
            // Xóa tất cả vòng trên trụ
            while (peg.rings.Count > 0)
            {
                Ring ringToRemove = peg.RemoveRing();
                if (ringToRemove != null)
                {
                    Destroy(ringToRemove.gameObject); // Dùng DestroyImmediate trong Editor mode để đảm bảo xóa ngay
                }
            }
            // Xóa trụ
            Destroy(peg.gameObject); // Dùng DestroyImmediate trong Editor mode để đảm bảo xóa ngay
        }
        currentPegs.Clear();
        selectedRing = null;
        sourcePeg = null;
        Debug.Log("[GameManager.cs] Current level cleared.");
    }

    // Xử lý khi người chơi nhấp vào một trụ
    void HandlePegClick(Peg clickedPeg)
    {
        Debug.Log($"[GameManager.cs] Clicked on Peg: {clickedPeg.name}");
        if (selectedRing == null)
        {
            // Chưa có vòng nào được chọn, thử chọn vòng trên cùng của trụ này
            if (clickedPeg.rings.Count > 0)
            {
                selectedRing = clickedPeg.GetTopRing();
                sourcePeg = clickedPeg;
                // Nâng vòng lên một chút để hiển thị đang được chọn
                Vector3 currentRingPos = selectedRing.transform.position;
                selectedRing.transform.position = new Vector3(currentRingPos.x, currentRingPos.y + 0.2f, currentRingPos.z);
                
                // Tiết lộ màu nếu đó là vòng bí ẩn ngay khi được chọn
                if (selectedRing.GetDisplayColor() == Ring.RingColor.Mystery)
                {
                    selectedRing.RevealMysteryColor();
                }
                Debug.Log($"[GameManager.cs] Selected ring: {selectedRing.name} (Display Color: {selectedRing.GetDisplayColor()}, Actual Color: {selectedRing.GetActualColor()}) from peg: {sourcePeg.name}.");
            }
            else
            {
                Debug.Log("[GameManager.cs] Clicked an empty peg, no ring to select.");
            }
        }
        else // Đã có vòng được chọn
        {
            if (clickedPeg == sourcePeg)
            {
                // Nhấp lại vào cùng trụ, hủy chọn vòng
                Vector3 currentRingPos = selectedRing.transform.position;
                selectedRing.transform.position = new Vector3(currentRingPos.x, currentRingPos.y - 0.2f, currentRingPos.z);
                selectedRing = null;
                sourcePeg = null;
                Debug.Log("[GameManager.cs] Deselected ring by clicking on source peg.");
            }
            else
            {
                // Thử di chuyển vòng
                Debug.Log($"[GameManager.cs] Attempting to move {selectedRing.name} from {sourcePeg.name} to {clickedPeg.name}");
                TryMoveRing(selectedRing, sourcePeg, clickedPeg);
            }
        }
    }

    void TryMoveRing(Ring ringToMove, Peg fromPeg, Peg toPeg)
    {
        // Kiểm tra xem vòng được chọn có phải là vòng trên cùng của trụ nguồn không
        if (fromPeg.GetTopRing() != ringToMove)
        {
            Debug.LogWarning("[GameManager.cs] Selected ring is not on top of its source peg! Invalid state, resetting selection.");
            // Đặt lại vị trí vòng và hủy chọn
            Vector3 currentRingPos = ringToMove.transform.position;
            ringToMove.transform.position = new Vector3(currentRingPos.x, currentRingPos.y - 0.2f, currentRingPos.z);
            selectedRing = null;
            sourcePeg = null;
            return;
        }

        // Kiểm tra xem trụ đích có thể nhận vòng này không (bao gồm cả giới hạn 4 vòng)
        if (toPeg.CanAddRing(ringToMove))
        {
            // Di chuyển vòng thành công
            fromPeg.RemoveRing();
            toPeg.AddRing(ringToMove);
            Debug.Log($"[GameManager.cs] Successfully moved {ringToMove.name} (Display: {ringToMove.GetDisplayColor()}, Actual: {ringToMove.GetActualColor()}) from Peg {fromPeg.name} to Peg {toPeg.name}");

            selectedRing = null;
            sourcePeg = null;

            CheckWinCondition();
        }
        else
        {
            Debug.Log("[GameManager.cs] Invalid move: Destination peg cannot accept the ring. Resetting selection.");
            // Đặt lại vị trí vòng và hủy chọn
            Vector3 currentRingPos = ringToMove.transform.position;
            ringToMove.transform.position = new Vector3(currentRingPos.x, currentRingPos.y - 0.2f, currentRingPos.z);
            selectedRing = null;
            sourcePeg = null;
        }
    }

    // Kiểm tra điều kiện thắng cuộc
    void CheckWinCondition()
    {
        Debug.Log("[GameManager.cs] Checking win condition...");
        LevelData currentLevelData = levels[currentLevelIndex];
        int totalColorsExpected = currentLevelData.GetTotalColorsInLevel();
        int totalRingsExpected = currentLevelData.GetTotalRingsInLevel();

        int sortedPegsCount = 0;
        int totalRingsOnSortedPegs = 0;
        HashSet<Ring.RingColor> sortedColorsFound = new HashSet<Ring.RingColor>();

        foreach (Peg peg in currentPegs)
        {
            if (peg.rings.Count == 0)
            {
                // Trụ rỗng luôn được coi là đã "sắp xếp"
                continue;
            }

            if (peg.IsSorted())
            {
                Ring.RingColor actualColorOfSortedPeg = peg.rings.Peek().GetActualColor();
                sortedPegsCount++;
                totalRingsOnSortedPegs += peg.rings.Count;
                sortedColorsFound.Add(actualColorOfSortedPeg);
                Debug.Log($"[GameManager.cs] Peg {peg.name} is perfectly sorted with color {actualColorOfSortedPeg}. Contains {peg.rings.Count} rings.");
            }
            else
            {
                Debug.Log($"[GameManager.cs] Peg {peg.name} is NOT perfectly sorted.");
            }
        }

        // Điều kiện thắng:
        // 1. Số lượng màu độc nhất được tìm thấy trên các trụ đã sắp xếp phải bằng tổng số màu mong đợi.
        // 2. Tổng số vòng trên các trụ đã sắp xếp phải bằng tổng số vòng mong đợi trong cấp độ.
        // 3. Số lượng trụ đã sắp xếp phải đủ để chứa tất cả các màu (ít nhất bằng số lượng màu).

        bool allRequiredColorsSorted = sortedColorsFound.Count == totalColorsExpected;
        bool allRingsAccountedFor = totalRingsOnSortedPegs == totalRingsExpected;
        bool enoughSortedPegs = sortedPegsCount >= totalColorsExpected;

        Debug.Log($"[GameManager.cs] Win Check Summary: Total Colors Expected: {totalColorsExpected}, Total Rings Expected: {totalRingsExpected}");
        Debug.Log($"[GameManager.cs] Sorted Pegs Count: {sortedPegsCount}, Rings on Sorted Pegs: {totalRingsOnSortedPegs}, Sorted Colors Found: {string.Join(", ", sortedColorsFound)}");
        Debug.Log($"[GameManager.cs] All Required Colors Sorted: {allRequiredColorsSorted}, All Rings Accounted For: {allRingsAccountedFor}, Enough Sorted Pegs: {enoughSortedPegs}");

        if (allRequiredColorsSorted && allRingsAccountedFor && enoughSortedPegs)
        {
            Debug.Log("[GameManager.cs] CONGRATULATIONS! Level Cleared!");
            currentLevelIndex++;
            Invoke("LoadNextLevel", 2f); // Load cấp độ tiếp theo sau 2 giây
        }
        else
        {
            Debug.Log("[GameManager.cs] Win condition not met yet.");
        }
    }

    void LoadNextLevel()
    {
        LoadLevel(currentLevelIndex);
    }
}

// Hàm mở rộng để xáo trộn danh sách (nếu bạn muốn tạo cấp độ ngẫu nhiên thay vì config cố định)
public static class ListExtensions
{
    public static void Shuffle<T>(this IList<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}