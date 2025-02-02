using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UI.UI_Bottom;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
	private static UIManager uiManager;
	public GUI_VariableViewer VariableViewer;
	public BookshelfViewer BookshelfViewer;
	public ControlAction actionControl;
	[FormerlySerializedAs("dragAndDrop")] public UIDragAndDrop uiDragAndDrop;
	public ControlDisplays displayControl;
	public ControlClothing controlClothing;
	public DisplayManager displayManager;
	public Hands hands;
	public ControlIntent intentControl;
	public PlayerHealthUI playerHealthUI;
	public PlayerListUI playerListUIControl;
	public AlertUI alertUI;
	public Text toolTip;
	public Text pingDisplay;
	public ControlWalkRun walkRunControl;
	public UI_StorageHandler storageHandler;
	public BuildMenu buildMenu;
	public ZoneSelector zoneSelector;
	public bool ttsToggle;
	public static GamePad GamePad => Instance.gamePad;
	public GamePad gamePad;
	[HideInInspector]
	//map from progress bar id to actual progress bar component.
	private Dictionary<int, ProgressBar> progressBars = new Dictionary<int, ProgressBar>();

	///Global flag for focused input field. Movement keystrokes are ignored if true.
	/// <see cref="InputFieldFocus"/> handles this flag automatically
	public static bool IsInputFocus
	{
		get
		{
			return Instance && Instance.isInputFocus;
		}
		set
		{
			if (!Instance)
			{
				return;
			}
			Instance.isInputFocus = value;
		}
	}
	public bool isInputFocus;

	///Global flag for when we are using the mouse to do something that shouldn't cause interaction with the game.
	public static bool IsMouseInteractionDisabled
	{
		get
		{
			return Instance && Instance.isMouseInteractionDisabled;
		}
		set
		{
			if (!Instance)
			{
				return;
			}
			Instance.isMouseInteractionDisabled = value;
		}
	}

	private bool isMouseInteractionDisabled;

	public static UIManager Instance
	{
		get
		{
			if (!uiManager)
			{
				uiManager = FindObjectOfType<UIManager>();
			}

			return uiManager;
		}
	}

#if UNITY_ANDROID || UNITY_IOS //|| UNITY_EDITOR
	public static bool UseGamePad = true;
#else
	public static bool UseGamePad = false;
