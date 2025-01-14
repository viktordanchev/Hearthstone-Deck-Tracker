#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.Themes;

#endregion

namespace Hearthstone_Deck_Tracker.Hearthstone
{
	public struct CardWinrates
	{
		public float MulliganWinrate;
		public float? BaseWinrate;
	}

	[Serializable]
	public class Card : ICloneable, INotifyPropertyChanged
	{
		[NonSerialized]
		private HearthDb.Card? _dbCard;

		private readonly Regex _overloadRegex = new Regex(@"Overload:.+?\((?<value>(\d+))\)");
		private int _count;
		private string? _englishText;
		private int _inHandCount;
		private bool _isCreated;
		private bool _loaded;
		private string? _localizedName;
		private int? _overload;
		private string? _text;
		private bool _wasDiscarded;
		private string? _id;
		private CardWinrates? _cardWinrates;
		private bool _isMulliganOption;

		[NonSerialized]
		private static readonly Dictionary<string, Dictionary<int, CardImageObject>> CardImageCache =
			new Dictionary<string, Dictionary<int, CardImageObject>>();

		[XmlIgnore]
		public List<string> AlternativeNames = new List<string>();

		[XmlIgnore]
		public List<string> AlternativeTexts = new List<string>();

		public string Id
		{
			get { return _id ?? Database.UnknownCardId; }
			set
			{
				_id = value;
				if(_dbCard == null)
					Load();
			}
		}

		// Typo - Do not remove this for Plugin combatibility
		[XmlIgnore, Obsolete("Use DbfId instead", true)]
		public int DbfIf => _dbCard?.DbfId ?? 0;

		[XmlIgnore]
		public int DbfId => _dbCard?.DbfId ?? 0;

		/// The mechanics attribute, such as windfury or taunt, comes from the cardDB json file
		[XmlIgnore]
		public string[]? Mechanics;

		[XmlIgnore]
		public string? PlayerClass;

		[XmlIgnore]
		public Rarity Rarity;

		[XmlIgnore]
		public bool BaconCard;

		[XmlIgnore]
		public bool BaconTriple;

		[XmlIgnore]
		public int DeckListIndex;

		[XmlIgnore]
		public SpellSchool SpellSchool => (SpellSchool?) _dbCard?.SpellSchool ?? SpellSchool.NONE;

		public Card()
		{
			Count = 1;
		}

		public Card(string id, string? playerClass, Rarity rarity, string? type, string? name, int cost, string? localizedName, int inHandCount,
		            int count, string? text, string? englishText, int attack, int health, string? race, string[]? mechanics, int? durability,
		            string? artist, string? set, bool baconCard, List<string>? alternativeNames = null, List<string>? alternativeTexts = null, HearthDb.Card? dbCard = null, CardWinrates? cardWinrates = null, bool isMulliganOption = false)
		{
			Id = id;
			PlayerClass = playerClass;
			Rarity = rarity;
			Type = type;
			Name = name;
			Cost = cost;
			LocalizedName = localizedName;
			InHandCount = inHandCount;
			Count = count;
			Text = text;
			EnglishText = englishText;
			Attack = attack;
			Health = health;
			Race = race;
			Durability = durability;
			Mechanics = mechanics;
			Artist = artist;
			Set = set;
			BaconCard = baconCard;
			if(alternativeNames != null)
				AlternativeNames = alternativeNames;
			if(alternativeTexts != null)
				AlternativeTexts = alternativeTexts;
			_dbCard = dbCard;
			CardWinrates = cardWinrates;
			IsMulliganOption = isMulliganOption;
		}

		private Locale? _selectedLanguage;

		private Locale SelectedLanguage
		{
			get
			{
				if(_selectedLanguage.HasValue)
					return _selectedLanguage.Value;
				if(!Enum.TryParse(Config.Instance.SelectedLanguage, out Locale lang))
					lang = Locale.enUS;
				_selectedLanguage = lang;
				return _selectedLanguage.Value;
			}
		}

