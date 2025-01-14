﻿using System.Collections;
using UnityEngine;
using Utility = UnityEngine.Networking.Utility;
using Mirror;

public class WeaponNetworkActions : ManagedNetworkBehaviour
{
	private readonly float speed = 7f;
	private bool allowAttack = true;

	/// <summary>
	/// Is attack currently allowed due to cooldown?
	/// </summary>
	public bool AllowAttack => allowAttack;

	float fistDamage = 5;

	//muzzle flash
	private bool isFlashing;

	private bool isForLerpBack;
	private Vector3 lerpFrom;
	public bool lerping { get; private set; } //needs to be read by Camera2DFollow

	private float lerpProgress;

	//Lerp parameters
	private Sprite lerpSprite;

	private Vector3 lerpTo;
	private PlayerMove playerMove;
	private PlayerScript playerScript;
	private GameObject spritesObj;
	public ItemTrait KnifeTrait;

	private void Start()
	{
		spritesObj = transform.Find("Sprites").gameObject;
		playerMove = GetComponent<PlayerMove>();
		playerScript = GetComponent<PlayerScript>();
		lerpSprite = null;
	}

	[Command]
	public void CmdLoadMagazine(GameObject gunObject, GameObject magazine, NamedSlot hand)
	{
		if (!Validations.CanInteract(playerScript, NetworkSide.Server)) return;

		Gun gun = gunObject.GetComponent<Gun>();
		uint networkID = magazine.GetComponent<NetworkIdentity>().netId;
		gun.ServerHandleReloadRequest(networkID);
	}

	[Command]
	public void CmdUnloadWeapon(GameObject gunObject)
	{
		if (!Validations.CanInteract(playerScript, NetworkSide.Server)) return;

		Gun gun = gunObject.GetComponent<Gun>();

		var cnt = gun.CurrentMagazine?.GetComponent<CustomNetTransform>();
		if(cnt != null)
		{
			cnt.InertiaDrop(transform.position, playerScript.PlayerSync.SpeedServer, playerScript.PlayerSync.ServerState.Impulse);
		} else {
			Logger.Log("Magazine not found for unload weapon", Category.Firearms);
		}

		gun.ServerHandleUnloadRequest();
	}

	/// <summary>
	/// Requests a melee attack to be performed using the object in the player's active hand. Will be validated and performed if valid. Also handles punching
	/// if weapon is null.
	/// </summary>
	/// <param name="victim"></param>
	/// <param name="weapon">null for unarmed attack / punch</param>
	/// <param name="attackDirection">vector pointing from attacker to the target</param>
	/// <param name="damageZone">damage zone if attacking mob, otherwise use None</param>
	/// <param name="layerType">layer being attacked if attacking tilemap, otherwise use None</param>
	[Command]
	public void CmdRequestMeleeAttack(GameObject victim, Vector2 attackDirection,
		BodyPartType damageZone, LayerType layerType)
	{
		var weapon = playerScript.playerNetworkActions.GetActiveHandItem();

		var tiles = victim.GetComponent<InteractableTiles>();
		if (tiles)
		{
			//validate based on position of target vector
			if (!Validations.CanApply(playerScript, victim, NetworkSide.Server, targetVector: attackDirection)) return;
		}
		else
		{
			//validate based on position of target object
			if (!Validations.CanApply(playerScript, victim, NetworkSide.Server)) return;
		}

		if (!playerMove.allowInput ||
		    playerScript.IsGhost ||
		    !victim ||
		    !playerScript.playerHealth.serverPlayerConscious
		)
		{
			return;
		}

		if (!allowAttack)
		{
			return;
		}

		var isWeapon = weapon != null;
		ItemAttributesV2 weaponAttr = isWeapon ? weapon.GetComponent<ItemAttributesV2>() : null;
		var damage = isWeapon ? weaponAttr.ServerHitDamage : fistDamage;
		var damageType = isWeapon ? weaponAttr.ServerDamageType : DamageType.Brute;
		var attackSoundName = isWeapon ? weaponAttr.ServerHitSound : "Punch#";
		LayerTile attackedTile = null;

		bool didHit = false;


		// If Tilemap LayerType is not None then it is a tilemap being attacked
		if (layerType != LayerType.None)
		{
			var tileChangeManager = victim.GetComponent<TileChangeManager>();
			if (tileChangeManager == null) return; //Make sure its on a matrix that is destructable

			//Tilemap stuff:
			var tileMapDamage = victim.GetComponentInChildren<MetaTileMap>().Layers[layerType].gameObject
				.GetComponent<TilemapDamage>();
			if (tileMapDamage != null)
			{
				var worldPos = (Vector2) transform.position + attackDirection;
				attackedTile = tileChangeManager.InteractableTiles.LayerTileAt(worldPos);
				tileMapDamage.DoMeleeDamage(worldPos,
					gameObject, (int) damage);
				didHit = true;

			}
		}
		else
		{
			//a regular object being attacked

			//butchering
			//TODO: Move butchering logic to IF2, it should be a progress action done on corpses (make a Corpse component probably)
			LivingHealthBehaviour victimHealth = victim.GetComponent<LivingHealthBehaviour>();
			if (victimHealth != null && victimHealth.IsDead && isWeapon && weaponAttr.HasTrait(KnifeTrait))
			{
				if (victim.GetComponent<SimpleAnimal>())
				{
					SimpleAnimal attackTarget = victim.GetComponent<SimpleAnimal>();
					RpcMeleeAttackLerp(attackDirection, weapon);
					playerMove.allowInput = false;
					attackTarget.Harvest();
					SoundManager.PlayNetworkedAtPos( "BladeSlice", transform.position );
				}
				else
				{
					PlayerHealth attackTarget = victim.GetComponent<PlayerHealth>();
					RpcMeleeAttackLerp(attackDirection, weapon);
					playerMove.allowInput = false;
					attackTarget.Harvest();
					SoundManager.PlayNetworkedAtPos( "BladeSlice", transform.position );
				}
			}

			var integrity = victim.GetComponent<Integrity>();
			if (integrity != null)
			{
				//damaging an object
				integrity.ApplyDamage((int)damage, AttackType.Melee, damageType);
				didHit = true;
			}
			else
			{
				//damaging a living thing
				var rng = new System.Random();
				// This is based off the alien/humanoid/attack_hand punch code of TGStation's codebase.
				// Punches have 90% chance to hit, otherwise it is a miss.
				if (isWeapon || 90 >= rng.Next(1, 100))
				{
					// The attack hit.
					victimHealth.ApplyDamageToBodypart(gameObject, (int) damage, AttackType.Melee, damageType, damageZone);
					didHit = true;
				}
				else
				{
					// The punch missed.
					string victimName = victim.Player()?.Name;
					SoundManager.PlayNetworkedAtPos("PunchMiss", transform.position);
					Chat.AddCombatMsgToChat(gameObject, $"You attempted to punch {victimName} but missed!",
						$"{gameObject.Player()?.Name} has attempted to punch {victimName}!");
				}
			}
		}

		//common logic to do if we hit something
		if (didHit)
		{
			SoundManager.PlayNetworkedAtPos(attackSoundName, transform.position);
			if (damage > 0)
			{
				Chat.AddAttackMsgToChat(gameObject, victim, damageZone, weapon, attackedTile: attackedTile);
			}
			if (victim != gameObject)
			{
				RpcMeleeAttackLerp(attackDirection, weapon);
				playerMove.allowInput = false;
			}
		}

		//no matter what, start a cooldown for attacking again so they can't spam attack requests
		StartCoroutine(AttackCoolDown());
	}