#endif

	//		public static ControlChat Chat => Instance.chatControl; //Use ChatRelay.Instance.AddToChatLog instead!
	public static PlayerHealthUI PlayerHealthUI => Instance.playerHealthUI;
	public static AlertUI AlertUI => Instance.alertUI;

	public static Hands Hands => Instance.hands;

	public static ControlIntent Intent => Instance.intentControl;

	public static ControlAction Action => Instance.actionControl;

	public static UIDragAndDrop UiDragAndDrop => Instance.uiDragAndDrop;

	public static ControlWalkRun WalkRun => Instance.walkRunControl;

	public static ControlDisplays Display => Instance.displayControl;
	public static ControlClothing ControlClothing => Instance.controlClothing;

	public static PlayerListUI PlayerListUI => Instance.playerListUIControl;

	public static DisplayManager DisplayManager => Instance.displayManager;
	public static UI_StorageHandler StorageHandler => Instance.storageHandler;
	public static BuildMenu BuildMenu => Instance.buildMenu;
	public static ZoneSelector ZoneSelector => Instance.zoneSelector;

	public static string SetToolTip
	{
		set { Instance.toolTip.text = value; }
	}

	public static string SetPingDisplay
	{
		set { Instance.pingDisplay.text = value; }
	}

	/// <summary>
	///     Current Intent status
	/// </summary>
	public static Intent CurrentIntent
	{
		get => currentIntent;
		set
		{
			currentIntent = value;
			//update the intent of the player so it can be synced
			if (PlayerManager.LocalPlayerScript != null)
			{
				PlayerManager.LocalPlayerScript.playerMove.IsHelpIntent = value == global::Intent.Help;
			}
		}
	}

	private static Intent currentIntent;

	/// <summary>
	///     What is DamageZoneSeclector currently set at
	/// </summary>
	public static BodyPartType DamageZone { get; set; }

	/// <summary>
	///     Is throw selected?
	/// </summary>
	public static bool IsThrow { get; set; }

	/// <summary>
	///     Is Oxygen On?
	/// </summary>
	public static bool IsOxygen { get; set; }

	private void Start()
	{
		Logger.Log("Touchscreen support = " + CommonInput.IsTouchscreen, Category.UI);

		if (!PlayerPrefs.HasKey(PlayerPrefKeys.TTSToggleKey))
		{
			PlayerPrefs.SetInt(PlayerPrefKeys.TTSToggleKey, 0);
			ttsToggle = false;
			PlayerPrefs.Save();
		}
		else
		{
			ttsToggle = PlayerPrefs.GetInt(PlayerPrefKeys.TTSToggleKey) == 1;
		}
	}

	public static void ToggleTTS(bool activeState)
	{
		Instance.ttsToggle = activeState;
		PlayerPrefs.SetInt(PlayerPrefKeys.TTSToggleKey, activeState ? 1 : 0);
		PlayerPrefs.Save();
	}

	public static void ResetAllUI()
	{
		UI_ItemSlot[] slots = Instance.GetComponentsInChildren<UI_ItemSlot>(true);
		foreach (UI_ItemSlot slot in slots)
		{
			slot.Reset();
		}
		foreach (DamageMonitorListener listener in Instance.GetComponentsInChildren<DamageMonitorListener>())
		{
			listener.Reset();
		}
		StorageHandler.CloseStorageUI();
		Camera2DFollow.followControl.ZeroStars();
		IsOxygen = false;
		GamePad.gameObject.SetActive(UseGamePad);
	}

	/// <summary>
	/// Gets the progress bar with the specified unique id. Creates it at the specified position if it doesn't
	/// exist yet
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public static ProgressBar GetProgressBar(int id)
	{
		if (Instance.progressBars.ContainsKey(id))
		{
			return Instance.progressBars[id];
		}

		return null;
	}

	/// <summary>
	/// Creates a new progress bar for local player with the specified id and specified offset from player
	/// (parented to the matrix at the position being targeted so it moves with it)
	/// </summary>
	/// <param name="offsetFromPlayer">offset position from local player</param>
	/// <param name="progressBarId">id to assign to the new progress bar</param>
	/// <returns> the new bar</returns>
	public static ProgressBar CreateProgressBar(Vector2Int offsetFromPlayer, int progressBarId)
	{
		//convert to local position so it appears correct on moving matrix
		//do not use tileworldposition for actual spawn position - bar will appear shifted on moving matrix
		var targetWorldPosition = PlayerManager.LocalPlayer.transform.position + offsetFromPlayer.To3Int();
		var targetTilePosition = PlayerManager.LocalPlayer.TileWorldPosition() + offsetFromPlayer;
		var targetMatrixInfo = MatrixManager.AtPoint(targetTilePosition.To3Int(), true);
		var targetParent = targetMatrixInfo.Objects;
		//snap to local position
		var targetLocalPosition = targetParent.transform.InverseTransformPoint(targetWorldPosition).RoundToInt();
		//back to world so we can call PoolClientInstantiate
		targetWorldPosition = targetParent.transform.TransformPoint(targetLocalPosition);
		var barObject = Spawn.ClientPrefab("ProgressBar", targetWorldPosition, targetParent).GameObject;
		var progressBar = barObject.GetComponent<ProgressBar>();

		progressBar.ClientStartProgress(progressBarId);

		Instance.progressBars.Add(progressBarId, progressBar);

		return progressBar;
	}

	/// <summary>
	/// Destroys the progress bar.
	/// </summary>
	/// <param name="progressBarId"></param>
	public static void DestroyProgressBar(int progressBarId)
	{
		var bar = GetProgressBar(progressBarId);
		if (bar == null)
		{
			Logger.LogWarningFormat("Tried to destroy progress bar with unrecognized id {0}, nothing will be done.", Category.UI, progressBarId);
		}
		else
		{
			Instance.progressBars.Remove(progressBarId);
			//note: not using poolmanager since this has no object behavior
			Despawn.ClientSingle(bar.gameObject);
		}
	}

	/// <summary>
	/// Tries to create and begin animating a progress bar for a specific player. Returns null
	/// if progress did not begin for some reason.
	/// </summary>
	/// <param name="progressAction">progress action being performed</param>
	/// <param name="worldTilePos">tile position the action is being performed on</param>
	/// <param name="timeForCompletion">how long in seconds the action should take</param>
	/// <param name="progressEndAction">callback for when action completes or is interrupted</param>
	/// <param name="player">player performing the action</param>
	/// <returns>progress bar associated with this action (can use this to interrupt progress). Null if
	/// progress was not started for some reason (such as already in progress for this action on the specified tile).</returns>
	public static ProgressBar ServerStartProgress(ProgressAction progressAction, Vector3 worldTilePos, float timeForCompletion,
		IProgressEndAction progressEndAction, GameObject player)
	{
		if (!progressAction.AllowMultiple)
		{
			//check if we are already doing this action anywhere else
			var existingAction = Instance.progressBars.Values
				.Where(pb => pb.RegisterPlayer.gameObject == player)
				.FirstOrDefault(pb => pb.ProgressAction == progressAction);
			if (existingAction != null)
			{
				//already doing this action, and multiple is not allowed
				return null;
			}
		}
		//convert to an offset so local position ends up being correct even on moving matrix
		var offsetFromPlayer = worldTilePos.To2Int() - player.TileWorldPosition();
		//convert to local position so it appears correct on moving matrix
		//do not use tileworldposition for actual spawn position - bar will appear shifted on moving matrix
		var targetWorldPosition = player.transform.position + offsetFromPlayer.To3Int();
		var targetTilePosition = player.TileWorldPosition() + offsetFromPlayer;
		var targetMatrixInfo = MatrixManager.AtPoint(targetTilePosition.To3Int(), true);
		var targetParent = targetMatrixInfo.Objects;
		//snap to local position
		var targetLocalPosition = targetParent.transform.InverseTransformPoint(targetWorldPosition).RoundToInt();

		//check if there is already progress at this location by this player
		var existingBar = Instance.progressBars.Values
			.Where(pb => pb.RegisterPlayer.gameObject == player)
			.Where(pb => pb.transform.parent == targetParent)
			.FirstOrDefault(pb => Vector3.Distance(pb.transform.localPosition, targetLocalPosition) < 0.1);

		if (existingBar != null)
		{
			return null;
		}

		//back to world so we can call PoolClientInstantiate
		targetWorldPosition = targetParent.transform.TransformPoint(targetLocalPosition);
		var barObject = Spawn.ClientPrefab("ProgressBar", targetWorldPosition, targetParent).GameObject;
		var progressBar = barObject.GetComponent<ProgressBar>();
		progressBar.ServerStartProgress(progressAction, timeForCompletion, progressEndAction, player);
		Instance.progressBars.Add(progressBar.ID, progressBar);

		return progressBar;
	}

	/// <summary>
	/// Interrupts all other actions of the given progressaction at the specified local position in the specified parent.
	/// </summary>
	/// <param name="progressBar">progressbar which is causing the interruption</param>
	/// <param name="progressAction">action type to interrupt</param>
	/// <param name="transformLocalPosition">local position to interrupt at</param>
	/// <param name="transformParent">parent to check children of for progress actions</param>
	public static void ServerInterruptProgress(ProgressBar cause, ProgressAction progressAction, Vector3 transformLocalPosition, Transform transformParent)
	{
		var existingBars = Instance.progressBars.Values
			.Where(pb => pb != cause)
			.Where(pb => pb.ProgressAction == progressAction)
			.Where(pb => pb.transform.parent == transformParent)
			.Where(pb => Vector3.Distance(pb.transform.localPosition, transformLocalPosition) < 0.1)
			.ToList();

		foreach (var existingBar in existingBars)
		{
			existingBar.ServerInterruptProgress();
		}
	}

	/// <summary>
	/// Links the UI slots to the spawned local player. Should only be called after local player has been spawned / set
	/// </summary>
	public static void LinkUISlots()
	{
		//link the UI slots to this player
		foreach (var uiSlot in Instance.GetComponentsInChildren<UI_ItemSlot>(true))
		{
			uiSlot.LinkToLocalPlayer();
		}
	}
}