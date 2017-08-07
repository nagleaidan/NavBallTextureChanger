using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NavBallTextureChanger.Extensions
{
	public static class Texture2DExtensions
	{
		/// <summary>
		/// Saves texture into plugin dir with supplied name.
		/// Precondition: texture must be readable
		/// </summary>
		public static bool SaveToDisk(this Texture2D texture, string pathInGameData)
		{
			// texture format - needs to be ARGB32, RGBA32, RGB24 or Alpha8
			var validFormats = new List<TextureFormat>{ TextureFormat.Alpha8,
														TextureFormat.RGB24,
														TextureFormat.RGBA32,
														TextureFormat.ARGB32};

			if (!validFormats.Contains(texture.format))
				return CreateReadable(texture).SaveToDisk(pathInGameData);


			if (pathInGameData.StartsWith("/"))
				pathInGameData = pathInGameData.Substring(1);

			pathInGameData = "GameData/" + pathInGameData;

			if (!pathInGameData.EndsWith(".png"))
				pathInGameData += ".png";

			try
			{
				var fullPath = Path.Combine(KSPUtil.ApplicationRootPath, pathInGameData);
				var directory = Path.GetDirectoryName(fullPath);

				if (directory == null)
					throw new DirectoryNotFoundException("Couldn't get directory from " + fullPath);


				if (!Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				var file = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.Write);
				var writer = new BinaryWriter(file);
				writer.Write(texture.EncodeToPNG());

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}


		public static Texture2D CreateReadable(this Texture2D original, Material blitMaterial = null)
		{
			if (original == null) throw new ArgumentNullException("original");

			if (original.width == 0 || original.height == 0)
				throw new Exception("Invalid image dimensions");

			var finalTexture = new Texture2D(original.width, original.height);

			// isn't read or writeable ... we'll have to get tricksy
			var rt = RenderTexture.GetTemporary(original.width, original.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);

			if (blitMaterial == null)
				Graphics.Blit(original, rt);
			else Graphics.Blit(original, rt, blitMaterial);

			RenderTexture.active = rt;

			finalTexture.ReadPixels(new Rect(0, 0, finalTexture.width, finalTexture.height), 0, 0);

			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary(rt);

			finalTexture.Apply(true);

			return finalTexture;
		}
	}
}