	private IEnumerator AttackCoolDown(float seconds = 0.5f)
	{
		allowAttack = false;
		yield return WaitFor.Seconds(seconds);
		allowAttack = true;
	}

	[ClientRpc]
	public void RpcMeleeAttackLerp(Vector2 stabDir, GameObject weapon)
	{
		if (lerping)
		{
			return;
		}

		if (weapon && lerpSprite == null)
		{
			SpriteRenderer spriteRenderer = weapon.GetComponentInChildren<SpriteRenderer>();
			lerpSprite = spriteRenderer.sprite;
		}

		if (lerpSprite != null)
		{
			playerScript.hitIcon.ShowHitIcon(stabDir, lerpSprite);
		}

		Vector3 lerpFromWorld = spritesObj.transform.position;
		Vector3 lerpToWorld = lerpFromWorld + (Vector3)(stabDir * 0.5f);
		Vector3 lerpFromLocal = spritesObj.transform.parent.InverseTransformPoint(lerpFromWorld);
		Vector3 lerpToLocal = spritesObj.transform.parent.InverseTransformPoint(lerpToWorld);
		Vector3 localStabDir = lerpToLocal - lerpFromLocal;

		lerpFrom = lerpFromLocal;
		lerpTo = lerpToLocal;
		lerpProgress = 0f;
		isForLerpBack = true;
		lerping = true;
	}

	[Command]
	private void CmdRequestInputActivation()
	{
		if (playerScript.playerHealth.serverPlayerConscious)
		{
			playerMove.allowInput = true;
		}
		else
		{
			playerMove.allowInput = false;
		}
	}

	//Server lerps
	public override void UpdateMe()
	{
		if (lerping)
		{
			lerpProgress += Time.deltaTime;
			spritesObj.transform.localPosition = Vector3.Lerp(lerpFrom, lerpTo, lerpProgress * speed);
			if (spritesObj.transform.localPosition == lerpTo || lerpProgress > 2f)
			{
				if (!isForLerpBack)
				{
					ResetLerp();
					spritesObj.transform.localPosition = Vector3.zero;
					if (PlayerManager.LocalPlayer)
					{
						if (PlayerManager.LocalPlayer == gameObject)
						{
							CmdRequestInputActivation(); //Ask server if you can move again after melee attack
						}
					}
				}
				else
				{
					//To lerp back from knife attack
					ResetLerp();
					lerpTo = lerpFrom;
					lerpFrom = spritesObj.transform.localPosition;
					lerping = true;
				}
			}
		}
	}

	private void ResetLerp()
	{
		lerpProgress = 0f;
		lerping = false;
		isForLerpBack = false;
		lerpSprite = null;
	}
}
