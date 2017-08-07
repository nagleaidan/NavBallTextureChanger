using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using KSP.UI.Screens.Flight;
using NavBallTextureChanger.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NavBallTextureChanger
{
	class NavBallTexture : IPersistenceLoad
	{
		private const string StockTextureFileName = "stock.png";
		private const string StockEmissiveTextureFileName = "stock_emissive.png";

		private const string IvaNavBallRendererName = "NavSphere";

		private static bool _savedStockTexture = false;
		private static bool _savedEmissiveTexture = false;
		private readonly Lazy<Texture> _stockTexture = null;
		private readonly Lazy<Material> _flightMaterial = null;
		// IVA materials are not cached because mods might spawn new IVAs or change/destroy existing ones
		// we'll grab those materials only when we're about to use them

		private readonly UrlDir _skinDirectory;

		private Texture _mainTextureRef;
		private Texture _emissiveTextureRef;

		[Persistent]
		private string TextureUrl = string.Empty;
		[Persistent]
		private string EmissiveUrl = string.Empty;
		[Persistent]
		private Color EmissiveColor = new Color(0.376f, 0.376f, 0.376f, 1f);
		[Persistent]
		private bool Flight = true;
		[Persistent]
		private bool Iva = true;


		public NavBallTexture([NotNull] UrlDir skinDirectory)
		{
			if (skinDirectory == null) throw new ArgumentNullException("skinDirectory");

			_stockTexture = new Lazy<Texture>(GetStockTexture);
			_flightMaterial = new Lazy<Material>(GetFlightNavBallMaterial);

			_skinDirectory = skinDirectory;
		}


		private static Material GetFlightNavBallMaterial()
		{
			return FlightUIModeController.Instance
				.With(controller => controller.GetComponentInChildren<NavBall>())
				.With(nb => nb.navBall)
				.With(nb => nb.GetComponent<Renderer>())
				.With(r => r.sharedMaterial);
		}


		// could be multiple IvaNavBalls. If they don't share a material, we might miss some
		// so we'll just target everything we can find
		private static List<Material> GetIvaNavBallMaterials()
		{
			return InternalSpace.Instance
				.With(space => space.GetComponentsInChildren<InternalNavBall>())
				.Select(inb => TransformExtension.FindChild(inb.transform, IvaNavBallRendererName))
				.Where(t => t != null)
				.Select(inb => inb.GetComponent<Renderer>())
				.Where(r => r != null)
				.Select(r => r.sharedMaterial).ToList()
				.Do(l =>
				{
					// just in case the hierarchy changes on us or NavSphere gets renamed some day
					if (!l.Any() && InternalSpace.Instance.With(space => space.GetComponentsInChildren<InternalNavBall>()).Length > 0)
						Debug.Log("[NavBallChanger] - There seems to be an IVA NavBall texture but its renderer wasn't found.");
				});
		}


		private Texture GetStockTexture()
		{
			return _flightMaterial.Value
					.With(m => m.GetTexture("_MainTexture"));
		}


		private bool SaveCopyOfTexture([NotNull] string textureUrl, [NotNull] Texture target)
		{
			if (textureUrl == null) throw new ArgumentNullException("textureUrl");
			if (target == null) throw new ArgumentNullException("target");

			bool successful = false;

			try
			{
				KSPUtil.GetOrCreatePath("GameData/" + _skinDirectory.url);

				target
					.With(tar => ((Texture2D)tar).CreateReadable())
					.Do(readable =>
					{
						successful = readable.SaveToDisk(textureUrl);
					}).Do(Object.Destroy);
			}
			catch (UnauthorizedAccessException e)
			{
				Debug.LogError("[NavBallChanger] - Could not create copy of stock NavBall texture inside directory '" + _skinDirectory.url +
						  "' due to insufficient permissions.");
				Debug.LogException(e);
			}
			catch (Exception e)
			{
				Debug.LogError("[NavBallChanger] - Error while creating copy of stock NavBall texture.");
				Debug.LogException(e);
			}

			return successful;
		}


		public void SaveCopyOfIvaEmissiveTexture()
		{
			var stockEmissiveUrl = _skinDirectory.url + "/" + Path.GetFileNameWithoutExtension(StockEmissiveTextureFileName);

			if (GameDatabase.Instance.GetTexture(stockEmissiveUrl, false) != null || _savedEmissiveTexture)
				return;

			GetIvaNavBallMaterials()
				.FirstOrDefault(m => m.GetTexture("_Emissive") != null)
				.With(m => m.GetTexture("_Emissive"))
				.Do(emissionTex => _savedEmissiveTexture = SaveCopyOfTexture(stockEmissiveUrl, emissionTex))
				.If(t => _savedEmissiveTexture)
				.Do(t => Debug.Log("[NavBallChanger] - Saved a copy of stock IVA emissive texture to " + stockEmissiveUrl));
		}


		public void SaveCopyOfStockTexture()
		{
			var stockUrl = _skinDirectory.url + "/" + Path.GetFileNameWithoutExtension(StockTextureFileName);

			if (GameDatabase.Instance.GetTexture(stockUrl, false) != null || _savedStockTexture)
				return;

			GetStockTexture()
				.IfNull(() => Debug.Log("[NavBallChanger] - Could not create copy of stock texture"))
				.Do(flightTexture => _savedStockTexture = SaveCopyOfTexture(stockUrl, flightTexture))
				.If(t => _savedStockTexture)
				.Do(t => Debug.Log("[NavBallChanger] - Saved a copy of stock NavBall texture to " + stockUrl));
		}


		private Maybe<Texture> GetTextureUsingUrl(string url)
		{
			if (string.IsNullOrEmpty(url)) return Maybe<Texture>.None;

			// treat url first as fully qualified, then as relative to the skins dir if nothing was found
			return
				GameDatabase.Instance.GetTexture(url, false)
					.With(t => t as Texture)
					.ToMaybe()
					.Or(() =>
					{
						var urlDir = _skinDirectory.url + (url.StartsWith("/")
							? string.Empty
							: "/");

						return
							GameDatabase.Instance.GetTextureIn(urlDir,
								url.Split('/').Last(), false) as Texture;
					})
					.ToMaybe()
					.IfNull(() => Debug.LogError("[NavBallChanger] - Url '" + url + "' not found"));
		}


		public void MarkMaterialsChanged()
		{
			_flightMaterial.Reset();
		}


		public void SetFlightTexture()
		{
			_mainTextureRef
				.If(t => Flight)
				.Do(newTex => _flightMaterial.Value.SetTexture("_MainTexture", newTex))
				.Do(t => Debug.Log("[NavBallChanger] - Changed flight NavBall texture"));
		}


		private void ForEachIvaMaterial(Action<Material> action)
		{
			foreach (var mat in GetIvaNavBallMaterials())
				action(mat);
		}


		public void SetIvaTextures()
		{
			if (!Iva) return;

			_mainTextureRef
				.Do(t =>
					ForEachIvaMaterial(m =>
					{
						m.SetTexture("_MainTex", t);

						// for some ungodly reason, the IVA texture is flipped horizontally ;\
						m.SetTextureScale("_MainTex", new Vector2(-1f, 1f));
						m.SetTextureOffset("_MainTex", new Vector2(1f, 0f));
					}))
					.With(t => GetIvaNavBallMaterials());

			_emissiveTextureRef
				.Do(t =>
					ForEachIvaMaterial(m =>
					{
						m.SetTexture("_Emissive", t);
						m.SetTextureScale("_Emissive", new Vector2(-1f, 1f));
						m.SetTextureOffset("_Emissive", new Vector2(1f, 0f));
					}))
					.With(t => GetIvaNavBallMaterials());


			ForEachIvaMaterial(m =>
			{
				m.SetColor("_EmissiveColor", EmissiveColor);
			});
		}


		public void PersistenceLoad()
		{
			_mainTextureRef = GetTextureUsingUrl(TextureUrl).Or(_stockTexture.Value);
			_emissiveTextureRef = GetTextureUsingUrl(EmissiveUrl).Or((Texture)null);
		}
	}
}