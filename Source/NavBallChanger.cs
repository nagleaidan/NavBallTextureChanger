using System;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.IO;
using NavBallTextureChanger.Extensions;
using UnityEngine;
using File = System.IO.File;

namespace NavBallTextureChanger
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class NavBallChanger : MonoBehaviour
	{
		private const string ConfigFileName = "settings.cfg";

		[Persistent]
		private NavBallTexture _navballTexture = new NavBallTexture(GetSkinDirectory());

		private void Awake()
		{
			LoadConfig();


			GameEvents.onVesselChange.Add(OnVesselChanged);
			GameEvents.OnCameraChange.Add(OnCameraChanged);

			_navballTexture.SaveCopyOfStockTexture();
			UpdateFlightTexture();
		}


		private void OnDestroy()
		{
			GameEvents.onVesselChange.Remove(OnVesselChanged);
			GameEvents.OnCameraChange.Remove(OnCameraChanged);
		}


		private void LoadConfig()
		{
			var configPath = IOUtils.GetFilePathFor(typeof(NavBallChanger), ConfigFileName);
			var haveConfig = File.Exists(configPath);

			if (!haveConfig)
			{
				Debug.Log("[NavBallChanger] - Config file not found. Creating default");

				if (!Directory.Exists(Path.GetDirectoryName(configPath)))
					Directory.CreateDirectory(Path.GetDirectoryName(configPath));

				ConfigNode.CreateConfigFromObject(this).Save(configPath);
			}

			configPath
				.With(ConfigNode.Load)
				.Do(config => ConfigNode.LoadObjectFromConfig(this, config));
		}


		private void OnVesselChanged(Vessel data)
		{
			_navballTexture.Do(nbt => nbt.MarkMaterialsChanged());
			UpdateFlightTexture();
		}


		// Reset textures when entering IVA. Parts might have loaded or changed their internal spaces
		// in the meantime
		private void OnCameraChanged(CameraManager.CameraMode mode)
		{
			if (mode != CameraManager.CameraMode.IVA) return;

			UpdateIvaTextures();
		}


		private void UpdateFlightTexture()
		{
			_navballTexture
				.Do(nbt => nbt.SetFlightTexture());
		}


		private void UpdateIvaTextures()
		{
			_navballTexture.SaveCopyOfIvaEmissiveTexture();
			_navballTexture.Do(nbt => nbt.SetIvaTextures());
		}


		private static UrlDir GetSkinDirectory()
		{
			var skinUrl = AssemblyLoader.loadedAssemblies.GetByAssembly(Assembly.GetExecutingAssembly()).url + "/Skins";

			var directory =
				GameDatabase.Instance.root.AllDirectories.FirstOrDefault(d => d.url == skinUrl);

			if (directory == null)
				throw new InvalidOperationException("Failed to find skin directory inside GameDatabase");

			return directory;
		}
	}
}