﻿using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hearthstone_Deck_Tracker
{
	public static class OpponentDeadForTracker
	{
		private static List<int> _uniqueDeadPlayers = new List<int>();
		private static List<int> _deadTracker = new List<int>();
		const int NextOpponentCheckDelay = 500;

		public static void ResetOpponentDeadForTracker()
		{
			_uniqueDeadPlayers.Clear();
			_deadTracker.Clear();
			Core.Overlay.ResetNextOpponentLeaderboardPosition();
			Core.Overlay.UpdateOpponentDeadForTurns(_deadTracker); // hide
		}

		public static async void ShoppingStarted(GameV2 game)
		{
			if(game.GetTurnNumber() <= 1)
				ResetOpponentDeadForTracker();
			for(int i = 0; i < _deadTracker.Count; i++)
				_deadTracker[i]++;
			var deadHeroes = game.Entities.Values.Where(x => x.IsHero && x.Health <= 0);
			foreach(var hero in deadHeroes)
			{
				var playerId = hero.GetTag(GameTag.PLAYER_ID);
				if(playerId > 0 && !_uniqueDeadPlayers.Contains(playerId))
				{
					_deadTracker.Add(0);
					_uniqueDeadPlayers.Add(playerId);
				}
			}
			_deadTracker.Sort((x, y) => y.CompareTo(x));
			Core.Overlay.UpdateOpponentDeadForTurns(_deadTracker);
			var gameEntites = game.Entities.Values;
			var currentPlayer = gameEntites.FirstOrDefault(x => x.IsCurrentPlayer);
			//We loop because the next opponent tag is set slightly after the start of shopping (when this function is called).
			for(int i = 0; i < 5; i++)
			{
				if(currentPlayer != null && currentPlayer.HasTag(GameTag.NEXT_OPPONENT_PLAYER_ID))
				{
					var nextOpponent = gameEntites.FirstOrDefault(x => x.GetTag(GameTag.PLAYER_ID) == currentPlayer.GetTag(GameTag.NEXT_OPPONENT_PLAYER_ID));
					if(nextOpponent != null)
					{
						var leaderboardPlace = nextOpponent.GetTag(GameTag.PLAYER_LEADERBOARD_PLACE);
						if(leaderboardPlace >= 0 && leaderboardPlace < 8)
						{
							Core.Overlay.PositionDeadForText(leaderboardPlace);
						}
					}
				}
				await Task.Delay(NextOpponentCheckDelay);
			}
		}
	}
}
