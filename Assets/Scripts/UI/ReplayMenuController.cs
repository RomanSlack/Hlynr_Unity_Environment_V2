using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Replay;

/// <summary>
/// Controls the replay selection menu UI.
/// Press Tab to toggle, Escape to close, Enter to load selected replay.
/// </summary>
[AddComponentMenu("Simulation/UI/Replay Menu Controller")]
public sealed class ReplayMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The UIDocument containing the replay menu")]
    public UIDocument menuDocument;

    [Header("Settings")]
    [Tooltip("Key to toggle the replay menu")]
    public Key toggleKey = Key.Tab;

    [Header("References")]
    [Tooltip("Reference to the ReplayDirector for hot-swapping replays")]
    public ReplayDirector replayDirector;

    // UI Elements
    private VisualElement overlay;
    private VisualElement cardContainer;
    private TextField searchField;
    private Label replayCount;
    private Label selectedInfo;
    private Button loadButton;
    private Button closeButton;
    private Button filterAll;
    private Button filterIntercepted;
    private Button filterMiss;

    // State
    private bool isVisible = false;
    private List<ReplayMetadata> allReplays = new List<ReplayMetadata>();
    private List<ReplayMetadata> filteredReplays = new List<ReplayMetadata>();
    private ReplayMetadata selectedReplay = null;
    private VisualElement selectedCard = null;
    private string currentFilter = "all";
    private string searchQuery = "";

    void Awake()
    {
        Debug.Log("[ReplayMenuController] Awake called");

        if (menuDocument == null)
            menuDocument = GetComponent<UIDocument>();

        if (replayDirector == null)
            replayDirector = FindObjectOfType<ReplayDirector>();

        Debug.Log($"[ReplayMenuController] menuDocument={menuDocument != null}, replayDirector={replayDirector != null}");
    }

    void Start()
    {
        Debug.Log("[ReplayMenuController] Start called");

        // Set high sort order to ensure menu is always on top
        if (menuDocument != null)
        {
            menuDocument.sortingOrder = 1000;
        }

        SetupUI();
        HideMenu();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Toggle menu with Tab
        if (keyboard[toggleKey].wasPressedThisFrame)
        {
            Debug.Log("[ReplayMenuController] Tab pressed, toggling menu");
            ToggleMenu();
        }

        // Load with Enter (when menu is open and replay selected)
        if (isVisible && keyboard.enterKey.wasPressedThisFrame && selectedReplay != null)
        {
            LoadSelectedReplay();
        }
    }

    void SetupUI()
    {
        if (menuDocument == null)
        {
            Debug.LogError("[ReplayMenuController] No UIDocument assigned!");
            return;
        }

        var root = menuDocument.rootVisualElement;
        Debug.Log($"[ReplayMenuController] rootVisualElement={root != null}, childCount={root?.childCount ?? 0}");

        overlay = root.Q<VisualElement>("replay-menu-overlay");
        cardContainer = root.Q<VisualElement>("card-container");
        searchField = root.Q<TextField>("search-field");
        replayCount = root.Q<Label>("replay-count");
        selectedInfo = root.Q<Label>("selected-info");
        loadButton = root.Q<Button>("load-btn");
        closeButton = root.Q<Button>("close-btn");
        filterAll = root.Q<Button>("filter-all");
        filterIntercepted = root.Q<Button>("filter-intercepted");
        filterMiss = root.Q<Button>("filter-miss");

        Debug.Log($"[ReplayMenuController] UI Elements found: overlay={overlay != null}, cardContainer={cardContainer != null}, loadButton={loadButton != null}");

        if (overlay == null)
        {
            Debug.LogError("[ReplayMenuController] Could not find 'replay-menu-overlay' element! Check that ReplayMenu.uxml is assigned to UIDocument.");
        }

        // Event bindings
        closeButton?.RegisterCallback<ClickEvent>(evt => HideMenu());
        loadButton?.RegisterCallback<ClickEvent>(evt => LoadSelectedReplay());

        // Search field
        if (searchField != null)
        {
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchQuery = evt.newValue ?? "";
                ApplyFilters();
            });
        }

        // Filter buttons
        filterAll?.RegisterCallback<ClickEvent>(evt => SetFilter("all"));
        filterIntercepted?.RegisterCallback<ClickEvent>(evt => SetFilter("intercepted"));
        filterMiss?.RegisterCallback<ClickEvent>(evt => SetFilter("miss"));

        // Subscribe to replay changes
        if (replayDirector != null)
        {
            replayDirector.OnReplayChanged += OnReplayChanged;
        }

        // Initially disable load button
        if (loadButton != null)
        {
            loadButton.SetEnabled(false);
        }
    }

    void SetFilter(string filter)
    {
        currentFilter = filter;
        UpdateFilterButtonStates();
        ApplyFilters();
    }

    void UpdateFilterButtonStates()
    {
        filterAll?.RemoveFromClassList("active");
        filterIntercepted?.RemoveFromClassList("active");
        filterMiss?.RemoveFromClassList("active");

        switch (currentFilter)
        {
            case "all":
                filterAll?.AddToClassList("active");
                break;
            case "intercepted":
                filterIntercepted?.AddToClassList("active");
                break;
            case "miss":
                filterMiss?.AddToClassList("active");
                break;
        }
    }

    void ScanReplays()
    {
        string path = ReplayMetadataParser.GetDefaultReplaysPath();
        allReplays = ReplayMetadataParser.ScanDirectory(path);
        ApplyFilters();
    }

    void ApplyFilters()
    {
        filteredReplays.Clear();

        foreach (var replay in allReplays)
        {
            // Apply outcome filter
            if (currentFilter == "intercepted" && !replay.IsIntercepted) continue;
            if (currentFilter == "miss" && replay.IsIntercepted) continue;

            // Apply search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string query = searchQuery.ToLower();
                bool matchesFilename = replay.fileName.ToLower().Contains(query);
                bool matchesEpisodeId = replay.episodeId.ToLower().Contains(query);

                if (!matchesFilename && !matchesEpisodeId)
                {
                    continue;
                }
            }

            filteredReplays.Add(replay);
        }

        if (replayCount != null)
        {
            replayCount.text = $"{filteredReplays.Count} replay{(filteredReplays.Count != 1 ? "s" : "")}";
        }

        RebuildCards();
    }

    void RebuildCards()
    {
        if (cardContainer == null) return;

        cardContainer.Clear();
        selectedCard = null;

        // Check what's currently playing
        string currentPath = replayDirector?.CurrentEpisodePath ?? "";

        foreach (var replay in filteredReplays)
        {
            var card = CreateCard(replay);

            // Check if this is the currently playing replay
            bool isNowPlaying = !string.IsNullOrEmpty(currentPath) &&
                               replay.filePath.Replace("\\", "/").EndsWith(currentPath.Replace("\\", "/"));

            if (isNowPlaying)
            {
                card.AddToClassList("now-playing");
            }

            cardContainer.Add(card);
        }

        // Show empty state if no replays
        if (filteredReplays.Count == 0)
        {
            var emptyState = new VisualElement();
            emptyState.AddToClassList("empty-state");

            var emptyText = new Label("No replays found");
            emptyText.AddToClassList("empty-state-text");
            emptyState.Add(emptyText);

            cardContainer.Add(emptyState);
        }
    }

    VisualElement CreateCard(ReplayMetadata replay)
    {
        var card = new VisualElement();
        card.AddToClassList("replay-card");

        // Header row
        var header = new VisualElement();
        header.AddToClassList("card-header");

        // Title container (filename + episode ID)
        var titleContainer = new VisualElement();
        titleContainer.AddToClassList("card-title-container");

        var filename = new Label(replay.fileName);
        filename.AddToClassList("card-filename");

        var episodeId = new Label(replay.episodeId.ToUpper());
        episodeId.AddToClassList("card-episode-id");

        var subtitle = new Label(replay.DurationFormatted);
        subtitle.AddToClassList("card-subtitle");

        titleContainer.Add(filename);
        titleContainer.Add(episodeId);
        titleContainer.Add(subtitle);

        // Outcome badge
        var badge = new Label(replay.OutcomeDisplay);
        badge.AddToClassList("outcome-badge");
        badge.AddToClassList(replay.IsIntercepted ? "intercepted" : "miss");

        header.Add(titleContainer);
        header.Add(badge);
        card.Add(header);

        // Footer row (radar indicator + now playing)
        var footer = new VisualElement();
        footer.AddToClassList("card-footer");

        if (replay.hasRadarData)
        {
            var radarLabel = new Label("RADAR DATA");
            radarLabel.AddToClassList("radar-indicator");
            radarLabel.AddToClassList("available");
            footer.Add(radarLabel);
        }
        else
        {
            var noRadarLabel = new Label("NO RADAR");
            noRadarLabel.AddToClassList("radar-indicator");
            footer.Add(noRadarLabel);
        }

        // Check if now playing
        string currentPath = replayDirector?.CurrentEpisodePath ?? "";
        bool isNowPlaying = !string.IsNullOrEmpty(currentPath) &&
                           replay.filePath.Replace("\\", "/").EndsWith(currentPath.Replace("\\", "/"));

        if (isNowPlaying)
        {
            var nowPlayingBadge = new Label("NOW PLAYING");
            nowPlayingBadge.AddToClassList("now-playing-badge");
            footer.Add(nowPlayingBadge);
        }

        card.Add(footer);

        // Click handler
        card.RegisterCallback<ClickEvent>(evt => SelectReplay(replay, card));

        return card;
    }

    void SelectReplay(ReplayMetadata replay, VisualElement card)
    {
        // Clear previous selection
        if (selectedCard != null)
        {
            selectedCard.RemoveFromClassList("selected");
        }

        // Set new selection
        card.AddToClassList("selected");
        selectedCard = card;
        selectedReplay = replay;

        // Update footer
        if (selectedInfo != null)
        {
            selectedInfo.text = $"Selected: {replay.fileName}";
        }

        if (loadButton != null)
        {
            loadButton.SetEnabled(true);
        }
    }

    void LoadSelectedReplay()
    {
        if (selectedReplay == null)
        {
            Debug.LogWarning("[ReplayMenuController] No replay selected");
            return;
        }

        if (replayDirector == null)
        {
            Debug.LogError("[ReplayMenuController] No ReplayDirector reference!");
            return;
        }

        Debug.Log($"[ReplayMenuController] Loading replay: {selectedReplay.fileName}");

        // Convert to relative path for ReplayDirector
        string relativePath = $"Replays/{selectedReplay.fileName}";
        replayDirector.LoadReplayAt(relativePath);

        HideMenu();
    }

    void OnReplayChanged(string newPath)
    {
        // Refresh cards to update "Now Playing" indicator
        RebuildCards();
    }

    public void ToggleMenu()
    {
        if (isVisible)
            HideMenu();
        else
            ShowMenu();
    }

    public void ShowMenu()
    {
        if (overlay == null) return;

        overlay.style.display = DisplayStyle.Flex;
        isVisible = true;

        // Show cursor and unlock it for menu interaction
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        // Refresh replays list
        ScanReplays();

        // Reset selection
        selectedReplay = null;
        selectedCard = null;
        if (loadButton != null) loadButton.SetEnabled(false);
        if (selectedInfo != null) selectedInfo.text = "Press TAB to close";

        Debug.Log("[ReplayMenuController] Menu opened");
    }

    public void HideMenu()
    {
        if (overlay == null) return;

        overlay.style.display = DisplayStyle.None;
        isVisible = false;

        // Hide cursor and lock it for game interaction
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;

        Debug.Log("[ReplayMenuController] Menu closed");
    }

    /// <summary>
    /// Check if the menu is currently visible.
    /// Used by other systems to block input when menu is open.
    /// </summary>
    public bool IsVisible => isVisible;

    void OnDestroy()
    {
        if (replayDirector != null)
        {
            replayDirector.OnReplayChanged -= OnReplayChanged;
        }
    }
}
