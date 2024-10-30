﻿using System.Linq;
using System.Collections.Generic;

namespace Hearthstone_Deck_Tracker.Hearthstone.RelatedCardsSystem.Cards.Hunter;

public class Product9: ICardWithRelatedCards
{
	public string GetCardId() => HearthDb.CardIds.Collectible.Hunter.Product9;

	public bool ShouldShowForOpponent(Player opponent)
	{
		var card = Database.GetCardFromId(GetCardId());
		return CardUtils.MayCardBeRelevant(card, Core.Game.CurrentFormat, opponent.Class) && GetRelatedCards(opponent).Count > 0;
	}

	public List<Card?> GetRelatedCards(Player player) =>
		player.SecretsTriggeredCards
			.Select(entity => CardUtils.GetProcessedCardFromCardId(entity.CardId, player))
			.Distinct()
			.Where(card => card != null)
			.OrderByDescending(card => card!.Cost)
			.ToList();
}
