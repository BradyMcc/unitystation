using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Antagonists
{
	/// <summary>
	/// An objective to steal items from the station
	/// </summary>
	[CreateAssetMenu(menuName="ScriptableObjects/Objectives/Steal")]
	public class Steal : Objective
	{
		/// <summary>
		/// The pool of possible items to steal
		/// </summary>
		[SerializeField]
		private ItemDictionary ItemPool;

		/// <summary>
		/// The item to steal
		/// </summary>
		private string ItemName;

		/// <summary>
		/// The number of items needed to complete the objective
		/// </summary>
		private int Amount;

		/// <summary>
		/// Make sure there's at least one item which hasn't been targeted
		/// </summary>
		public override bool IsPossible(PlayerScript candidate)
		{
			// Get all items from the item pool which haven't been targeted already
			int itemCount = ItemPool.Where( itemDict =>
				!AntagManager.Instance.TargetedItems.Contains(itemDict.Key)).Count();
			return (itemCount > 0);
		}

		/// <summary>
		/// Choose a target item from the item pool (no duplicates)
		/// </summary>
		protected override void Setup()
		{
			// Get all items from the item pool which haven't been targeted already
			var possibleItems = ItemPool.Where( itemDict =>
				!AntagManager.Instance.TargetedItems.Contains(itemDict.Key)).ToList();

			if (possibleItems.Count == 0)
			{
				Logger.LogWarning("Unable to find any suitable items to steal! Giving free objective", Category.Antags);
				description = "Free objective";
				Complete = true;
				return;
			}

			// Pick a random item and add it to the targeted list
			var itemEntry = possibleItems.PickRandom();
			ItemName = itemEntry.Key.Item().ItemName;
			Amount = itemEntry.Value;
			AntagManager.Instance.TargetedItems.Add(itemEntry.Key);
			// TODO randomise amount based on range/weightings?
			description = $"Steal {Amount} {ItemName}";
		}

		protected override bool CheckCompletion()
		{
			int count = 0;
			foreach (var slot in Owner.body.ItemStorage.GetItemSlotTree())
			{
				// TODO find better way to determine item types (ScriptableObjects/item IDs could work but would need to refactor all items)
				if (slot.ItemObject != null && slot.ItemObject.GetComponent<ItemAttributesV2>()?.ItemName == ItemName)
				{
					count++;
				}
			}
			// Check if the count is higher than the specified amount
			return count >= Amount;
		}
	}
}