		public Card(HearthDb.Card dbCard, bool baconCard = false)
		{
			_dbCard = dbCard;
			Id = dbCard.Id;
			Count = 1;
			PlayerClass = HearthDbConverter.ConvertClass(dbCard.Class);
			Rarity = dbCard.Rarity;
			Type = HearthDbConverter.CardTypeConverter(dbCard.Type);
			Name = dbCard.GetLocName(Locale.enUS);
			Cost = dbCard.Cost;
			LocalizedName = dbCard.GetLocName(SelectedLanguage);
			Text = dbCard.GetLocText(SelectedLanguage);
			EnglishText = dbCard.GetLocText(Locale.enUS);
			Attack = dbCard.Attack;
			Health = dbCard.Health;
			Race = HearthDbConverter.RaceConverter(dbCard.Race);
			Durability = dbCard.Durability > 0 ? (int?)dbCard.Durability : null;
			Mechanics = dbCard.Mechanics;
			Artist = dbCard.ArtistName;
			Set = HearthDbConverter.SetConverter(dbCard.Set);
			BaconCard = baconCard;
			BaconTriple = false;
			_loaded = true;
		}

		public int Count
		{
			get { return _count; }
			set
			{
				_count = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(Background));
			}
		}

		[XmlIgnore]
		public bool IsMulliganOption
		{
			get { return _isMulliganOption; }
			set
			{
				_isMulliganOption = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(Background));
			}
		}

		[XmlIgnore]
		public CardWinrates? CardWinrates
		{
			get { return _cardWinrates; }
			set
			{
				_cardWinrates = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(Background));
			}
		}

		[XmlIgnore]
		public bool Jousted { get; set; }

		[XmlIgnore]
		public int Attack { get; set; }

		[XmlIgnore]
		public int Health { get; set; }

		[XmlIgnore]
		public string? Text
		{
			get { return CleanUpText(_text); }
			set { _text = value; }
		}

		[XmlIgnore]
		public string FormattedText => CleanUpText(_text, false) ?? "";

		[XmlIgnore]
		public string? EnglishText
		{
			get { return CleanUpText(string.IsNullOrEmpty(_englishText) ? Text : _englishText); }
			set { _englishText =value; }
		}

		[XmlIgnore]
		public string AlternativeLanguageText => GetAlternativeText(false);

		[XmlIgnore]
		public string FormattedAlternativeLanguageText => GetAlternativeText(true);

		private string GetAlternativeText(bool formatted)
		{
			var result = "";
			for(var i = 0; i < AlternativeNames.Count; ++i)
			{
				if(i > 0)
					result += "-\n";
				result += "[" + AlternativeNames[i] + "]\n";
				if(AlternativeTexts[i] != null)
					result += CleanUpText(AlternativeTexts[i], !formatted) + "\n";
			}
			return result.TrimEnd(' ', '\n');
		}

		[XmlIgnore]
		public Visibility ShowAlternativeLanguageTextInTooltip => AlternativeNames.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

		[XmlIgnore]
		public bool HasVisibleStats => Type != "Spell" && Type != "Enchantment" && Type != "Hero Power" && !IsPlayableHeroCard;

		[XmlIgnore]
		public Visibility ShowIconsInTooltip => HasVisibleStats ? Visibility.Visible : Visibility.Hidden;

		[XmlIgnore]
		public Visibility ShowArmorIconInTooltip => IsPlayableHeroCard ? Visibility.Visible : Visibility.Hidden;

		[XmlIgnore]
		public Visibility ShowHealthValueInTooltip => HasVisibleStats || IsPlayableHeroCard ? Visibility.Visible : Visibility.Hidden;

		[XmlIgnore]
		public string? Set { get; set; }

		public CardSet? CardSet => _dbCard?.Set;

		public CardClass CardClass => _dbCard?.Class ?? CardClass.INVALID;

		public int TechLevel => _dbCard?.Entity.GetTag(GameTag.TECH_LEVEL) ?? 0;

		public int BattlegroundsSkinParentId => _dbCard?.Entity.GetTag(GameTag.BACON_SKIN_PARENT_ID) ?? 0;

		public int BattlegroundsArmorTier => _dbCard?.BattlegroundArmorTier ?? 0;

		public Race? RaceEnum => _dbCard?.Race;
		public Race? SecondaryRaceEnum => _dbCard?.SecondaryRace;

		public int LettuceCooldown => _dbCard?.Entity.GetTag(GameTag.LETTUCE_COOLDOWN_CONFIG) ?? 0;

		[XmlIgnore]
		public string? Race { get; set; }

		public bool HasRace(Race race)
		{
			if(race == RaceEnum || race == SecondaryRaceEnum)
				return true;
			if(race == HearthDb.Enums.Race.BLANK)
				return false;
			if(RaceEnum == HearthDb.Enums.Race.ALL || SecondaryRaceEnum == HearthDb.Enums.Race.ALL)
				return true;
			return false;
		}

		public bool IsMech() => HasRace(HearthDb.Enums.Race.MECHANICAL);

		public bool IsDemon() => HasRace(HearthDb.Enums.Race.DEMON);

		public bool IsBeast() => HasRace(HearthDb.Enums.Race.BEAST);

		public bool IsMurloc() => HasRace(HearthDb.Enums.Race.MURLOC);

		public bool IsDragon() => HasRace(HearthDb.Enums.Race.DRAGON);

		public bool IsPirate() => HasRace(HearthDb.Enums.Race.PIRATE);

		public bool IsElemental() => HasRace(HearthDb.Enums.Race.ELEMENTAL);

		public bool IsQuillboar() => HasRace(HearthDb.Enums.Race.QUILBOAR);

		public bool IsUndead() => HasRace(HearthDb.Enums.Race.UNDEAD);

		public bool IsNaga() => HasRace(HearthDb.Enums.Race.NAGA);

		public bool isDraenei() => HasRace(HearthDb.Enums.Race.DRAENEI);

		public bool IsEmptyRace() => RaceEnum == HearthDb.Enums.Race.INVALID && SecondaryRaceEnum == HearthDb.Enums.Race.INVALID;

		[XmlIgnore]
		public string? RaceOrType => Race ?? Type;

		[XmlIgnore]
		public int? Durability { get; set; }

		[XmlIgnore]
		public int ArmorDurabilityOrHealth => (IsPlayableHeroCard ? _dbCard?.Armor : Durability) ?? Health;

		[XmlIgnore]
		public string? Type { get; set; }

		public CardType? TypeEnum => _dbCard?.Type;

		[XmlIgnore]
		public string? Name { get; set; }

		[XmlIgnore]
		public int Cost { get; set; }

		public bool HideStats => _dbCard?.Entity.GetTag(GameTag.HIDE_STATS) == 1;

		[XmlIgnore]
		public bool IsPlayableHeroCard => Type == "Hero" && CardSet != HearthDb.Enums.CardSet.CORE && CardSet != HearthDb.Enums.CardSet.HERO_SKINS;

		[XmlIgnore]
		public int Overload
		{
			get
			{
				if(_overload.HasValue)
					return _overload.Value;
				var overload = -1;
				if(!string.IsNullOrEmpty(EnglishText))
				{
					var match = _overloadRegex.Match(EnglishText);
					if(match.Success)
						int.TryParse(match.Groups["value"].Value, out overload);
					_overload = overload;
				}
				return overload;
			}
		}

		public int DustCost
		{
			get
			{
				switch(Rarity)
				{
					case Rarity.COMMON:
						return 40;
					case Rarity.RARE:
						return 100;
					case Rarity.EPIC:
						return 400;
					case Rarity.LEGENDARY:
						return 1600;
				}
				return 0;
			}
		}

		[XmlIgnore]
		public string? Artist { get; set; }

		[XmlIgnore]
		public string? LocalizedName
		{
			get { return string.IsNullOrEmpty(_localizedName) ? Name : _localizedName; }
			set { _localizedName = value; }
		}

		[XmlIgnore]
		public int InHandCount
		{
			get { return _inHandCount; }
			set
			{
				_inHandCount = value;
				OnPropertyChanged();
			}
		}

		[XmlIgnore]
		public bool IsClassCard => GetPlayerClass != "Neutral";

		[XmlIgnore]
		public bool IsCreated
		{
			get { return _isCreated; }
			set
			{
				_isCreated = value;
				OnPropertyChanged();
			}
		}

		[XmlIgnore]
		public bool WasDiscarded
		{
			get { return _wasDiscarded; }
			set
			{
				_wasDiscarded = value;
				OnPropertyChanged();
			}
		}

		public string GetPlayerClass => PlayerClass ?? "Neutral";

		public bool IsClass(string? playerClass)
		{
			if(playerClass == null)
				return false;
			return GetClasses().Contains(playerClass);
		}

		public bool IsNeutral => GetPlayerClass == "Neutral" && (_dbCard?.Entity.GetTag(GameTag.MULTIPLE_CLASSES) ?? 0) == 0;

		public List<string> GetClasses()
		{
			List<string> classes = new List<string>();

			var multipleClasses = _dbCard?.Entity.GetTag(GameTag.MULTIPLE_CLASSES) ?? 0;
			if (multipleClasses == 0u)
			{
				classes.Add(GetPlayerClass);
				return classes;
			}

			int cardClass = 1;
			while (multipleClasses != 0u)
			{
				if (1u == (multipleClasses & 1u))
				{
					var className = HearthDbConverter.ConvertClass((CardClass)cardClass);
					classes.Add(className ?? "Neutral");
				}
				multipleClasses >>= 1;
				cardClass++;
			}
			return classes;
		}

		[XmlIgnore]
		private static readonly Dictionary<GameTag, CardClass> TOURIST_MAP = new ()
		{
			{ GameTag.MAGE_TOURIST, CardClass.MAGE },
			{ GameTag.PALADIN_TOURIST, CardClass.PALADIN },
			{ GameTag.WARRIOR_TOURIST, CardClass.WARRIOR },
			{ GameTag.HUNTER_TOURIST, CardClass.HUNTER },
			{ GameTag.ROGUE_TOURIST, CardClass.ROGUE },
			{ GameTag.DRUID_TOURIST, CardClass.DRUID },
			{ GameTag.SHAMAN_TOURIST, CardClass.SHAMAN },
			{ GameTag.WARLOCK_TOURIST, CardClass.WARLOCK },
			{ GameTag.PRIEST_TOURIST, CardClass.PRIEST },
			{ GameTag.DEMON_HUNTER_TOURIST, CardClass.DEMONHUNTER },
			{ GameTag.DEATH_KNIGHT_TOURIST, CardClass.DEATHKNIGHT }
		};

		[XmlIgnore]
		private static readonly Dictionary<CardClass, CardClass> TOURIST_VISIT_MAP = new ()
		{
			{ CardClass.ROGUE, CardClass.PALADIN },
			{ CardClass.WARLOCK, CardClass.ROGUE },
			{ CardClass.DEATHKNIGHT, CardClass.WARLOCK },
			{ CardClass.SHAMAN, CardClass.DEATHKNIGHT },
			{ CardClass.DEMONHUNTER, CardClass.SHAMAN },
			{ CardClass.PRIEST, CardClass.DEMONHUNTER },
			{ CardClass.HUNTER, CardClass.PRIEST },
			{ CardClass.WARRIOR, CardClass.HUNTER },
			{ CardClass.DRUID, CardClass.WARRIOR },
			{ CardClass.MAGE, CardClass.DRUID },
		};

		public bool IsTourist => _dbCard?.Entity.GetTag(GameTag.TOURIST) > 0;

		public bool CanBeVisitedByTourist => !IsTourist && CardSet == HearthDb.Enums.CardSet.ISLAND_VACATION;

		public string? GetTouristClass()
		{
			if(!IsTourist)
				return null;

			foreach (var tag in TOURIST_MAP)
			{
				if (_dbCard?.Entity.GetTag(tag.Key) > 0)
					return HearthDbConverter.ConvertClass(tag.Value);
			}

			return null;
		}

		public string? GetTouristVisitClass()
		{
			if(!CanBeVisitedByTourist)
				return null;

			var cardClass = _dbCard?.Class ?? CardClass.INVALID;


			if (TOURIST_VISIT_MAP.TryGetValue(cardClass, out var visitClass))
				return HearthDbConverter.ConvertClass(visitClass);

			return null;
		}

		public bool ZilliaxCustomizableFunctionalModule => _dbCard?.Entity.GetTag(GameTag.ZILLIAX_CUSTOMIZABLE_FUNCTIONALMODULE) > 0;

		public bool ZilliaxCustomizableCosmeticModule => _dbCard?.Entity.GetTag(GameTag.ZILLIAX_CUSTOMIZABLE_COSMETICMODULE) > 0;

		public Card DeckbuildingCard
		{
			get
			{
				if(ZilliaxCustomizableCosmeticModule)
				{
					return Database.GetCardFromId(HearthDb.CardIds.Collectible.Neutral.ZilliaxDeluxe3000) ?? this;
				}

				return this;
			}
		}

		public SolidColorBrush ColorPlayer
		{
			get
			{
				Color color;
				if(HighlightInHand && Config.Instance.HighlightCardsInHand)
					color = Colors.GreenYellow;
				else if(WasDiscarded && Config.Instance.HighlightDiscarded)
					color = Colors.IndianRed;
				else
					color = Colors.White;
				return new SolidColorBrush(color);
			}
		}

		public SolidColorBrush ColorOpponent => new SolidColorBrush(Colors.White);

		public string? CardFileName => Name?.ToLowerInvariant().Replace(' ', '-').Replace(":", "").Replace("'", "-").Replace(".", "").Replace("!", "").Replace(",", "");

		public static FontFamily DefaultFont => Helper.UseLatinFont() ? new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#Chunkfive") : new FontFamily();

		public static FontWeight DefaultFontWeight => Helper.UseLatinFont() ? FontWeights.Normal : FontWeights.Bold;

		private CardBarImageBuilder? GetImageBuilder()
		{
			if(BaconCard)
			{
				var theme = ThemeManager.FindTheme("dark");
				if(theme != null)
					return new DarkBarImageBuilder(this, theme.Directory);
			}
			return ThemeManager.GetBarImageBuilder(this);
		}

		private int _backgroundImageTries = 0;
		public DrawingBrush Background
		{
			get
			{
				if(Id == null || Name == null)
					return new DrawingBrush();
				var cardImageObj = new CardImageObject(this);
				if(CardImageCache.TryGetValue(Id, out var cache))
				{
					if(cache.TryGetValue(cardImageObj.GetHashCode(), out var cached))
						return cached.Image ?? new DrawingBrush();
				}
				try
				{
					var image = GetImageBuilder()?.Build(async (success) => {
						// This only gets invoked if the image was not available
						// in memory or on disk.
						if(_backgroundImageTries > 3)
							return;
						_backgroundImageTries++;

						// Ensure the Background_get call completed before
						// remove the entry from cache.
						await Task.Yield();
						CardImageCache.Remove(Id);

						if(success)
						{
							// Force background to update when card image becomes available.
							// If loading the art was not sucessful we don't
							// force an update, but just try again next time.
							Update();
						}
					}) ?? new DrawingBrush();

					if (image.CanFreeze)
						image.Freeze();
					cardImageObj = new CardImageObject(image, this);
					if(cache == null)
					{
						cache = new Dictionary<int, CardImageObject>();
						CardImageCache.Add(Id, cache);
					}
					cache.Add(cardImageObj.GetHashCode(), cardImageObj);
					return cardImageObj.Image ?? new DrawingBrush();
				}
				catch(Exception ex)
				{
					Log.Error($"Image builder failed: {ex.Message}");
					return new DrawingBrush();
				}
			}
		}

		internal void Update() => OnPropertyChanged(nameof(Background));
		internal void UpdateHighlight() => OnPropertyChanged(nameof(Highlight));

		public ImageBrush Highlight => ThemeManager.CurrentTheme?.HighlightImage ?? new ImageBrush();

		[XmlIgnore]
		public bool HighlightInHand { get; set; }

		[XmlIgnore]
		public string FlavorText => CleanUpText(_dbCard?.GetLocFlavorText(SelectedLanguage)) ?? "";

		[XmlIgnore]
		public string FormattedFlavorText => CleanUpText(_dbCard?.GetLocFlavorText(SelectedLanguage), false) ?? "";

		[XmlIgnore]
		public bool Collectible => _dbCard?.Collectible ?? false;

		public object Clone() => new Card(Id, PlayerClass, Rarity, Type, Name, Cost, LocalizedName, InHandCount, Count, _text, EnglishText, Attack,
										  Health, Race, Mechanics, Durability, Artist, Set, BaconCard, AlternativeNames, AlternativeTexts, _dbCard);

		public override string ToString() => Name + "(" + Count + ")";

		public override bool Equals(object card)
		{
			if(!(card is Card))
				return false;
			var c = (Card)card;
			return c.Id == Id;
		}

		public bool EqualsWithCount(Card card) => card.Id == Id && card.Count == Count;

		public override int GetHashCode() => (Id ?? "").GetHashCode();

		public void Load()
		{
			if(_loaded)
				return;

			var stats = Database.GetCardFromId(Id);
			if(stats == null)
				return;
			PlayerClass = stats.PlayerClass;
			Rarity = stats.Rarity;
			Type = stats.Type;
			Name = stats.Name;
			Cost = stats.Cost;
			LocalizedName = stats.LocalizedName;
			InHandCount = stats.InHandCount;
			Text = stats._text;
			EnglishText = stats.EnglishText;
			Attack = stats.Attack;
			Health = stats.Health;
			Race = stats.Race;
			Durability = stats.Durability;
			Mechanics = stats.Mechanics;
			Artist = stats.Artist;
			Set = stats.Set;
			AlternativeNames = stats.AlternativeNames;
			AlternativeTexts = stats.AlternativeTexts;
			_dbCard = stats._dbCard;
			_loaded = true;
			OnPropertyChanged();
		}

		private static string CleanUpText(string? text, bool replaceTags = true)
		{
			if (replaceTags)
				text = text?.Replace("<b>", "").Replace("</b>", "").Replace("<i>", "").Replace("</i>", "") ?? string.Empty;
			return text?.Replace("$", "").Replace("#", "").Replace("\\n", "\n").Replace("[x]", "") ?? string.Empty;
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	internal class CardImageObject
	{
		public DrawingBrush? Image { get; }
		public int Count { get; }
		public int Cost { get; }
		public bool Jousted { get; }
		public bool ColoredFrame { get; }
		public bool ColoredGem { get; }
		public bool Created { get; }
		public string? Theme { get; }
		public int TextColorHash { get; }
		public bool BaconCard { get; }
		public CardWinrates? CardWinrates { get;  }
		public bool IsMulliganOption { get; }

		public CardImageObject(DrawingBrush image, Card card) : this(card)
		{
			Image = image;
		}

		public CardImageObject(Card card)
		{
			Count = card.Count;
			Cost = card.Cost;
			Jousted = card.Jousted;
			ColoredFrame = Config.Instance.RarityCardFrames;
			ColoredGem = Config.Instance.RarityCardGems;
			Theme = ThemeManager.CurrentTheme?.Name;
			TextColorHash = card.ColorPlayer.Color.GetHashCode();
			Created = card.IsCreated;
			BaconCard = card.BaconCard;
			CardWinrates = card.CardWinrates;
			IsMulliganOption = card.IsMulliganOption;
		}

		public override bool Equals(object obj)
		{
			var cardObj = obj as CardImageObject;
			return cardObj != null && Equals(cardObj);
		}

		protected bool Equals(CardImageObject other)
			=> Count == other.Count && Cost == other.Cost && Jousted == other.Jousted && ColoredFrame == other.ColoredFrame && ColoredGem == other.ColoredGem
				&& string.Equals(Theme, other.Theme) && TextColorHash == other.TextColorHash && Created == other.Created && BaconCard == other.BaconCard
				&& (CardWinrates?.Equals(other.CardWinrates) ?? CardWinrates.HasValue == other.CardWinrates.HasValue) && IsMulliganOption == other.IsMulliganOption;

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = Count + 1;
				hashCode = (hashCode * 397) ^ (Cost + 1);
				hashCode = (hashCode * 397) ^ (Jousted.GetHashCode() + 1);
				hashCode = (hashCode * 397) ^ (ColoredFrame.GetHashCode() + 1);
				hashCode = (hashCode * 397) ^ (ColoredGem.GetHashCode() + 1);
				hashCode = (hashCode * 397) ^ (Theme?.GetHashCode() ?? 1);
				hashCode = (hashCode * 397) ^ TextColorHash;
				hashCode = (hashCode * 397) ^ (Created.GetHashCode() + 1);
				hashCode = (hashCode * 397) ^ (BaconCard.GetHashCode() + 1);
				hashCode = (hashCode * 397) ^ (CardWinrates?.GetHashCode() ?? 1);
				hashCode = (hashCode * 397) ^ (IsMulliganOption.GetHashCode() + 1);
				return hashCode;
			}
		}
	}
}
