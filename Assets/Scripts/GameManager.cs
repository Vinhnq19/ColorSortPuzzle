using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Game Object Settings")]
    [SerializeField] private GameObject pegPrefab;
    [SerializeField] private GameObject ringPrefab;
    [SerializeField] private List<Peg> currentPegs = new List<Peg>();
    [SerializeField] private GameObject winPanel;
    private Peg sourcePeg = null;
    private List<Ring> selectedRingStack = null;
    public const int MAX_RINGS_PER_PEG = 4;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip ringMoveSound;
    [SerializeField] private AudioClip pegSortedSound;

    private AudioSource audioSource;
    private bool isMuted = false; // Trạng thái tắt tiếng hiện tại

    [Header("Level Selection")]
    [SerializeField] private GameObject levelSelectionPanel;
    [SerializeField] private Button levelButtonPrefab;
    [SerializeField] private Transform levelButtonParent; //GameObject chứa các nút màn chơi(ví dụ: Content của Scroll View/Grid Layout)
    [System.Serializable]
    public class LevelData
    {
        // Tổng số trụ trong cấp độ này
        public int numPegs;

        // Cấu hình ban đầu của các vòng trên mỗi trụ
        // Mỗi List<Ring.RingColor> con đại diện cho một trụ
        // Các vòng được sắp xếp từ dưới lên trên trong List này.
        public List<PegConfig> initialPegConfigs;

        [System.Serializable]
        public class PegConfig
        {
            public List<Ring.RingColor> ringsOnPeg; // Các vòng trên trụ này, từ dưới lên
        }

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
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            Debug.LogError("[GameManager.cs] AudioSource component not found on GameManager!");
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.0f; // 2D sound
        isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;
        audioSource.mute = isMuted;

        // Đảm bảo LevelSelectionPanel và WinPanel ban đầu được ẩn
        if (levelSelectionPanel != null)
        {
            levelSelectionPanel.SetActive(false);
        }
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
    }

    void Start()
    {
        LoadLevel(0);
        currentLevelIndex = 0;
    }

    void Update()
    {
        // Xử lý đầu vào người chơi
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
    public void ShowLevelSelectionPanel()
    {
        if (levelSelectionPanel != null)
        {
            levelSelectionPanel.SetActive(true);
            // Ẩn tất cả các trụ hiện có (nếu có)
            ClearCurrentLevel();
            GenerateLevelButtons(); // Tạo các nút màn chơi mỗi khi hiển thị panel
        }
    }

    public void HideLevelSelectionPanel()
    {
        if (levelSelectionPanel != null)
        {
            levelSelectionPanel.SetActive(false);
        }
    }

    private void GenerateLevelButtons()
    {
        // Xóa các nút cũ để tránh trùng lặp khi Generate lại
        foreach (Transform child in levelButtonParent)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < levels.Count; i++)
        {
            // Tạo một nút mới từ prefab
            Button newButton = Instantiate(levelButtonPrefab, levelButtonParent);
            int levelIndex = i; // Lưu index vào biến cục bộ cho closure
            newButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Level {levelIndex + 1}"; // Đặt text cho nút
            // Gán sự kiện OnClick cho nút
            newButton.onClick.AddListener(() => OnLevelSelected(levelIndex));
        }
        Debug.Log($"[GameManager.cs] Generated {levels.Count} level buttons.");
    }

    public void OnLevelSelected(int levelIndex)
    {
        Debug.Log($"[GameManager.cs] Level {levelIndex + 1} selected.");
        SelectLevel(levelIndex); // Tải màn chơi đã chọn
        HideLevelSelectionPanel(); // Ẩn màn hình chọn màn
    }
    // Hàm public để chọn và tải một màn cụ thể
    public void SelectLevel(int levelIndex)
    {
        if (levelIndex >= 0 && levelIndex < levels.Count)
        {
            currentLevelIndex = levelIndex;
            LoadLevel(currentLevelIndex);
        }
        else
        {
            Debug.LogWarning($"[GameManager.cs] Attempted to select invalid level index: {levelIndex}");
        }
    }
    void LoadLevel(int levelIndex)
    {
        Debug.Log($"[GameManager.cs] Attempting to load level: {levelIndex + 1}");
        if (levelIndex >= levels.Count || levelIndex < 0)
        {
            Debug.LogWarning("[GameManager.cs] Level index out of bounds or no more levels! Displaying game over / win screen.");
            // TODO: Hiển thị màn hình thắng toàn bộ game
            ShowLevelSelectionPanel();
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
        int pegsPerRow = 6;
        float columnSpacing = 2.0f;
        float rowSpacing = -2.0f;
        for (int i = 0; i < levelData.numPegs; i++)
        {
            int rowIndex = i / pegsPerRow;
            int columnIndex = i % pegsPerRow;

            float startX = -(pegsPerRow - 1) * columnSpacing / 2f;
            float xPos = startX + columnIndex * columnSpacing;
            float zPos = rowIndex * rowSpacing;
            Vector3 pegPosition = new Vector3(xPos, 0, zPos);
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
                Debug.LogWarning($"[GameManager.cs] Peg {i + 1} has no initial configuration defined in levelData.initialPegConfigs. It will be empty.");
            }
        }
        Debug.Log($"[GameManager.cs] Level {currentLevelIndex + 1} loaded successfully with {levelData.GetTotalRingsInLevel()} rings on {levelData.numPegs} pegs.");
        // TODO: Cập nhật UI (level number, etc.)
    }


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
                    Destroy(ringToRemove.gameObject);
                }
            }
            // Xóa trụ
            Destroy(peg.gameObject);
        }
        currentPegs.Clear();
        selectedRingStack = null;
        sourcePeg = null;

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
    }


    // Xử lý khi người chơi nhấp vào một trụ
    void HandlePegClick(Peg clickedPeg)
    {
        Debug.Log($"[GameManager.cs] Clicked on Peg: {clickedPeg.name}");
        if (selectedRingStack == null || selectedRingStack == null)
        {
            List<Ring> topColorStack = clickedPeg.GetTopColorStack();
            // Chưa có vòng nào được chọn, thử chọn vòng trên cùng của trụ này
            if (topColorStack.Count > 0)
            {
                selectedRingStack = topColorStack;
                sourcePeg = clickedPeg;
                // Nâng tất cả các vòng trong chuỗi lên một chút để hiển thị đang được chọn
                foreach (Ring r in selectedRingStack)
                {
                    Vector3 currentRingPos = r.transform.position;
                    r.transform.position = new Vector3(currentRingPos.x, currentRingPos.y + 0.7f, currentRingPos.z);
                    // Tiết lộ màu nếu là vòng bí ẩn
                    if (r.GetDisplayColor() == Ring.RingColor.Mystery)
                    {
                        r.RevealMysteryColor();
                    }
                }
                Debug.Log($"[GameManager.cs] Selected a stack of {selectedRingStack.Count} rings from peg '{sourcePeg.name}'.");
                PlaySound(ringMoveSound);
            }
            else
            {
                Debug.Log("[GameManager.cs] Clicked an empty peg or peg with no valid stack to select.");
            }
        }
        else // Đã có vòng được chọn
        {
            if (clickedPeg == sourcePeg)
            {
                // Nhấp lại vào cùng trụ, hủy chọn chuỗi vòng
                foreach (Ring r in selectedRingStack)
                {
                    Vector3 currentRingPos = r.transform.position;
                    r.transform.position = new Vector3(currentRingPos.x, currentRingPos.y - 0.7f, currentRingPos.z);
                }
                selectedRingStack = null;
                sourcePeg = null;
                Debug.Log("[GameManager.cs] Deselected ring stack by clicking on source peg.");
                PlaySound(ringMoveSound, 2.5f);
            }
            else
            {
                // Thử di chuyển chuỗi vòng
                Debug.Log($"[GameManager.cs] Attempting to move stack of {selectedRingStack.Count} rings from {sourcePeg.name} to {clickedPeg.name}");
                TryMoveRingStack(selectedRingStack, sourcePeg, clickedPeg);
            }
        }
    }

    void TryMoveRingStack(List<Ring> ringStackToMove, Peg fromPeg, Peg toPeg)
    {
        // Kiểm tra xem vòng dưới cùng của chuỗi có phải là vòng trên cùng của trụ nguồn không (đảm bảo đúng thứ tự)
        if (fromPeg.GetTopRing() != ringStackToMove[ringStackToMove.Count - 1]) // Vòng trên cùng của stack là vòng cuối cùng trong list
        {
            Debug.LogWarning("[GameManager.cs] Selected ring stack's top ring is not on top of its source peg! Invalid state, resetting selection.");
            foreach (Ring r in ringStackToMove)
            {
                Vector3 currentRingPos = r.transform.position;
                r.transform.position = new Vector3(currentRingPos.x, currentRingPos.y - 0.7f, currentRingPos.z);
            }
            selectedRingStack = null;
            sourcePeg = null;
            return;
        }
        // Lưu lại trạng thái trước khi di chuyển để kiểm tra âm thanh "pegSorted"
        bool wasFromPegSorted = fromPeg.IsSorted();
        bool wasToPegSorted = toPeg.IsSorted();

        // Kiểm tra xem trụ đích có thể nhận toàn bộ chuỗi vòng này không
        if (toPeg.CanAddRingStack(ringStackToMove))
        {
            // Di chuyển chuỗi vòng thành công
            fromPeg.RemoveRingStack(ringStackToMove.Count); // Xóa toàn bộ chuỗi từ trụ nguồn
            toPeg.AddRingStack(ringStackToMove); // Thêm toàn bộ chuỗi vào trụ đích
            PlaySound(ringMoveSound);
            selectedRingStack = null;
            sourcePeg = null;

            // Kiểm tra và phát âm thanh khi một trụ được sắp xếp hoàn chỉnh
            if (!wasFromPegSorted && fromPeg.IsSorted() && fromPeg.rings.Count > 0)
            {
                PlaySound(pegSortedSound, 0.3f);
            }
            if (!wasToPegSorted && toPeg.IsSorted() && toPeg.rings.Count > 0)
            {
                PlaySound(pegSortedSound, 0.3f);
            }
            if (toPeg.rings.Count == 0 && toPeg.rings.Count != MAX_RINGS_PER_PEG)
            {
            }

            CheckWinCondition();
        }
        else
        {
            Debug.Log("[GameManager.cs] Invalid move: Destination peg cannot accept the ring stack. Resetting selection.");
            // Đặt lại vị trí các vòng và hủy chọn
            foreach (Ring r in ringStackToMove)
            {
                Vector3 currentRingPos = r.transform.position;
                r.transform.position = new Vector3(currentRingPos.x, currentRingPos.y - 0.7f, currentRingPos.z);
            }
            selectedRingStack = null;
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

        if (allRequiredColorsSorted && allRingsAccountedFor && enoughSortedPegs)
        {
            Invoke("ShowWinPanelAndPlaySound", 1f);
        }
        else
        {
            Debug.Log("[GameManager.cs] Win condition not met yet.");
        }
    }
    void ShowWinPanelAndPlaySound()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
        PlaySound(winSound, 2.0f);
    }
    private void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
    public void ToggleMute()
    {
        isMuted = !isMuted;
        audioSource.mute = isMuted;
        PlayerPrefs.SetInt("IsMuted", isMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void OnReplayButtonClick()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
        LoadLevel(currentLevelIndex);
    }
    // Hàm này được gọi khi nút "Next Level" được bấm
    public void OnNextLevelButtonClick()
    {
        Debug.Log("[GameManager.cs] Next Level button clicked.");
        if (winPanel != null)
        {
            winPanel.SetActive(false); // Tắt màn hình thắng
        }
        currentLevelIndex++;
        if (currentLevelIndex < levels.Count)
        {
            LoadLevel(currentLevelIndex); // Tải màn tiếp theo
        }
        else
        {
            Debug.Log("[GameManager.cs] No more levels. Game Finished!");
            // Xử lý khi hoàn thành tất cả các màn (ví dụ: hiển thị màn hình chúc mừng cuối game)
            ShowLevelSelectionPanel();
        }
    }
    // Hàm public cho nút "Home" hoặc "Back to Levels"
    public void OnHomeButtonClick()
    {
        Debug.Log("[GameManager.cs] Home button clicked. Showing level selection.");
        if (winPanel != null) // Đảm bảo ẩn winPanel nếu đang hiển thị
        {
            winPanel.SetActive(false);
        }
        ShowLevelSelectionPanel();
    }
}