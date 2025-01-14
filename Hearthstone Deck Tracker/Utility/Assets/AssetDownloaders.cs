﻿using Hearthstone_Deck_Tracker.Utility.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using MahApps.Metro.Controls;

namespace Hearthstone_Deck_Tracker.Utility.Assets
{
	public enum CardAssetType
	{
		FullImage,
		Portrait,
		Tile,
	}

	public static class AssetDownloaders
	{
		public static AssetDownloader<Hearthstone.Card, BitmapImage>? cardPortraitDownloader;
		public static AssetDownloader<Hearthstone.Card, BitmapImage>? cardTileDownloader;
		public static AssetDownloader<Hearthstone.Card, BitmapImage>? cardImageDownloader;

		static AssetDownloaders()
		{
			try
			{
				cardPortraitDownloader = new AssetDownloader<Hearthstone.Card, BitmapImage>(
					Path.Combine(Config.AppDataPath, "Images", "CardPortraits"),
					(Hearthstone.Card card) => $"https://art.hearthstonejson.com/v1/256x/{card.Id}.jpg",
					(Hearthstone.Card card) => $"{card.Id}.jpg",
					Helper.BitmapImageFromBytes,
					maxCacheSize: 500,
					placeholderAsset: "pack://application:,,,/Resources/faceless_manipulator.png"
				);
			}
			catch(ArgumentException e)
			{
				Log.Error($"Could not create asset downloader to download card portraits: {e.Message}");
			}

			try
			{
				cardTileDownloader = new AssetDownloader<Hearthstone.Card, BitmapImage>(
					Path.Combine(Config.AppDataPath, "Images", "CardTiles"),
					(Hearthstone.Card card) => $"https://art.hearthstonejson.com/v1/tiles/{card.Id}.jpg",
					(Hearthstone.Card card) => $"{card.Id}.jpg",
					Helper.BitmapImageFromBytes,
					maxCacheSize: 10_000, // About 2KB per tile. Caching up to 20MB.
					placeholderAsset: "pack://application:,,,/Resources/card-tile-placeholder.jpg"
				);
			}
			catch(ArgumentException e)
			{
				Log.Error($"Could not create asset downloader to download card tiles: {e.Message}");
			}

			try
			{
				cardImageDownloader = new AssetDownloader<Hearthstone.Card, BitmapImage>(
					Path.Combine(Config.AppDataPath, "Images", "CardImages"),
					card => $"https://art.hearthstonejson.com/v1/{(card.BaconCard ? "bgs" : "render")}/latest" +
					        $"/{Config.Instance.SelectedLanguage}/{(Config.Instance.HighResolutionCardImages ? "512x" : "256x")}" +
					        $"/{card.Id}{(card.BaconTriple ? "_triple" : "")}.png",
					card => $"{card.Id}{(card.BaconTriple ? "_triple" : "")}.png",
					Helper.BitmapImageFromBytes,
					maxCacheSize: 200,
					placeholderAsset: "pack://application:,,,/Resources/faceless_manipulator.png"
				);
				ConfigWrapper.CardImageConfigs.CardResolutionChanged += () => cardImageDownloader.ClearStorage();
			}
			catch(ArgumentException e)
			{
				Log.Error($"Could not create asset downloader to download card images: {e.Message}");
			}
		}


		public static AssetDownloader<Hearthstone.Card, BitmapImage>? GetCardAssetDownloader(CardAssetType type)
		{
			switch(type)
			{
				case CardAssetType.FullImage:
					return cardImageDownloader;
				case CardAssetType.Portrait:
					return cardPortraitDownloader;
				case CardAssetType.Tile:
					return cardTileDownloader;
				default:
					throw new NotImplementedException($"CardAssetType {type} is not implemented");
			}
		}
	}
}
