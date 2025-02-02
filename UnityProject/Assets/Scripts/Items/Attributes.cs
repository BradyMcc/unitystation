﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IngameDebugConsole;
using UnityEngine;
using Mirror;
using UnityEngine.Serialization;
using Random = System.Random;

[RequireComponent(typeof(Integrity))]
[RequireComponent(typeof(CustomNetTransform))]
public class Attributes : NetworkBehaviour, IRightClickable, IServerSpawn
{
	
	[Tooltip("Display name of this item when spawned.")]
	[SerializeField]
	public string initialName;
	/// <summary>
	/// Current name
	/// </summary>
	[HideInInspector]
	[SyncVar(hook = nameof(SyncArticleName))]
	public string ArticleName;

	[Tooltip("Description of this item when spawned.")]
	[SerializeField]
	public string initialDescription;
	/// <summary>
	/// Current description
	/// </summary>

	[Tooltip("How much does one of these sell for when shipped on the cargo shuttle?")]
	[SerializeField]
	private int exportCost;
	public int ExportCost
	{
		get
		{
			var stackable = GetComponent<Stackable>();

			if (stackable != null)
			{
				return exportCost * stackable.Amount;
			}

			return exportCost;
		}

	}

	[Tooltip("Should an alternate name be used when displaying this in the cargo console report?")]
	[SerializeField]
	private string exportName;
	public string ExportName => exportName;

	[Tooltip("Additional message to display in the cargo console report.")]
	[SerializeField]
	private string exportMessage;
	public string ExportMessage => exportMessage;

	[HideInInspector]
	[SyncVar(hook = nameof(SyncArticleDescription))]
	public string ArticleDescription;

    public override void OnStartClient()
	{
		SyncArticleName(this.name);
		SyncArticleDescription(this.ArticleDescription);
		base.OnStartClient();
	}


	public virtual void OnSpawnServer(SpawnInfo info)
	{
		SyncArticleName(initialName);
		SyncArticleDescription(initialDescription);
	}

	private void SyncArticleName(string newName)
	{
		ArticleName = newName;
	}

	private void SyncArticleDescription(string newDescription)
	{
		ArticleDescription = newDescription;
	}

	public void OnHoverStart()
	{
		// Show the parenthesis for an item's description only if the item has a description
		UIManager.SetToolTip =
			initialName +
			(String.IsNullOrEmpty(ArticleDescription) ? "" : $" ({ArticleDescription})");
	}

	public void OnHoverEnd()
	{
		UIManager.SetToolTip = String.Empty;
	}


	// Sends examine event to all monobehaviors on gameobject
	public void SendExamine()
	{
		SendMessage("OnExamine");
	}

	private void OnExamine()
	{
		if (!string.IsNullOrEmpty(initialDescription))
		{
			Chat.AddExamineMsgToClient(initialDescription);
		}
	}

	public RightClickableResult GenerateRightClickOptions()
	{
		return RightClickableResult.Create()
			.AddElement("Examine", OnExamine);
	}


	public void ServerSetArticleName(string newName)
	{
		SyncArticleName(newName);
	}

	[Server]
	public void ServerSetArticleDescription(string desc)
	{
		SyncArticleDescription(desc);
	}

}
